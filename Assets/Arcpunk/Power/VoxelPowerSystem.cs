// ── VoxelPowerSystem.cs (수정 v3) ──
// 기존 BlockDef.IsPowerBlock / BlockDef.PowerRole / BlockDef.BatteryCapacity 등을
// 그대로 사용. PowerRole enum은 Arcpunk.Voxel에 이미 정의되어 있으므로 재정의 X.
//
// 기존 시스템이 호출하던 API 전부 유지:
//   - GetGlobalPowerStats()     → ArcGun, DebugHUD
//   - ConsumeGlobal()           → ArcGun
//   - GetAllProducerPositions() → LightningSystem

using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Voxel;

namespace Arcpunk.Power
{
    public class VoxelPowerSystem : MonoBehaviour
    {
        public static VoxelPowerSystem Instance { get; private set; }

        // ── 참조 ──
        private VoxelWorld _world;

        // ── 블록 좌표 → powered 여부 ──
        private Dictionary<Vector3Int, bool> _poweredBlocks = new();

        // ── 배터리 저장량 ──
        private Dictionary<Vector3Int, float> _batteryStorage = new();

        // ── 네트워크 ──
        private List<PowerNetwork> _networks = new();
        private bool _isDirty = true;

        // 등록된 전력 블록 좌표
        private HashSet<Vector3Int> _registeredBlocks = new();

        // BFS 버퍼
        private Queue<Vector3Int> _bfsQueue = new();
        private HashSet<Vector3Int> _bfsVisited = new();
        private List<Vector3Int> _neighborBuf = new(8);

        // =====================================================
        //  내부 구조
        // =====================================================

        private class PowerNetwork
        {
            public List<Vector3Int> Producers = new();
            public List<Vector3Int> Storages = new();
            public List<Vector3Int> Consumers = new();
            public float TotalProduced;
        }

        // =====================================================
        //  초기화
        // =====================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Core.TickSystem tick, VoxelWorld world)
        {
            _world = world;
            if (tick != null)
                tick.OnTick += OnTick;
        }

        private void Start()
        {
            if (_world == null)
                _world = VoxelWorld.Instance;
        }

        private void OnDestroy()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick -= OnTick;
        }

        // =====================================================
        //  틱 루프
        // =====================================================

        private void OnTick(int tickNumber)
        {
            if (_world == null) return;

            if (_isDirty)
            {
                RebuildNetworks();
                _isDirty = false;
            }

            _poweredBlocks.Clear();

            foreach (var net in _networks)
            {
                ProcessNetwork(net);
            }
        }

        // =====================================================
        //  네트워크 빌드 (6방향 인접 BFS)
        //  Wire 시스템 전환 시 GetNeighbors()만 교체하면 됨.
        // =====================================================

        private void RebuildNetworks()
        {
            _networks.Clear();
            _bfsVisited.Clear();

            foreach (var startPos in _registeredBlocks)
            {
                if (_bfsVisited.Contains(startPos)) continue;

                var net = new PowerNetwork();
                BFS(startPos, net);
                _networks.Add(net);
            }
        }

        private void BFS(Vector3Int start, PowerNetwork net)
        {
            _bfsQueue.Clear();
            _bfsQueue.Enqueue(start);
            _bfsVisited.Add(start);

            while (_bfsQueue.Count > 0)
            {
                var pos = _bfsQueue.Dequeue();

                BlockType bt = _world.GetBlock(pos.x, pos.y, pos.z);
                BlockDef def = BlockData.Defs[(ushort)bt];

                if (!def.IsPowerBlock) continue;

                switch (def.PowerRole)
                {
                    case PowerRole.Producer:  net.Producers.Add(pos); break;
                    case PowerRole.Storage:   net.Storages.Add(pos);  break;
                    case PowerRole.Consumer:  net.Consumers.Add(pos); break;
                    case PowerRole.Conductor: break; // 전도만
                }

                GetNeighbors(pos, _neighborBuf);
                foreach (var neighbor in _neighborBuf)
                {
                    if (_bfsVisited.Contains(neighbor)) continue;

                    BlockType nbt = _world.GetBlock(neighbor.x, neighbor.y, neighbor.z);
                    BlockDef ndef = BlockData.Defs[(ushort)nbt];

                    if (ndef.IsPowerBlock)
                    {
                        _bfsVisited.Add(neighbor);
                        _bfsQueue.Enqueue(neighbor);
                    }
                }
            }
        }

