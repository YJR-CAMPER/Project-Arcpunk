// ── GhoulSpawner.cs ──
// 구울 스폰 관리자.
// WaveDirector에서 호출하는 방향성 스폰 + 보스 스폰 지원.
// 기존 자극 기반 랜덤 스폰도 유지 (비전투 시간 배회 구울용).

using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Weather;
using Arcpunk.Player;

namespace Arcpunk.Ghoul
{
    public enum BossType { Mini, Mega }

    public class GhoulSpawner : MonoBehaviour
    {
        public static GhoulSpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private int _maxGhouls = 60;     // 호드용으로 상향
        [SerializeField] private float _spawnRadius = 40f;
        [SerializeField] private float _minSpawnDist = 20f;
        [SerializeField] private int _ticksPerSpawnCheck = 4;

        [Header("Ambient Spawning (비전투 시)")]
        [SerializeField] private float _ambientSpawnChance = 0.1f;  // Calm 중 소수 배회 구울
        [SerializeField] private int _maxAmbientGhouls = 5;

        [Header("Ghoul Visual")]
        [SerializeField] private Color _ghoulColor = new Color(0.7f, 0.15f, 0.1f);
        [SerializeField] private Color _miniBossColor = new Color(0.5f, 0.1f, 0.4f);
        [SerializeField] private Color _megaBossColor = new Color(0.2f, 0.05f, 0.1f);

        [Header("Ghoul Model")]
        [SerializeField] private GameObject _ghoulPrefab;      // 일반 구울 (Mixamo Zombie)
        [SerializeField] private GameObject _miniBossPrefab;   // 소형 보스 (없으면 일반 구울 스케일업)
        [SerializeField] private GameObject _megaBossPrefab;   // 대형 보스 (Mixamo Mutant 추천)

        [Header("Boss Stats")]
        [SerializeField] private float _miniBossHP = 200f;
        [SerializeField] private float _miniBossSpeed = 2.0f;
        [SerializeField] private float _miniBossDamage = 20f;
        [SerializeField] private float _miniBossScale = 1.3f;
        [SerializeField] private float _miniBossBlockDamage = 15f;

        [SerializeField] private float _megaBossHP = 500f;
        [SerializeField] private float _megaBossSpeed = 1.5f;
        [SerializeField] private float _megaBossDamage = 40f;
        [SerializeField] private float _megaBossScale = 2.0f;
        [SerializeField] private float _megaBossBlockDamage = 30f;

        private List<SimpleGhoul> _activeGhouls = new();
        private int _tickCounter;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick += OnTick;
        }

