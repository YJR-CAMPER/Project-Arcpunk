// ── WaveDirector.cs ──
// They Are Billions 스타일 호드 웨이브 관리자.
// WeatherSystem의 상태 머신과 연동:
//   Calm     → 건설/준비 시간
//   Building → 웨이브 경고 + 마지막 준비
//   Storm    → 호드 돌격
//   Subsiding → 잔여 처치
//
// 웨이브가 진행될수록 구울 수, 방향 수, 보스 등장이 스케일링.

using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Weather;
using Arcpunk.Voxel;

namespace Arcpunk.Ghoul
{
    /// <summary>호드가 접근하는 방향.</summary>
    public enum HordeDirection { North, South, East, West }

    /// <summary>한 웨이브의 공격 계획.</summary>
    [System.Serializable]
    public struct WavePlan
    {
        public int WaveNumber;
        public int GhoulCount;
        public List<HordeDirection> Directions;   // 공격 방향들
        public int MiniBossCount;
        public bool HasMegaBoss;
        public float GhoulSpeedMult;              // 속도 배율
        public float GhoulHealthMult;             // 체력 배율
    }

    public class WaveDirector : MonoBehaviour
    {
        public static WaveDirector Instance { get; private set; }

        [Header("Wave Scaling")]
        [SerializeField] private int _baseGhoulCount = 8;         // 1웨이브 기본 구울 수
        [SerializeField] private float _ghoulCountMult = 1.5f;    // 웨이브당 곱 증가
        [SerializeField] private int _maxGhoulsPerWave = 80;
        [SerializeField] private int _bossStartWave = 7;          // 보스 등장 시작 웨이브
        [SerializeField] private int _megaBossStartWave = 10;     // 대형 보스 등장 시작
        [SerializeField] private int _multiDirStartWave = 4;      // 다방향 공격 시작

        [Header("Spawn Timing")]
        [SerializeField] private float _spawnInterval = 0.5f;     // 구울 간 스폰 간격 (초)
        [SerializeField] private float _bossSpawnDelay = 5f;      // 보스는 일반 구울 후 등장

        [Header("Spawn Distance")]
        [SerializeField] private float _spawnDistFromEdge = 5f;   // 월드 가장자리에서 몇 블록 안쪽

        // ── 런타임 상태 ──
        public int CurrentWave { get; private set; }
        public WavePlan? CurrentPlan { get; private set; }
        public bool IsWaveActive { get; private set; }
        public List<HordeDirection> IncomingDirections => _currentDirections;

        private List<HordeDirection> _currentDirections = new();
        private float _spawnTimer;
        private int _spawnedThisWave;
        private int _bossesSpawnedThisWave;
        private bool _bossPhase;
        private float _bossDelayTimer;
        private WeatherPhase _lastPhase = WeatherPhase.Calm;

        // ── 이벤트 ──
        public event System.Action<WavePlan> OnWaveAnnounced;   // Building 진입 시
        public event System.Action<int> OnWaveStarted;          // Storm 진입 시
        public event System.Action<int> OnWaveCompleted;        // 모든 구울 처치 시

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            var weather = WeatherSystem.Instance;
            if (weather == null) return;

            WeatherPhase currentPhase = weather.CurrentWeather.Phase;

            // 상태 전환 감지
            if (currentPhase != _lastPhase)
            {
                OnPhaseChanged(_lastPhase, currentPhase);
                _lastPhase = currentPhase;
            }

            // Storm 중 스폰 처리
            if (IsWaveActive && currentPhase == WeatherPhase.Storm)
            {
                UpdateSpawning();
            }

            // 웨이브 완료 체크 (Storm 또는 Subsiding 중)
            if (IsWaveActive && _spawnedThisWave >= GetCurrentGhoulTarget() && !_bossPhase)
            {
                // 모든 구울이 스폰됨 — 남은 구울이 전멸하면 웨이브 완료
                int alive = GhoulSpawner.Instance?.ActiveGhoulCount ?? 0;
                if (alive == 0)
                {
                    CompleteWave();
                }
            }
        }