        /// <summary>이웃 탐색. Wire 시스템 전환 시 여기만 교체.</summary>
        private void GetNeighbors(Vector3Int pos, List<Vector3Int> result)
        {
            result.Clear();

            // Wire 시스템 활성화 후 아래 코드로 교체:
             var wm = WireManager.Instance;
             if (wm != null) { wm.GetConnectedBlocks(pos, result); return; }

            result.Add(pos + Vector3Int.right);
            result.Add(pos + Vector3Int.left);
            result.Add(pos + Vector3Int.up);
            result.Add(pos + Vector3Int.down);
            result.Add(pos + new Vector3Int(0, 0, 1));
            result.Add(pos + new Vector3Int(0, 0, -1));
        }

        // =====================================================
        //  전력 분배
        // =====================================================

        private void ProcessNetwork(PowerNetwork net)
        {
            // 발전
            net.TotalProduced = 0f;
            foreach (var prodPos in net.Producers)
            {
                net.TotalProduced += GetProducerOutput(prodPos);
            }

            // 충전
            float surplus = net.TotalProduced;
            foreach (var storPos in net.Storages)
            {
                if (surplus <= 0f) break;
                float capacity = GetBatteryCapacity(storPos);
                float current = GetBatteryLevel(storPos);
                float space = capacity - current;
                if (space > 0f)
                {
                    float charge = Mathf.Min(surplus, space);
                    SetBatteryLevel(storPos, current + charge);
                    surplus -= charge;
                }
            }

            // 총 가용 전력
            float totalStored = 0f;
            foreach (var storPos in net.Storages)
                totalStored += GetBatteryLevel(storPos);

            float totalSupply = surplus + totalStored;

            // 총 수요
            float totalDemand = 0f;
            foreach (var consPos in net.Consumers)
                totalDemand += GetPowerDraw(consPos);

            // 소비
            if (totalSupply >= totalDemand)
            {
                foreach (var consPos in net.Consumers)
                    _poweredBlocks[consPos] = true;

                float drain = totalDemand - surplus;
                if (drain > 0f) DrainBatteries(net.Storages, drain);
            }

            // Producer/Storage도 powered
            foreach (var p in net.Producers)
                _poweredBlocks[p] = true;
            foreach (var s in net.Storages)
            {
                if (GetBatteryLevel(s) > 0f || net.TotalProduced > 0f)
                    _poweredBlocks[s] = true;
            }
        }

        private float GetProducerOutput(Vector3Int pos)
        {
            BlockDef def = BlockData.Defs[(ushort)_world.GetBlock(pos.x, pos.y, pos.z)];
            float baseOutput = def.BaseOutput > 0 ? def.BaseOutput : 5f;
            float heightMult = 1f + pos.y * 0.05f;
            return baseOutput * heightMult;
        }

        private float GetBatteryCapacity(Vector3Int pos)
        {
            BlockDef def = BlockData.Defs[(ushort)_world.GetBlock(pos.x, pos.y, pos.z)];
            return def.BatteryCapacity > 0 ? def.BatteryCapacity : 100f;
        }

        private float GetPowerDraw(Vector3Int pos)
        {
            BlockDef def = BlockData.Defs[(ushort)_world.GetBlock(pos.x, pos.y, pos.z)];
            return def.PowerDraw > 0 ? def.PowerDraw : 1f;
        }

        // =====================================================
        //  배터리 관리
        // =====================================================

        public float GetBatteryLevel(Vector3Int pos)
        {
            return _batteryStorage.TryGetValue(pos, out float val) ? val : 0f;
        }

