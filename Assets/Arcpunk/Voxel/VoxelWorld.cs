// ── VoxelWorld.cs ──
// 복셀 월드의 중앙 관리자.
// 청크 생성, 지형 생성, 블록 편집, 더티 청크 리메싱을 담당.
// 프로토타입: 고정 크기 월드 (무한 아님).

using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Combat;

namespace Arcpunk.Voxel
{
    public class VoxelWorld : MonoBehaviour
    {
        public static VoxelWorld Instance { get; private set; }

        [Header("World Size (in chunks)")]
        [SerializeField] private int _worldSizeX = 8;   // 128 blocks
        [SerializeField] private int _worldSizeY = 4;   // 64 blocks
        [SerializeField] private int _worldSizeZ = 8;   // 128 blocks

        [Header("Terrain Generation")]
        [SerializeField] private float _noiseScale = 0.02f;
        [SerializeField] private float _terrainHeight = 20f;
        [SerializeField] private float _terrainBase = 16f;
        [SerializeField] private int _seed = 42;

        [Header("References")]
        [SerializeField] private Material _chunkMaterial; // 텍스처 아틀라스 머티리얼

        private Dictionary<Vector3Int, Chunk> _chunks = new();
        private Dictionary<Vector3Int, ChunkRenderer> _renderers = new();

        // ── 공개 프로퍼티 ──
        public int WorldSizeX => _worldSizeX;
        public int WorldSizeY => _worldSizeY;
        public int WorldSizeZ => _worldSizeZ;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            GenerateWorld();

