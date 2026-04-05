// ── GhoulSpawner.cs ──
// 서지 레벨에 따라 구울을 스폰.
// 자극 핫스팟(피뢰침, 조명) 근처에 우선 스폰.
// TickSystem에 구독하여 주기적으로 스폰 판정.

using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Weather;
using Arcpunk.Player;

namespace Arcpunk.Ghoul
{

    public class GhoulSpawner : MonoBehaviour
    {
        public static GhoulSpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private int _maxGhouls = 20;
        [SerializeField] private float _spawnRadius = 40f;    // 플레이어 주변 스폰 범위
        [SerializeField] private float _minSpawnDist = 20f;   // 최소 거리 (너무 가까이 안 나오게)
        [SerializeField] private int _ticksPerSpawnCheck = 4;  // 몇 틱마다 스폰 체크

        [Header("Surge Scaling")]
        [SerializeField] private float _spawnChanceNone = 0f;     // Calm: 스폰 안 함
        [SerializeField] private float _spawnChanceSurge1 = 0.3f;
        [SerializeField] private float _spawnChanceSurge2 = 0.6f;

        [Header("Ghoul Visual")]
        [SerializeField] private Color _ghoulColor = new Color(0.7f, 0.15f, 0.1f);

        [Header("Ghoul Model")]
        [SerializeField] private GameObject _ghoulPrefab; // Inspector에서 FBX 드래그

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

            // 최대 수 체크
            if (_activeGhouls.Count >= _maxGhouls) return;

            // 현재 스폰 확률
            var weather = WeatherSystem.Instance?.CurrentWeather
                ?? new WeatherState();
            float spawnChance = weather.SurgeLevel switch
            {
                SurgeLevel.Surge1 => _spawnChanceSurge1,
                SurgeLevel.Surge2 => _spawnChanceSurge2,
                _ => _spawnChanceNone,
            };

            if (Random.value > spawnChance) return;

            // 스폰 시도
            TrySpawn();
        }

        private void TrySpawn()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            // 스폰 위치 결정: 플레이어 주변 랜덤
            Vector3 spawnPos = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                // 랜덤 방향 + 거리
                Vector2 rndDir = Random.insideUnitCircle.normalized;
                float rndDist = Random.Range(_minSpawnDist, _spawnRadius);
                float testX = playerPos.x + rndDir.x * rndDist;
                float testZ = playerPos.z + rndDir.y * rndDist;

                int ix = Mathf.FloorToInt(testX);
                int iz = Mathf.FloorToInt(testZ);

                // 월드 범위 체크
                var world = VoxelWorld.Instance;
                int maxX = world.WorldSizeX * Chunk.SIZE;
                int maxZ = world.WorldSizeZ * Chunk.SIZE;
                if (ix < 0 || ix >= maxX || iz < 0 || iz >= maxZ)
                    continue;

                // 지표면 찾기
                int surfaceY = world.GetSurfaceY(ix, iz);
                if (surfaceY < 0) continue;

                // 스폰 위치: 지표면 위 2블록
                spawnPos = new Vector3(ix + 0.5f, surfaceY + 2f, iz + 0.5f);

                // 머리 위가 공기인지 확인
                if (world.GetBlock(ix, surfaceY + 1, iz) != BlockType.Air ||
                    world.GetBlock(ix, surfaceY + 2, iz) != BlockType.Air)
                    continue;

                found = true;
                break;
            }

            if (!found) return;

            // 구울 생성
            SpawnGhoul(spawnPos);
        }

        private void SpawnGhoul(Vector3 position)
        {
            // 캡슐 프리미티브로 구울 생성
            GameObject ghoulObj;
            if (_ghoulPrefab != null)
                ghoulObj = Instantiate(_ghoulPrefab);
            else
                ghoulObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghoulObj.name = $"Ghoul_{_activeGhouls.Count}";
            ghoulObj.transform.position = position;
            ghoulObj.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            // 기본 Collider 제거 (CharacterController가 충돌 처리)
            var defaultCollider = ghoulObj.GetComponent<CapsuleCollider>();
            if (defaultCollider != null)
                Destroy(defaultCollider);

            // 색상
            var renderer = ghoulObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = _ghoulColor;
                // 약간의 Emission (어둠 속에서 눈 빛나는 느낌)
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", _ghoulColor * 0.3f);
            }

            // 레이어 (Raycast 구분용)
            ghoulObj.layer = LayerMask.NameToLayer("Ghoul");

            // 구울 컴포넌트 추가
            var ghoul = ghoulObj.AddComponent<SimpleGhoul>();

            // 서지 레벨에 따라 스탯 보정
            var weather = WeatherSystem.Instance?.CurrentWeather
                ?? new WeatherState();
            if (weather.SurgeLevel == SurgeLevel.Surge2)
            {
                ghoul.MaxHealth = 70f;
                ghoul.MoveSpeed = 3.2f;
                ghoul.Damage = 12f;
            }

            _activeGhouls.Add(ghoul);
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        public int ActiveGhoulCount => _activeGhouls.Count;

        /// <summary>모든 구울 제거 (디버그용).</summary>
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
