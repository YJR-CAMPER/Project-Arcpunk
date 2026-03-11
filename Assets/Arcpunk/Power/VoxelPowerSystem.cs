// ── VoxelPowerSystem.cs ──
// 복셀 좌표 기반 전력 시스템.
// 배선(CopperWire)을 BFS로 따라가며 연결된 전력망을 자동 구성.
// 매 틱: 발전 → 충전 → 소비.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Weather;

namespace Arcpunk.Power
{
    public class VoxelPowerSystem : MonoBehaviour
    {
        public static VoxelPowerSystem Instance { get; private set; }

        private VoxelWorld _world;
        private List<PowerNetwork> _networks = new();

        // 전력 블록 좌표 → 소속 네트워크
        private Dictionary<Vector3Int, PowerNetwork> _blockToNetwork = new();

        // 모든 피뢰침 좌표 (LightningSystem용)
        private List<Vector3Int> _allProducers = new();

        // 더티 플래그 — 블록 변경 시 true, 다음 틱에 리빌드
        private bool _needsRebuild = true;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _world = VoxelWorld.Instance;

            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick += OnTick;

            // 낙뢰 이벤트 구독 (피뢰침에 낙뢰가 맞으면 보너스 발전)
            var lightning = LightningSystem.Instance;
            if (lightning != null)
                lightning.OnLightningStrike += OnLightningStrike;
        }

        private void OnDestroy()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick -= OnTick;

            var lightning = LightningSystem.Instance;
            if (lightning != null)
                lightning.OnLightningStrike -= OnLightningStrike;
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        /// <summary>블록 변경 시 호출. 다음 틱에 네트워크 리빌드.</summary>
        public void OnBlockChanged(int wx, int wy, int wz, BlockType type)
        {
            _needsRebuild = true;
        }

        /// <summary>모든 피뢰침 좌표 반환 (LightningSystem용).</summary>
        public List<Vector3Int> GetAllProducerPositions() => _allProducers;

        /// <summary>특정 좌표의 소비 블록이 현재 전력을 받고 있는지 확인.</summary>
        public bool IsBlockPowered(Vector3Int pos)
        {
            if (_blockToNetwork.TryGetValue(pos, out PowerNetwork network))
                return network.IsConsumerPowered(pos);
            return false;
        }

        /// <summary>특정 좌표가 속한 네트워크의 총 저장량.</summary>
        public float GetStoredPower(Vector3Int pos)
        {
            if (_blockToNetwork.TryGetValue(pos, out PowerNetwork network))
                return network.TotalStored;
            return 0;
        }

        /// <summary>모든 네트워크의 총 저장/총 용량.</summary>
        public (float stored, float capacity) GetGlobalPowerStats()
        {
            float stored = 0, capacity = 0;
            foreach (var net in _networks)
            {
                stored += net.TotalStored;
                capacity += net.TotalCapacity;
            }
            return (stored, capacity);
        }

        // ═══════════════════════════════════════
        // 틱 처리
        // ═══════════════════════════════════════

        private void OnTick(int tickNumber)
        {
            if (_needsRebuild)
            {
                RebuildAllNetworks();
                _needsRebuild = false;
            }

            var weather = WeatherSystem.Instance?.CurrentWeather
                          ?? new WeatherState { SurgeMultiplier = 0.1f };

            foreach (var network in _networks)
                network.Tick(weather, _world);
        }

        // ═══════════════════════════════════════
        // 낙뢰 처리
        // ═══════════════════════════════════════

        private void OnLightningStrike(Vector3 worldPos)
        {
            // 낙뢰 지점에서 가장 가까운 피뢰침에 보너스 발전
            Vector3Int strikeBlock = VoxelWorld.WorldPosToBlockCoord(worldPos);

            float closestDist = float.MaxValue;
            PowerNetwork closestNetwork = null;

            foreach (var rod in _allProducers)
            {
                float dist = Vector3Int.Distance(rod, strikeBlock);
                if (dist < closestDist && dist < 5f) // 5블록 이내만
                {
                    closestDist = dist;
                    if (_blockToNetwork.TryGetValue(rod, out PowerNetwork net))
                        closestNetwork = net;
                }
            }

            if (closestNetwork != null)
            {
                // 직격 보너스: 서지 배율 × 50의 즉시 충전
                var weather = WeatherSystem.Instance?.CurrentWeather
                              ?? new WeatherState { SurgeMultiplier = 1f };
                float bonus = 50f * weather.SurgeMultiplier;
                closestNetwork.AddInstantPower(bonus);

                Debug.Log($"[Power] Lightning direct hit! +{bonus:F0} power");
            }
        }

        // ═══════════════════════════════════════
        // 네트워크 리빌드
        // ═══════════════════════════════════════

        private void RebuildAllNetworks()
        {
            _networks.Clear();
            _blockToNetwork.Clear();
            _allProducers.Clear();

            // 월드의 모든 전력 블록을 스캔
            var powerBlocks = FindAllPowerBlocks();
            var visited = new HashSet<Vector3Int>();
            int networkId = 0;

            foreach (var pos in powerBlocks)
            {
                if (visited.Contains(pos)) continue;

                // BFS로 연결된 전력 블록 그룹 탐색
                var network = new PowerNetwork(networkId++);
                var queue = new Queue<Vector3Int>();
                queue.Enqueue(pos);
                visited.Add(pos);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    BlockType bt = _world.GetBlock(current.x, current.y, current.z);
                    ref BlockDef def = ref BlockData.Defs[(ushort)bt];

                    network.AddBlock(current, ref def);
                    _blockToNetwork[current] = network;

                    if (def.PowerRole == PowerRole.Producer)
                        _allProducers.Add(current);

                    // 6방향 이웃 탐색
                    foreach (var dir in _neighbors)
                    {
                        var next = current + dir;
                        if (visited.Contains(next)) continue;

                        BlockType nbt = _world.GetBlock(next.x, next.y, next.z);
                        if (BlockData.Defs[(ushort)nbt].IsPowerBlock)
                        {
                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }

                _networks.Add(network);
            }

            Debug.Log($"[Power] Rebuilt: {_networks.Count} networks, " +
                      $"{_allProducers.Count} rods, " +
                      $"{_blockToNetwork.Count} power blocks");
        }

        private List<Vector3Int> FindAllPowerBlocks()
        {
            var result = new List<Vector3Int>();
            int maxX = _world.WorldSizeX * Chunk.SIZE;
            int maxY = _world.WorldSizeY * Chunk.SIZE;
            int maxZ = _world.WorldSizeZ * Chunk.SIZE;

            for (int x = 0; x < maxX; x++)
            for (int y = 0; y < maxY; y++)
            for (int z = 0; z < maxZ; z++)
            {
                BlockType bt = _world.GetBlock(x, y, z);
                if (bt != BlockType.Air && BlockData.Defs[(ushort)bt].IsPowerBlock)
                    result.Add(new Vector3Int(x, y, z));
            }

            return result;
        }

        private static readonly Vector3Int[] _neighbors =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, new(0, 0, 1), new(0, 0, -1)
        };
    }
}
