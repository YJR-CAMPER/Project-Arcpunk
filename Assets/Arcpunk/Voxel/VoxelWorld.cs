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
        [SerializeField] private int _seed = 42;

        [Header("Cave Generation (Spaghetti)")]
        [SerializeField] private float _caveFreq = 0.035f;        // 동굴 주파수 (작을수록 넓고 긴 터널)
        [SerializeField] private float _caveWidth = 0.06f;        // 터널 두께 (클수록 넓은 동굴)
        [SerializeField] private int _caveMinY = 2;               // 동굴 최소 Y (바닥 뚫림 방지)
        [SerializeField] private int _caveSurfaceGap = 4;         // 지표면에서 이 깊이 아래부터만 동굴 생성
        [SerializeField] private float _entranceChance = 0.92f;   // 입구 노이즈 임계값 (높을수록 입구 희귀)

        [Header("References")]
        [SerializeField] private Material _chunkMaterial; // 텍스처 아틀라스 머티리얼
        [SerializeField] private Material _crossMaterial; // X자 빌보드 머티리얼

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

            // 큐에 먼저 추가 (SetBlock이 IsDirty를 설정하기 전에)
            if (!chunk.IsDirty)
                _dirtyQueue.Enqueue(chunkCoord);

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

        [Header("Optimization")]
        [SerializeField] private int _maxMeshPerFrame = 4;             // 프레임당 메시 갱신 수
        [SerializeField] private int _maxColliderPerFrame = 1;         // 프레임당 콜라이더 갱신 수
        [SerializeField] private float _nearDistance = 32f;            // 이 거리 안 = 즉시 콜라이더

        // 더티 청크 큐 (전체 딕셔너리 스캔 대신)
        private System.Collections.Generic.Queue<Vector3Int> _dirtyQueue = new();
        private System.Collections.Generic.Queue<Vector3Int> _colliderQueue = new();

        private void LateUpdate()
        {
            ProcessDirtyChunks();
            ProcessColliderQueue();
        }

        /// <summary>프레임당 제한된 수의 더티 청크만 메시 갱신.</summary>
        private void ProcessDirtyChunks()
        {
            int rebuilt = 0;
            while (_dirtyQueue.Count > 0 && rebuilt < _maxMeshPerFrame)
            {
                var coord = _dirtyQueue.Dequeue();
                if (!_chunks.TryGetValue(coord, out Chunk chunk)) continue;
                if (!chunk.IsDirty) continue;

                Mesh mesh = ChunkMesher.GenerateMesh(chunk, GetBlock);

                if (_renderers.TryGetValue(coord, out ChunkRenderer renderer))
                {
                    renderer.UpdateMeshOnly(mesh);

                    // 플레이어 근처면 콜라이더 즉시, 아니면 큐에
                    if (IsNearPlayer(coord))
                        renderer.UpdateCollider();
                    else
                        _colliderQueue.Enqueue(coord);
                }

                rebuilt++;
            }
        }

        /// <summary>먼 청크의 콜라이더를 프레임 분산 갱신.</summary>
        private void ProcessColliderQueue()
        {
            int updated = 0;
            while (_colliderQueue.Count > 0 && updated < _maxColliderPerFrame)
            {
                var coord = _colliderQueue.Dequeue();
                if (_renderers.TryGetValue(coord, out ChunkRenderer renderer))
                    renderer.UpdateCollider();
                updated++;
            }
        }

        private bool IsNearPlayer(Vector3Int chunkCoord)
        {
            var player = Player.PlayerController.Instance;
            if (player == null) return true;

            Vector3 chunkCenter = new Vector3(
                (chunkCoord.x + 0.5f) * Chunk.SIZE,
                (chunkCoord.y + 0.5f) * Chunk.SIZE,
                (chunkCoord.z + 0.5f) * Chunk.SIZE);

            return Vector3.Distance(player.transform.position, chunkCenter) < _nearDistance;
        }

        /// <summary>모든 더티 청크를 리메싱. 초기 로드용 (한번에).</summary>
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
            {
                if (!chunk.IsDirty) // 이미 큐에 있으면 중복 추가 방지
                    _dirtyQueue.Enqueue(chunkCoord);
                chunk.IsDirty = true;
            }
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

                // 서브메시 2개 → 머티리얼 2개 (블록 + 크로스)
                var meshRenderer = go.GetComponent<MeshRenderer>();
                if (meshRenderer != null && _crossMaterial != null)
                    meshRenderer.materials = new[] { _chunkMaterial, _crossMaterial };

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

                // ── 바이옴 결정 (블렌딩 포함) ──
                BiomeParams biome = BiomeData.SampleBlended(wx, wz, seedOffset);

                // ── 멀티 옥타브 높이맵 (바이옴별 파라미터) ──
                float heightNoise = 0f;
                float amp = 1f;
                float freq = biome.NoiseFreq;
                float maxAmp = 0f;

                for (int o = 0; o < biome.Octaves; o++)
                {
                    heightNoise += Mathf.PerlinNoise(
                        (wx + seedOffset) * freq + o * 100f,
                        (wz + seedOffset) * freq + o * 100f) * amp;
                    maxAmp += amp;
                    amp *= biome.Persistence;
                    freq *= 2f;
                }
                heightNoise /= maxAmp; // 0~1 정규화

                float height = heightNoise * biome.HeightScale + biome.BaseHeight;
                int iHeight = Mathf.RoundToInt(height);

                // ── 블록 채우기 ──
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
                        type = biome.Surface; // 바이옴별 표면 블록
                    }
                    else if (wy > iHeight - 4)
                    {
                        type = biome.Subsurface; // 바이옴별 지표 아래 블록
                    }
                    else
                    {
                        type = BlockType.Stone;

                        // 광석 분포 (바이옴별 구리 보너스 적용)
                        float oreNoise = Mathf.PerlinNoise(
                            (wx + seedOffset) * 0.1f,
                            (wy * 0.1f + wz * 0.1f + seedOffset));

                        if (oreNoise > (0.82f - biome.CopperBonus) && wy < 25)
                            type = BlockType.CopperOre;
                        else if (oreNoise > 0.88f && wy < 18)
                            type = BlockType.IronOre;
                    }

                    chunk.SetBlock(lx, ly, lz, type);
                }

                // ──────────────────────────────────
                // 동굴 카빙 패스 (Spaghetti Cave)
                // 3D 퍼린 노이즈 두 개의 절대값이 동시에 0에 가까울 때
                // → 두 "등위면"이 교차하는 곳 = 긴 터널 형태의 동굴
                // ──────────────────────────────────

                // 입구 판정: 이 XZ 위치가 동굴 입구 후보인지 결정
                // 입구 후보 지점에서는 surfaceGap을 0으로 → 동굴이 지표까지 관통 가능
                float entranceNoise = Mathf.PerlinNoise(
                    (wx + seedOffset + 777f) * 0.08f,
                    (wz + seedOffset + 777f) * 0.08f);
                bool isEntrance = entranceNoise > _entranceChance;
                int surfaceGap = isEntrance ? 0 : _caveSurfaceGap;

                for (int ly = 0; ly < Chunk.SIZE; ly++)
                {
                    int wy = chunk.Coord.y * Chunk.SIZE + ly;

                    // 바닥 보호는 항상 적용, 지표면 보호는 입구 여부에 따라
                    if (wy <= _caveMinY || wy >= iHeight - surfaceGap)
                        continue;

                    // 이미 Air면 스킵
                    if (chunk.GetBlock(lx, ly, lz) == BlockType.Air)
                        continue;

                    // 노이즈 좌표 (시드 오프셋으로 분리)
                    float nx = (wx + seedOffset) * _caveFreq;
                    float ny = wy * _caveFreq * 0.7f;  // Y를 약간 압축 → 수평 터널 경향
                    float nz = (wz + seedOffset) * _caveFreq;

                    // 두 개의 독립적인 3D 퍼린 노이즈 (-1 ~ +1 범위)
                    float caveA = Noise3D.Perlin(nx, ny, nz);
                    float caveB = Noise3D.Perlin(nx + 500f, ny + 500f, nz + 500f);

                    // 스파게티 동굴 핵심:
                    // |caveA|과 |caveB|가 동시에 작을 때 = 두 등위면의 교차점 = 터널
                    float t1 = Mathf.Abs(caveA);
                    float t2 = Mathf.Abs(caveB);

                    // 깊이 보너스: 깊을수록 동굴이 약간 넓어짐 (탐험 보상)
                    float depthRatio = 1f - ((float)wy / iHeight);
                    float width = _caveWidth * (1f + depthRatio * 0.5f);

                    // 입구 지점에서는 지표면 근처 동굴을 약간 넓혀 자연스러운 개구부 형성
                    if (isEntrance && wy > iHeight - 6)
                        width *= 1.4f;

                    if (t1 < width && t2 < width)
                    {
                        chunk.SetBlock(lx, ly, lz, BlockType.Air);
                    }
                }
            }

            chunk.IsDirty = true;
        }

        /// <summary>지형 위에 죽은 나무 기둥, 버섯 등을 바이옴별 밀도로 배치.</summary>
        private void GenerateStructures()
        {
            float seedOffset = _seed * 0.3f;
            float biomeOffset = _seed * 0.1f;
            int totalX = _worldSizeX * Chunk.SIZE;
            int totalZ = _worldSizeZ * Chunk.SIZE;

            for (int wx = 0; wx < totalX; wx++)
            for (int wz = 0; wz < totalZ; wz++)
            {
                // 바이옴별 밀도 파라미터 조회
                BiomeType biomeType = BiomeData.SampleType(wx, wz, biomeOffset);
                BiomeParams biome = BiomeData.Get(biomeType);

                // 죽은 나무 기둥 (바이옴별 밀도)
                float treeNoise = Mathf.PerlinNoise(
                    (wx + seedOffset) * 0.3f,
                    (wz + seedOffset) * 0.3f);

                if (treeNoise > biome.TreeDensity)
                {
                    int surfaceY = GetSurfaceY(wx, wz);
                    if (surfaceY < 0) continue;

                    // 위치 기반 시드로 같은 위치에 항상 같은 나무 생성
                    int treeSeed = wx * 73856093 ^ wz * 19349663 ^ _seed;
                    DeadTreeGenerator.Generate(SetBlock, wx, surfaceY, wz, treeSeed);
                }

                // 버섯 (바이옴별 밀도)
                float mushNoise = Mathf.PerlinNoise(
                    (wx + seedOffset + 500f) * 0.15f,
                    (wz + seedOffset + 500f) * 0.15f);

                if (mushNoise > biome.MushroomDensity)
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