        // ═══════════════════════════════════════
        // 날씨 상태 전환 핸들러
        // ═══════════════════════════════════════

        private void OnPhaseChanged(WeatherPhase from, WeatherPhase to)
        {
            switch (to)
            {
                case WeatherPhase.Building:
                    // 다음 웨이브 계획 수립 + 경고
                    CurrentWave++;
                    var plan = GenerateWavePlan(CurrentWave);
                    CurrentPlan = plan;
                    _currentDirections = plan.Directions;

                    Debug.Log($"[WaveDirector] Wave {CurrentWave} incoming from {string.Join(", ", plan.Directions)} " +
                              $"| {plan.GhoulCount} ghouls | {plan.MiniBossCount} mini-bosses | mega={plan.HasMegaBoss}");

                    OnWaveAnnounced?.Invoke(plan);
                    break;

                case WeatherPhase.Storm:
                    // 호드 돌격 시작
                    StartWave();
                    break;

                case WeatherPhase.Calm:
                    // 전투 종료 — 아직 웨이브가 활성이면 강제 완료
                    if (IsWaveActive)
                        CompleteWave();
                    break;
            }
        }

        // ═══════════════════════════════════════
        // 웨이브 계획 생성
        // ═══════════════════════════════════════

        private WavePlan GenerateWavePlan(int wave)
        {
            var plan = new WavePlan();
            plan.WaveNumber = wave;

            // 구울 수 스케일링
            plan.GhoulCount = Mathf.Min(
                Mathf.RoundToInt(_baseGhoulCount * Mathf.Pow(_ghoulCountMult, wave - 1)),
                _maxGhoulsPerWave);

            // 스탯 스케일링
            plan.GhoulHealthMult = 1f + (wave - 1) * 0.1f;    // 웨이브당 +10% 체력
            plan.GhoulSpeedMult = 1f + (wave - 1) * 0.05f;    // 웨이브당 +5% 속도
            plan.GhoulSpeedMult = Mathf.Min(plan.GhoulSpeedMult, 1.8f); // 속도 캡

            // 공격 방향
            plan.Directions = PickDirections(wave);

            // 보스
            if (wave >= _bossStartWave)
                plan.MiniBossCount = 1 + (wave - _bossStartWave) / 3;
            if (wave >= _megaBossStartWave)
                plan.HasMegaBoss = true;

            return plan;
        }

        private List<HordeDirection> PickDirections(int wave)
        {
            var all = new List<HordeDirection> {
                HordeDirection.North, HordeDirection.South,
                HordeDirection.East, HordeDirection.West
            };

            // 셔플
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }

            int dirCount = 1;
            if (wave >= _multiDirStartWave) dirCount = 2;
            if (wave >= _multiDirStartWave + 4) dirCount = 3;