        private void OnDestroy()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick -= OnTick;
        }

        private void OnTick(int tickNumber)
        {
            // 죽은 구울 정리
            _activeGhouls.RemoveAll(g => g == null || !g.IsAlive);

            _tickCounter++;
            if (_tickCounter < _ticksPerSpawnCheck) return;
            _tickCounter = 0;

            // WaveDirector가 활성이면 ambient 스폰 안 함
            var waveDir = WaveDirector.Instance;
            if (waveDir != null && waveDir.IsWaveActive) return;

            // 비전투 중 소수 배회 구울 스폰
            int ambientCount = 0;
            foreach (var g in _activeGhouls)
                if (g != null && g.IsAlive) ambientCount++;

            if (ambientCount >= _maxAmbientGhouls) return;
            if (Random.value > _ambientSpawnChance) return;

            TryAmbientSpawn();
        }

        // ═══════════════════════════════════════
        // WaveDirector용 공개 스폰 API
        // ═══════════════════════════════════════

        /// <summary>지정 위치에 일반 구울 스폰 (방향성 호드용).</summary>
        public void SpawnDirectional(Vector3 position, float healthMult, float speedMult, bool canBreakBlocks)
        {
            if (_activeGhouls.Count >= _maxGhouls) return;

            var ghoul = CreateGhoul(position, _ghoulColor, 0.7f);
            if (ghoul == null) return;

            ghoul.MaxHealth = 50f * healthMult;
            ghoul.Health = ghoul.MaxHealth;
            ghoul.MoveSpeed = 2.5f * speedMult;
            ghoul.CanBreakBlocks = canBreakBlocks;

            // 호드 구울은 플레이어 기지를 향해 바로 추적
            ghoul.ForceChasePlayer();

            _activeGhouls.Add(ghoul);
        }

        /// <summary>보스 구울 스폰.</summary>
        public void SpawnBoss(Vector3 position, BossType type)
        {
            if (_activeGhouls.Count >= _maxGhouls) return;

            Color color;
            float scale, hp, speed, damage, blockDmg;
            GameObject prefab;

            if (type == BossType.Mini)
            {
                color = _miniBossColor;
                scale = _miniBossScale;
                hp = _miniBossHP;
                speed = _miniBossSpeed;
                damage = _miniBossDamage;
                blockDmg = _miniBossBlockDamage;
                prefab = _miniBossPrefab != null ? _miniBossPrefab : _ghoulPrefab;
            }
            else
            {
                color = _megaBossColor;
                scale = _megaBossScale;
                hp = _megaBossHP;
                speed = _megaBossSpeed;
                damage = _megaBossDamage;
                blockDmg = _megaBossBlockDamage;
                prefab = _megaBossPrefab != null ? _megaBossPrefab : _ghoulPrefab;
            }

            var ghoul = CreateGhoul(position, color, scale, prefab);
            if (ghoul == null) return;

            ghoul.MaxHealth = hp;
            ghoul.Health = hp;
            ghoul.MoveSpeed = speed;
            ghoul.Damage = damage;
            ghoul.CanBreakBlocks = true;
            ghoul.BlockDamage = blockDmg;
            ghoul.IsBoss = true;

            ghoul.ForceChasePlayer();

            _activeGhouls.Add(ghoul);

            Debug.Log($"[GhoulSpawner] {type} Boss spawned at {position}! HP={hp}");
        }

        // ═══════════════════════════════════════
        // 내부 스폰 로직
        // ═══════════════════════════════════════

        private void TryAmbientSpawn()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 rndDir = Random.insideUnitCircle.normalized;
                float rndDist = Random.Range(_minSpawnDist, _spawnRadius);
                float testX = playerPos.x + rndDir.x * rndDist;
                float testZ = playerPos.z + rndDir.y * rndDist;

                int ix = Mathf.FloorToInt(testX);
                int iz = Mathf.FloorToInt(testZ);

                var world = VoxelWorld.Instance;
                int maxX = world.WorldSizeX * Chunk.SIZE;
                int maxZ = world.WorldSizeZ * Chunk.SIZE;
                if (ix < 0 || ix >= maxX || iz < 0 || iz >= maxZ)
                    continue;

                int surfaceY = world.GetSurfaceY(ix, iz);
                if (surfaceY < 0) continue;

                Vector3 spawnPos = new Vector3(ix + 0.5f, surfaceY + 2f, iz + 0.5f);

                if (world.GetBlock(ix, surfaceY + 1, iz) != BlockType.Air ||
                    world.GetBlock(ix, surfaceY + 2, iz) != BlockType.Air)
                    continue;

                var ghoul = CreateGhoul(spawnPos, _ghoulColor, 0.7f);
                if (ghoul != null)
                    _activeGhouls.Add(ghoul);

                break;
            }
        }

        private SimpleGhoul CreateGhoul(Vector3 position, Color color, float scale)
        {
            return CreateGhoul(position, color, scale, _ghoulPrefab);
        }

        private SimpleGhoul CreateGhoul(Vector3 position, Color color, float scale, GameObject prefab)
        {
            GameObject ghoulObj;
            if (prefab != null)
                ghoulObj = Instantiate(prefab);
            else
                ghoulObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);

            ghoulObj.name = $"Ghoul_{_activeGhouls.Count}";
            ghoulObj.transform.position = position;
            ghoulObj.transform.localScale = Vector3.one * scale;

            // 기존 Collider 제거 (CharacterController가 대체)
            foreach (var col in ghoulObj.GetComponentsInChildren<Collider>())
                Destroy(col);

            // 렌더러: 자식 포함 전체에서 찾아서 색상 적용
            var renderers = ghoulObj.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                // 프리팹에 이미 머티리얼이 있으면 유지, 없으면 생성
                if (rend.material == null)
                    rend.material = new Material(Shader.Find("Standard"));

                // Emission만 추가 (기존 텍스처 유지)
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", color * 0.2f);
            }

            // 레이어: 자식 포함 전체에 적용
            int ghoulLayer = LayerMask.NameToLayer("Ghoul");
            SetLayerRecursive(ghoulObj, ghoulLayer);

            var ghoul = ghoulObj.AddComponent<SimpleGhoul>();

            // Animator가 있는 프리팹이면 GhoulAnimator 자동 부착
            if (ghoulObj.GetComponentInChildren<Animator>() != null)
                ghoulObj.AddComponent<GhoulAnimator>();

            return ghoul;
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        public int ActiveGhoulCount => _activeGhouls.Count;

        public void KillAll()
        {
            foreach (var g in _activeGhouls)
            {
                if (g != null)
                    g.TakeDamage(9999);
            }
            _activeGhouls.Clear();
        }
    }
}