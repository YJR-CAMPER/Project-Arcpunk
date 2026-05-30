// ── WireManager.cs ──
// 와이어 연결 전체를 관리하는 싱글턴 매니저.
// 이중 인덱싱(_wires + _adj)으로 O(1) 조회 제공.
// dirty flag + connected component 캐싱으로 BFS 최적화.
// VoxelPowerSystem이 이 매니저를 통해 네트워크를 조회.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arcpunk.Power
{
    public class WireManager : MonoBehaviour
    {
        public static WireManager Instance { get; private set; }

        // ── 설정 ──
        public const int MaxWiresPerBlock = 8;

        // ── 이벤트 (WireRenderer 등이 구독) ──
        public event Action<WireData> OnWireAdded;
        public event Action<WireData> OnWireRemoved;
        public event Action OnNetworkChanged; // dirty → rebuild 완료 시

        // ── 코어 데이터: 이중 인덱싱 ──
        // wireId → WireData (와이어 정보 조회)
        private Dictionary<int, WireData> _wires = new();
        // 블록 좌표 → 연결된 wireId 셋 (인접 탐색)
        private Dictionary<Vector3Int, HashSet<int>> _adj = new();

        private int _nextWireId = 1;

        // ── 네트워크 캐시 ──
        private bool _isDirty = true;
        // networkId → 해당 네트워크에 속한 블록 좌표 셋
        private List<HashSet<Vector3Int>> _networks = new();
        // 블록 좌표 → 소속 networkId (역참조)
        private Dictionary<Vector3Int, int> _blockToNetwork = new();

        // ── 읽기 전용 프로퍼티 ──
        public IReadOnlyDictionary<int, WireData> AllWires => _wires;
        public int WireCount => _wires.Count;
        public bool IsDirty => _isDirty;
        public int NetworkCount => _networks.Count;

        // =====================================================
        //  Unity 생명주기
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

        // =====================================================
        //  와이어 추가 / 삭제
        // =====================================================

        /// <summary>두 블록 사이에 와이어를 추가. 성공 시 wireId 반환, 실패 시 -1.</summary>
        public int AddWire(Vector3Int a, Vector3Int b)
        {
            // ── 검증 ──
            if (a == b)
            {
                Debug.LogWarning("[WireManager] 자기 자신에 연결 불가.");
                return -1;
            }

            if (HasWireBetween(a, b))
            {
                Debug.LogWarning($"[WireManager] 이미 {a}↔{b} 와이어가 존재함.");
                return -1;
            }

            if (GetWireCount(a) >= MaxWiresPerBlock)
            {
                Debug.LogWarning($"[WireManager] {a}의 최대 연결 수({MaxWiresPerBlock}) 초과.");
                return -1;
            }

            if (GetWireCount(b) >= MaxWiresPerBlock)
            {
                Debug.LogWarning($"[WireManager] {b}의 최대 연결 수({MaxWiresPerBlock}) 초과.");
                return -1;
            }

            // ── 생성 ──
            int id = _nextWireId++;
            var wire = new WireData(id, a, b);

            _wires[id] = wire;

            if (!_adj.TryGetValue(a, out var setA))
            {
                setA = new HashSet<int>();
                _adj[a] = setA;
            }
            setA.Add(id);

            if (!_adj.TryGetValue(b, out var setB))
            {
                setB = new HashSet<int>();
                _adj[b] = setB;
            }
            setB.Add(id);

            _isDirty = true;
            OnWireAdded?.Invoke(wire);

            Debug.Log($"[WireManager] 와이어 추가: {wire}");
            return id;
        }

        /// <summary>wireId로 와이어를 삭제.</summary>
        public bool RemoveWire(int wireId)
        {
            if (!_wires.TryGetValue(wireId, out var wire))
                return false;

            // _adj 양쪽에서 제거
            if (_adj.TryGetValue(wire.BlockA, out var setA))
            {
                setA.Remove(wireId);
                if (setA.Count == 0) _adj.Remove(wire.BlockA);
            }

            if (_adj.TryGetValue(wire.BlockB, out var setB))
            {
                setB.Remove(wireId);
                if (setB.Count == 0) _adj.Remove(wire.BlockB);
            }

            _wires.Remove(wireId);
            _isDirty = true;
            OnWireRemoved?.Invoke(wire);

            Debug.Log($"[WireManager] 와이어 삭제: {wire}");
            return true;
        }

        /// <summary>특정 좌표에 연결된 모든 와이어를 삭제. 블록 파괴 시 호출.</summary>
        public void RemoveAllWiresAt(Vector3Int pos)
        {
            if (!_adj.TryGetValue(pos, out var wireIds))
                return;

            // 순회 중 컬렉션 수정 방지용 복사
            var idsCopy = new List<int>(wireIds);
            foreach (int id in idsCopy)
            {
                RemoveWire(id);
            }
        }

        // =====================================================
        //  조회 API
        // =====================================================

        /// <summary>특정 좌표에 연결된 와이어 개수.</summary>
        public int GetWireCount(Vector3Int pos)
        {
            return _adj.TryGetValue(pos, out var set) ? set.Count : 0;
        }

        /// <summary>특정 좌표와 와이어로 연결된 이웃 블록 좌표 목록.
        /// VoxelPowerSystem의 BFS가 이걸 호출.</summary>
        public void GetConnectedBlocks(Vector3Int pos, List<Vector3Int> result)
        {
            result.Clear();
            if (!_adj.TryGetValue(pos, out var wireIds))
                return;

            foreach (int id in wireIds)
            {
                if (_wires.TryGetValue(id, out var wire))
                {
                    result.Add(wire.GetOtherEnd(pos));
                }
            }
        }

        /// <summary>두 좌표 사이에 와이어가 있는지 확인.</summary>
        public bool HasWireBetween(Vector3Int a, Vector3Int b)
        {
            if (!_adj.TryGetValue(a, out var wireIds))
                return false;

            foreach (int id in wireIds)
            {
                if (_wires.TryGetValue(id, out var wire) && wire.IsConnectedTo(b))
                    return true;
            }
            return false;
        }

        /// <summary>두 좌표 사이의 wireId를 반환. 없으면 -1.</summary>
        public int GetWireIdBetween(Vector3Int a, Vector3Int b)
        {
            if (!_adj.TryGetValue(a, out var wireIds))
                return -1;

            foreach (int id in wireIds)
            {
                if (_wires.TryGetValue(id, out var wire) && wire.IsConnectedTo(b))
                    return id;
            }
            return -1;
        }

        /// <summary>특정 좌표에 연결된 모든 wireId를 반환.</summary>
        public IEnumerable<int> GetWireIdsAt(Vector3Int pos)
        {
            if (_adj.TryGetValue(pos, out var set))
                return set;
            return Array.Empty<int>();
        }

        // =====================================================
        //  네트워크 캐시 (Connected Component)
        // =====================================================

        /// <summary>dirty 상태일 때만 네트워크를 재계산.
        /// VoxelPowerSystem.OnTick 시작부에서 호출.</summary>
        public void RebuildNetworksIfDirty()
        {
            if (!_isDirty) return;

            _networks.Clear();
            _blockToNetwork.Clear();

            var visited = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            var neighborBuf = new List<Vector3Int>();

            foreach (var pos in _adj.Keys)
            {
                if (visited.Contains(pos)) continue;

                // BFS로 connected component 탐색
                var network = new HashSet<Vector3Int>();
                int networkId = _networks.Count;

                queue.Clear();
                queue.Enqueue(pos);
                visited.Add(pos);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    network.Add(current);
                    _blockToNetwork[current] = networkId;

                    GetConnectedBlocks(current, neighborBuf);
                    foreach (var neighbor in neighborBuf)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                _networks.Add(network);
            }

            _isDirty = false;
            OnNetworkChanged?.Invoke();

            Debug.Log($"[WireManager] 네트워크 재계산 완료: {_networks.Count}개 네트워크, {_wires.Count}개 와이어");
        }

        /// <summary>특정 블록이 속한 네트워크의 모든 블록 좌표.</summary>
        public HashSet<Vector3Int> GetNetwork(Vector3Int pos)
        {
            if (_blockToNetwork.TryGetValue(pos, out int netId) && netId < _networks.Count)
                return _networks[netId];
            return null;
        }

        /// <summary>특정 블록의 네트워크 ID. 미소속이면 -1.</summary>
        public int GetNetworkId(Vector3Int pos)
        {
            return _blockToNetwork.TryGetValue(pos, out int id) ? id : -1;
        }

        /// <summary>모든 네트워크를 순회용으로 반환.</summary>
        public IReadOnlyList<HashSet<Vector3Int>> GetAllNetworks()
        {
            return _networks;
        }

        // =====================================================
        //  직렬화 (저장 / 로드)
        // =====================================================

        /// <summary>모든 와이어를 JSON 문자열로 직렬화.</summary>
        public string SerializeToJson()
        {
            var save = new WireSaveData();
            var list = new List<WireSerializable>();

            foreach (var kvp in _wires)
            {
                list.Add(WireSerializable.FromWireData(kvp.Value));
            }

            save.Wires = list.ToArray();
            return JsonUtility.ToJson(save, true);
        }

        /// <summary>JSON 문자열에서 와이어를 복원.</summary>
        public void DeserializeFromJson(string json)
        {
            // 기존 데이터 초기화
            _wires.Clear();
            _adj.Clear();
            _nextWireId = 1;

            if (string.IsNullOrEmpty(json)) return;

            var save = JsonUtility.FromJson<WireSaveData>(json);
            if (save?.Wires == null) return;

            foreach (var ws in save.Wires)
            {
                var wire = ws.ToWireData();

                _wires[wire.WireId] = wire;

                if (!_adj.TryGetValue(wire.BlockA, out var setA))
                {
                    setA = new HashSet<int>();
                    _adj[wire.BlockA] = setA;
                }
                setA.Add(wire.WireId);

                if (!_adj.TryGetValue(wire.BlockB, out var setB))
                {
                    setB = new HashSet<int>();
                    _adj[wire.BlockB] = setB;
                }
                setB.Add(wire.WireId);

                // nextWireId를 복원된 최대값 이후로 설정
                if (wire.WireId >= _nextWireId)
                    _nextWireId = wire.WireId + 1;
            }

            _isDirty = true;
            Debug.Log($"[WireManager] {_wires.Count}개 와이어 로드 완료.");
        }

        /// <summary>wires.json 파일 경로.</summary>
        public static string GetSavePath(string worldName)
        {
            return Path.Combine(Application.persistentDataPath, worldName, "wires.json");
        }

        /// <summary>파일로 저장.</summary>
        public void SaveToFile(string worldName)
        {
            string path = GetSavePath(worldName);
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = SerializeToJson();
            File.WriteAllText(path, json);
            Debug.Log($"[WireManager] 저장 완료: {path} ({_wires.Count}개 와이어)");
        }

        /// <summary>파일에서 로드.</summary>
        public void LoadFromFile(string worldName)
        {
            string path = GetSavePath(worldName);
            if (!File.Exists(path))
            {
                Debug.Log("[WireManager] 저장 파일 없음, 빈 상태로 시작.");
                return;
            }

            string json = File.ReadAllText(path);
            DeserializeFromJson(json);
        }

        // =====================================================
        //  디버그
        // =====================================================

        /// <summary>전체 와이어 상태를 콘솔에 출력.</summary>
        public void DebugDump()
        {
            Debug.Log($"=== WireManager Dump ===");
            Debug.Log($"와이어 수: {_wires.Count}, 연결된 블록 수: {_adj.Count}");
            Debug.Log($"네트워크 수: {_networks.Count}, dirty: {_isDirty}");

            foreach (var kvp in _wires)
            {
                Debug.Log($"  {kvp.Value}");
            }

            foreach (var kvp in _adj)
            {
                Debug.Log($"  블록 {kvp.Key}: {kvp.Value.Count}개 와이어 연결");
            }
        }
    }
}