            return all.GetRange(0, Mathf.Min(dirCount, all.Count));
        }

        // ═══════════════════════════════════════
        // 웨이브 실행
        // ═══════════════════════════════════════

        private void StartWave()
        {
            IsWaveActive = true;
            _spawnedThisWave = 0;
            _bossesSpawnedThisWave = 0;
            _bossPhase = false;
            _bossDelayTimer = 0f;
            _spawnTimer = 0f;

            OnWaveStarted?.Invoke(CurrentWave);
            Debug.Log($"[WaveDirector] Wave {CurrentWave} STARTED!");
        }

        private void UpdateSpawning()
        {
            if (!CurrentPlan.HasValue) return;
            var plan = CurrentPlan.Value;

            var spawner = GhoulSpawner.Instance;
            if (spawner == null) return;

            // 일반 구울 스폰
            if (_spawnedThisWave < plan.GhoulCount)
            {
                _spawnTimer -= Time.deltaTime;
                if (_spawnTimer <= 0)
                {
                    // 방향 순환 배정
                    var dir = plan.Directions[_spawnedThisWave % plan.Directions.Count];
                    Vector3 spawnPos = GetEdgeSpawnPosition(dir);

                    if (spawnPos != Vector3.zero)
                    {
                        spawner.SpawnDirectional(spawnPos,
                            plan.GhoulHealthMult, plan.GhoulSpeedMult, false);
                        _spawnedThisWave++;
                    }

                    _spawnTimer = _spawnInterval;
                }
            }
            // 보스 스폰 (일반 구울 전부 스폰 후)
            else if (plan.MiniBossCount > 0 || plan.HasMegaBoss)
            {
                if (!_bossPhase)
                {
                    _bossPhase = true;
                    _bossDelayTimer = _bossSpawnDelay;
                }

                _bossDelayTimer -= Time.deltaTime;
                if (_bossDelayTimer <= 0)
                {
                    if (_bossesSpawnedThisWave < plan.MiniBossCount)
                    {
                        // 소형 보스
                        var dir = plan.Directions[_bossesSpawnedThisWave % plan.Directions.Count];
                        Vector3 pos = GetEdgeSpawnPosition(dir);
                        if (pos != Vector3.zero)
                        {
                            spawner.SpawnBoss(pos, BossType.Mini);
                            _bossesSpawnedThisWave++;
                        }
                        _bossDelayTimer = 2f; // 보스 간 간격
                    }
                    else if (plan.HasMegaBoss && _bossesSpawnedThisWave == plan.MiniBossCount)
                    {
                        // 대형 보스
                        var dir = plan.Directions[0];
                        Vector3 pos = GetEdgeSpawnPosition(dir);
                        if (pos != Vector3.zero)
                        {
                            spawner.SpawnBoss(pos, BossType.Mega);
                            _bossesSpawnedThisWave++;
                        }
                    }
                }
            }
        }

        private int GetCurrentGhoulTarget()
        {
            if (!CurrentPlan.HasValue) return 0;
            return CurrentPlan.Value.GhoulCount + CurrentPlan.Value.MiniBossCount
                   + (CurrentPlan.Value.HasMegaBoss ? 1 : 0);
        }

        private void CompleteWave()
        {
            IsWaveActive = false;
            CurrentPlan = null;
            _currentDirections.Clear();

            Debug.Log($"[WaveDirector] Wave {CurrentWave} COMPLETED!");
            OnWaveCompleted?.Invoke(CurrentWave);
        }

        // ═══════════════════════════════════════
        // 방향별 스폰 위치 계산
        // ═══════════════════════════════════════

        private Vector3 GetEdgeSpawnPosition(HordeDirection dir)
        {
            var world = VoxelWorld.Instance;
            if (world == null) return Vector3.zero;

            int maxX = world.WorldSizeX * Chunk.SIZE;
            int maxZ = world.WorldSizeZ * Chunk.SIZE;
            int dist = Mathf.RoundToInt(_spawnDistFromEdge);

            int wx, wz;

            switch (dir)
            {
                case HordeDirection.North:
                    wx = Random.Range(dist, maxX - dist);
                    wz = maxZ - dist;
                    break;
                case HordeDirection.South:
                    wx = Random.Range(dist, maxX - dist);
                    wz = dist;
                    break;
                case HordeDirection.East:
                    wx = maxX - dist;
                    wz = Random.Range(dist, maxZ - dist);
                    break;
                case HordeDirection.West:
                    wx = dist;
                    wz = Random.Range(dist, maxZ - dist);
                    break;
                default:
                    wx = maxX / 2;
                    wz = maxZ / 2;
                    break;
            }

            int surfaceY = world.GetSurfaceY(wx, wz);
            if (surfaceY < 0) return Vector3.zero;

            return new Vector3(wx + 0.5f, surfaceY + 2f, wz + 0.5f);
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        /// <summary>디버그: 즉시 다음 웨이브 강제 시작.</summary>
        public void ForceNextWave()
        {
            CurrentWave++;
            var plan = GenerateWavePlan(CurrentWave);
            CurrentPlan = plan;
            _currentDirections = plan.Directions;
            StartWave();
        }
    }
}