            // 플레이어를 월드 중앙 지표면으로 이동
            var player = Player.PlayerController.Instance;
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;   // CC 켜진 채로 position 바꾸면 무시됨
                player.transform.position = GetSpawnPosition();
                if (cc != null) cc.enabled = true;
            }
        }

        // ═══════════════════════════════════════
        // 블록 조회 / 설정
        // ═══════════════════════════════════════

        /// <summary>월드 좌표로 블록 타입 조회. 범위 밖이면 Air.</summary>
        public BlockType GetBlock(int wx, int wy, int wz)
        {
            Vector3Int chunkCoord = Chunk.WorldToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(chunkCoord, out Chunk chunk))
                return BlockType.Air;

            Vector3Int local = Chunk.WorldToLocal(wx, wy, wz);
            return chunk.GetBlock(local.x, local.y, local.z);
        }

        /// <summary>월드 좌표에 블록 설정. 주변 청크도 더티 마킹.</summary>
        public void SetBlock(int wx, int wy, int wz, BlockType type)
        {
            Vector3Int chunkCoord = Chunk.WorldToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(chunkCoord, out Chunk chunk))
                return;

            Vector3Int local = Chunk.WorldToLocal(wx, wy, wz);
            chunk.SetBlock(local.x, local.y, local.z, type);

            // 청크 경계의 블록이 변경되면 이웃 청크도 리메싱 필요
            if (local.x == 0)                MarkDirty(chunkCoord + Vector3Int.left);
            if (local.x == Chunk.SIZE - 1)   MarkDirty(chunkCoord + Vector3Int.right);
            if (local.y == 0)                MarkDirty(chunkCoord + Vector3Int.down);
            if (local.y == Chunk.SIZE - 1)   MarkDirty(chunkCoord + Vector3Int.up);
            if (local.z == 0)                MarkDirty(chunkCoord + new Vector3Int(0, 0, -1));
            if (local.z == Chunk.SIZE - 1)   MarkDirty(chunkCoord + new Vector3Int(0, 0, 1));

            // 전력 시스템에 블록 변경 알림
            var powerSys = Power.VoxelPowerSystem.Instance;
            if (powerSys != null)
                powerSys.OnBlockChanged(wx, wy, wz, type);

            // 전력 블록 시각 피드백
            var powerBehaviour = Power.PowerBlockBehaviour.Instance;
            if (powerBehaviour != null)
            {
                var def = BlockData.Get(type);
                var pos = new Vector3Int(wx, wy, wz);

                if (type == BlockType.Air)
                    powerBehaviour.OnPowerBlockRemoved(pos);
                else if (def.IsPowerBlock)
                    powerBehaviour.OnPowerBlockPlaced(pos, type);
            }
            //센트리 등록 및 해제
            var sentryBehaviour = Combat.SentryBehaviour.Instance;
            if (sentryBehaviour != null)
            {
                var pos = new Vector3Int(wx, wy, wz);
                if (type == BlockType.Sentry)
                    sentryBehaviour.RegisterSentry(pos);
                else if (type == BlockType.Air)
                    sentryBehaviour.UnregisterSentry(pos);
            }
        }

        /// <summary>Vector3 월드 좌표를 정수 블록 좌표로 변환.</summary>
        public static Vector3Int WorldPosToBlockCoord(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x),
                Mathf.FloorToInt(worldPos.y),
                Mathf.FloorToInt(worldPos.z)
            );
        }

        /// <summary>블록 좌표를 월드 중심 위치로 변환.</summary>
        public static Vector3 BlockCoordToWorldPos(Vector3Int blockCoord)
        {
            return new Vector3(blockCoord.x + 0.5f, blockCoord.y + 0.5f, blockCoord.z + 0.5f);
        }

        // ═══════════════════════════════════════
        // 더티 청크 리메싱
        // ═══════════════════════════════════════

        /// <summary>모든 더티 청크를 리메싱. Update 또는 블록 변경 후 호출.</summary>
        public void RebuildDirtyChunks()
        {
            foreach (var kvp in _chunks)
            {
                if (!kvp.Value.IsDirty) continue;

                Mesh mesh = ChunkMesher.GenerateMesh(kvp.Value, GetBlock);

                if (_renderers.TryGetValue(kvp.Key, out ChunkRenderer renderer))
                {
                    renderer.UpdateMesh(mesh);
                }
            }
        }

        private void MarkDirty(Vector3Int chunkCoord)
        {
            if (_chunks.TryGetValue(chunkCoord, out Chunk chunk))
                chunk.IsDirty = true;
        }

        // ═══════════════════════════════════════
        // 월드 생성
        // ═══════════════════════════════════════

        private void GenerateWorld()
        {
            float startTime = Time.realtimeSinceStartup;

            // 1. 모든 청크 생성 + 지형 데이터 채우기
            for (int cx = 0; cx < _worldSizeX; cx++)
            for (int cy = 0; cy < _worldSizeY; cy++)
            for (int cz = 0; cz < _worldSizeZ; cz++)
            {
                Vector3Int coord = new(cx, cy, cz);
                Chunk chunk = new Chunk(coord);
                GenerateChunkTerrain(chunk);
                _chunks[coord] = chunk;
            }

            // 2. 지형 위에 구조물 생성 (죽은 나무 기둥 등)
            GenerateStructures();

            // 3. 모든 청크 메싱 + 렌더러 생성
            foreach (var kvp in _chunks)
            {
                Mesh mesh = ChunkMesher.GenerateMesh(kvp.Value, GetBlock);

                GameObject go = new GameObject();
                go.transform.SetParent(transform);

                // MeshFilter, MeshRenderer, MeshCollider 추가
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                go.AddComponent<MeshCollider>();

                ChunkRenderer renderer = go.AddComponent<ChunkRenderer>();
                renderer.Initialize(kvp.Value, _chunkMaterial);
                renderer.UpdateMesh(mesh);

                _renderers[kvp.Key] = renderer;
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log($"[VoxelWorld] Generated {_chunks.Count} chunks in {elapsed:F2}s " +
                      $"({_worldSizeX * Chunk.SIZE}×{_worldSizeY * Chunk.SIZE}×{_worldSizeZ * Chunk.SIZE} blocks)");
        }

        private void GenerateChunkTerrain(Chunk chunk)
        {
            float seedOffset = _seed * 0.1f;

            for (int lx = 0; lx < Chunk.SIZE; lx++)
            for (int lz = 0; lz < Chunk.SIZE; lz++)
            {
                int wx = chunk.Coord.x * Chunk.SIZE + lx;
                int wz = chunk.Coord.z * Chunk.SIZE + lz;

                // 2옥타브 펄린 노이즈로 지형 높이 결정
                float n1 = Mathf.PerlinNoise(
                    (wx + seedOffset) * _noiseScale,
                    (wz + seedOffset) * _noiseScale);
                float n2 = Mathf.PerlinNoise(
                    (wx + seedOffset) * _noiseScale * 2f + 100f,
                    (wz + seedOffset) * _noiseScale * 2f + 100f) * 0.5f;

                float height = (n1 + n2) * _terrainHeight + _terrainBase;
                int iHeight = Mathf.RoundToInt(height);

                for (int ly = 0; ly < Chunk.SIZE; ly++)
                {
                    int wy = chunk.Coord.y * Chunk.SIZE + ly;
                    BlockType type;

                    if (wy > iHeight)
                    {
                        type = BlockType.Air;
                    }
                    else if (wy == iHeight)
                    {
                        type = BlockType.Dirt; // 표면
                    }
                    else if (wy > iHeight - 4)
                    {
                        type = BlockType.Dirt; // 흙 레이어
                    }
                    else
                    {
                        type = BlockType.Stone;

                        // 광석 분포 (3D 노이즈)
                        float oreNoise = Mathf.PerlinNoise(
                            (wx + seedOffset) * 0.1f,
                            (wy * 0.1f + wz * 0.1f + seedOffset));

                        if (oreNoise > 0.82f && wy < 25)
                            type = BlockType.CopperOre;
                        else if (oreNoise > 0.88f && wy < 18)
                            type = BlockType.IronOre;
                    }

                    chunk.SetBlock(lx, ly, lz, type);
                }
            }

            chunk.IsDirty = true;
        }

        /// <summary>지형 위에 죽은 나무 기둥, 버섯 등을 배치.</summary>
        private void GenerateStructures()
        {
            float seedOffset = _seed * 0.3f;
            int totalX = _worldSizeX * Chunk.SIZE;
            int totalZ = _worldSizeZ * Chunk.SIZE;

            for (int wx = 0; wx < totalX; wx++)
            for (int wz = 0; wz < totalZ; wz++)
            {
                // 죽은 나무 기둥 (희귀)
                float treeNoise = Mathf.PerlinNoise(
                    (wx + seedOffset) * 0.3f,
                    (wz + seedOffset) * 0.3f);

                if (treeNoise > 0.90f)
                {
                    int surfaceY = GetSurfaceY(wx, wz);
                    if (surfaceY < 0) continue;

                    // 2~4블록 높이의 죽은 나무
                    int treeHeight = 2 + Mathf.FloorToInt((treeNoise - 0.90f) * 30f);
                    treeHeight = Mathf.Clamp(treeHeight, 2, 5);

                    for (int dy = 1; dy <= treeHeight; dy++)
                    {
                        SetBlock(wx, surfaceY + dy, wz, BlockType.DeadWood);
                    }
                }

                // 버섯 (보통 빈도)
                float mushNoise = Mathf.PerlinNoise(
                    (wx + seedOffset + 500f) * 0.15f,
                    (wz + seedOffset + 500f) * 0.15f);

                if (mushNoise > 0.85f)
                {
                    int surfaceY = GetSurfaceY(wx, wz);
                    if (surfaceY < 0) continue;

                    SetBlock(wx, surfaceY + 1, wz, BlockType.MushroomBlock);
                }
            }
        }

        /// <summary>특정 XZ 좌표의 지표면 Y를 반환. 못 찾으면 -1.</summary>
        public int GetSurfaceY(int wx, int wz)
        {
            int maxY = _worldSizeY * Chunk.SIZE - 1;
            for (int wy = maxY; wy >= 0; wy--)
            {
                if (BlockData.IsSolid(GetBlock(wx, wy, wz)))
                    return wy;
            }
            return -1;
        }

        /// <summary>플레이어 스폰 위치 결정. 월드 중앙 부근의 지표면.</summary>
        public Vector3 GetSpawnPosition()
        {
            int cx = (_worldSizeX * Chunk.SIZE) / 2;
            int cz = (_worldSizeZ * Chunk.SIZE) / 2;
            int surfaceY = GetSurfaceY(cx, cz);
            if (surfaceY < 0) surfaceY = 30;
            return new Vector3(cx + 0.5f, surfaceY + 2f, cz + 0.5f);
        }
    }
}
