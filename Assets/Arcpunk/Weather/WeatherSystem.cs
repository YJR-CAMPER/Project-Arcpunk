// ── WeatherSystem.cs ──
// 날씨/서지 상태 머신. Calm → Building → Surge1 → Surge2 → Subsiding → Calm.
// 서지 레벨에 따라 발전 효율, 구울 스폰, 낙뢰 빈도가 변한다.
// TickSystem에 구독하여 매 틱마다 상태를 갱신.

using System;
using UnityEngine;

namespace Arcpunk.Weather
{
    public enum SurgeLevel
    {
        None,    // 평시
        Surge1,  // 보통 폭풍
        Surge2,  // 강한 폭풍
    }

    public enum WeatherPhase
    {
        Calm,       // 맑음 — 발전 거의 없음
        Building,   // 구름이 모이는 중
        Storm,      // 폭풍 진행 중 (서지 레벨에 따라 강도 변화)
        Subsiding,  // 폭풍이 약해지는 중
    }

    /// <summary>현재 날씨 상태의 스냅샷. 다른 시스템에서 읽기 전용으로 사용.</summary>
    [System.Serializable]
    public struct WeatherState
    {
        public WeatherPhase Phase;
        public SurgeLevel SurgeLevel;
        public float LightningChance;  // 틱당 낙뢰 확률 (0~1)
        public float SurgeMultiplier;  // 발전 배율
        public float Visibility;       // 0~1 (1=맑음, 0=폭풍 한가운데)
        public bool IsStorming => Phase == WeatherPhase.Storm || Phase == WeatherPhase.Building;
    }

    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Header("Cycle Timing (in ticks)")]
        [SerializeField] private int _calmDuration = 120;       // 60초
        [SerializeField] private int _buildingDuration = 20;    // 10초
        [SerializeField] private int _stormDuration = 80;       // 40초
        [SerializeField] private int _subsidingDuration = 20;   // 10초

        [Header("Surge Progression")]
        [Tooltip("이 횟수만큼 폭풍이 반복되면 서지 레벨 상승")]
        [SerializeField] private int _stormsPerSurgeUp = 2;

        /// <summary>날씨 상태가 변할 때 발생.</summary>
        public event Action<WeatherState> OnWeatherChanged;

        /// <summary>낙뢰가 떨어져야 할 때 발생. LightningSystem이 구독.</summary>
        public event Action OnLightningRequested;

        public WeatherState CurrentWeather { get; private set; }

        private int _phaseTicksRemaining;
        private int _stormCount; // 서지 레벨 상승 추적용

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Calm 상태로 시작
            TransitionTo(WeatherPhase.Calm, SurgeLevel.None);

            // TickSystem에 구독
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick += OnTick;
            else
                Debug.LogError("[WeatherSystem] TickSystem not found!");
        }

        private void OnDestroy()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick -= OnTick;
        }

        private void OnTick(int tickNumber)
        {
            _phaseTicksRemaining--;

            // 폭풍 중 낙뢰 판정
            if (CurrentWeather.IsStorming)
            {
                if (UnityEngine.Random.value < CurrentWeather.LightningChance)
                {
                    OnLightningRequested?.Invoke();
                }
            }

            // 페이즈 전환
            if (_phaseTicksRemaining <= 0)
            {
                AdvancePhase();
            }
        }

        private void AdvancePhase()
        {
            var current = CurrentWeather;

            switch (current.Phase)
            {
                case WeatherPhase.Calm:
                    TransitionTo(WeatherPhase.Building, GetNextSurgeLevel());
                    break;

                case WeatherPhase.Building:
                    TransitionTo(WeatherPhase.Storm, current.SurgeLevel);
                    break;

                case WeatherPhase.Storm:
                    _stormCount++;
                    TransitionTo(WeatherPhase.Subsiding, current.SurgeLevel);
                    break;

                case WeatherPhase.Subsiding:
                    TransitionTo(WeatherPhase.Calm, SurgeLevel.None);
                    break;
            }
        }

        private SurgeLevel GetNextSurgeLevel()
        {
            // stormsPerSurgeUp 횟수마다 서지 레벨 상승
            if (_stormCount > 0 && _stormCount % _stormsPerSurgeUp == 0)
            {
                // Surge2가 최대
                return SurgeLevel.Surge2;
            }
            return SurgeLevel.Surge1;
        }

        private void TransitionTo(WeatherPhase phase, SurgeLevel surge)
        {
            var state = new WeatherState
            {
                Phase = phase,
                SurgeLevel = surge,
            };

            // 페이즈별 설정
            switch (phase)
            {
                case WeatherPhase.Calm:
                    _phaseTicksRemaining = _calmDuration;
                    state.LightningChance = 0f;
                    state.SurgeMultiplier = 0.1f;
                    state.Visibility = 1f;
                    break;

                case WeatherPhase.Building:
                    _phaseTicksRemaining = _buildingDuration;
                    state.LightningChance = 0.05f;
                    state.SurgeMultiplier = 0.3f;
                    state.Visibility = 0.7f;
                    break;

                case WeatherPhase.Storm:
                    _phaseTicksRemaining = _stormDuration;
                    state.LightningChance = surge switch
                    {
                        SurgeLevel.Surge1 => 0.15f,
                        SurgeLevel.Surge2 => 0.35f,
                        _ => 0.05f,
                    };
                    state.SurgeMultiplier = surge switch
                    {
                        SurgeLevel.Surge1 => 1.0f,
                        SurgeLevel.Surge2 => 2.5f,
                        _ => 0.3f,
                    };
                    state.Visibility = surge switch
                    {
                        SurgeLevel.Surge1 => 0.5f,
                        SurgeLevel.Surge2 => 0.25f,
                        _ => 0.8f,
                    };
                    break;

                case WeatherPhase.Subsiding:
                    _phaseTicksRemaining = _subsidingDuration;
                    state.LightningChance = 0.03f;
                    state.SurgeMultiplier = 0.2f;
                    state.Visibility = 0.8f;
                    break;
            }

            CurrentWeather = state;
            OnWeatherChanged?.Invoke(state);

            Debug.Log($"[Weather] {phase} (Surge: {surge}, " +
                      $"Lightning: {state.LightningChance:P0}, " +
                      $"PowerMult: {state.SurgeMultiplier:F1}x)");
        }

        // ── 디버그용: 강제 서지 전환 ──
        public void DebugForceSurge(SurgeLevel level)
        {
            TransitionTo(WeatherPhase.Storm, level);
        }
    }
}