        public void SetBatteryLevel(Vector3Int pos, float level)
        {
            _batteryStorage[pos] = Mathf.Max(0f, level);
        }

        private void DrainBatteries(List<Vector3Int> storages, float amount)
        {
            float remaining = amount;
            foreach (var pos in storages)
            {
                if (remaining <= 0f) break;
                float current = GetBatteryLevel(pos);
                if (current <= 0f) continue;
                float drain = Mathf.Min(current, remaining);
                SetBatteryLevel(pos, current - drain);
                remaining -= drain;
            }
        }

        // =====================================================
        //  공개 API
        // =====================================================

        public bool IsBlockPowered(Vector3Int pos)
        {
            return _poweredBlocks.TryGetValue(pos, out bool val) && val;
        }

        public void ConsumePower(Vector3Int pos, float amount)
        {
            foreach (var net in _networks)
            {
                if (net.Consumers.Contains(pos) || net.Producers.Contains(pos))
                {
                    DrainBatteries(net.Storages, amount);
                    return;
                }
            }
        }

        public void OnBlockDestroyed(Vector3Int pos)
        {
            _batteryStorage.Remove(pos);
            _poweredBlocks.Remove(pos);
            _registeredBlocks.Remove(pos);
            _isDirty = true;
        }

        public void OnBlockChanged(int wx, int wy, int wz, BlockType type)
        {
            var pos = new Vector3Int(wx, wy, wz);

            if (type == BlockType.Air)
            {
                OnBlockDestroyed(pos);
            }
            else
            {
                BlockDef def = BlockData.Defs[(ushort)type];
                if (def.IsPowerBlock)
                {
                    _registeredBlocks.Add(pos);

                    if (def.PowerRole == PowerRole.Storage && !_batteryStorage.ContainsKey(pos))
                        _batteryStorage[pos] = 0f;

                    _isDirty = true;
                }
            }
        }

        // =====================================================
        //  하위 호환 API (ArcGun, DebugHUD, LightningSystem)
        // =====================================================

        /// <summary>전체 전력 통계. (총 저장량, 총 용량)</summary>
        public (float stored, float capacity) GetGlobalPowerStats()
        {
            float totalStored = 0f;
            float totalCapacity = 0f;

            foreach (var pos in _registeredBlocks)
            {
                BlockType bt = _world != null ? _world.GetBlock(pos.x, pos.y, pos.z) : BlockType.Air;
                BlockDef def = BlockData.Defs[(ushort)bt];
                if (def.PowerRole == PowerRole.Storage)
                {
                    totalStored += GetBatteryLevel(pos);
                    totalCapacity += def.BatteryCapacity > 0 ? def.BatteryCapacity : 100f;
                }
            }

            return (totalStored, totalCapacity);
        }

        /// <summary>글로벌 전력 소모.</summary>
        public void ConsumeGlobal(float amount)
        {
            float remaining = amount;
            foreach (var net in _networks)
            {
                if (remaining <= 0f) break;
                DrainBatteries(net.Storages, remaining);
                remaining = 0f; // 첫 네트워크에서 전부 차감 시도
            }
        }

        /// <summary>모든 Producer 좌표 목록.</summary>
        public List<Vector3Int> GetAllProducerPositions()
        {
            var result = new List<Vector3Int>();
            foreach (var pos in _registeredBlocks)
            {
                if (_world == null) continue;
                BlockType bt = _world.GetBlock(pos.x, pos.y, pos.z);
                BlockDef def = BlockData.Defs[(ushort)bt];
                if (def.IsPowerBlock && def.PowerRole == PowerRole.Producer)
                    result.Add(pos);
            }
            return result;
        }

        // =====================================================
        //  디버그
        // =====================================================

        public void DebugDump()
        {
            Debug.Log($"=== VoxelPowerSystem ===");
            Debug.Log($"Registered: {_registeredBlocks.Count}, Powered: {_poweredBlocks.Count}, Networks: {_networks.Count}");
        }
    }
}
