// ── StimulusManager.cs ──
// 모든 빛/소리 자극을 중앙 관리.
// 구울 AI가 매 틱 이 시스템에 쿼리하여 가장 강한 자극 방향으로 이동.
// 낙뢰, 조명, 무기 발사, 블록 파괴 등이 자극을 등록한다.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Ghoul
{
    public enum StimulusType
    {
        Light,
        Sound,
    }

    public struct Stimulus
    {
        public StimulusType Type;
        public Vector3 Origin;
        public float Intensity;    // 높을수록 멀리서 감지
        public float Duration;     // 초 단위 지속시간
        public float Timestamp;    // 등록 시각 (Time.time)
        public bool Continuous;    // true면 매 틱 갱신 (조명 등)

        public bool IsExpired => !Continuous && Time.time > Timestamp + Duration;
        public float CurrentIntensity
        {
            get
            {
                if (Continuous) return Intensity;
                float elapsed = Time.time - Timestamp;
                float remaining = 1f - (elapsed / Duration);
                return Intensity * Mathf.Max(0, remaining);
            }
        }
    }

    public class StimulusManager : MonoBehaviour
    {
        public static StimulusManager Instance { get; private set; }

        private List<Stimulus> _stimuli = new();

        // 지속성 자극 (조명 등) — 키는 월드 좌표
        private Dictionary<Vector3Int, Stimulus> _continuousStimuli = new();

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // 낙뢰 이벤트 구독
            var lightning = Weather.LightningSystem.Instance;
            if (lightning != null)
                lightning.OnLightningStrike += OnLightningStrike;
        }

        private void OnDestroy()
        {
            var lightning = Weather.LightningSystem.Instance;
            if (lightning != null)
                lightning.OnLightningStrike -= OnLightningStrike;
        }

        private void Update()
        {
            // 만료된 자극 제거
            _stimuli.RemoveAll(s => s.IsExpired);
        }

        // ═══════════════════════════════════════
        // 자극 등록
        // ═══════════════════════════════════════

        /// <summary>일회성 자극 등록 (블록 파괴, 무기 발사 등).</summary>
        public void Register(Stimulus stimulus)
        {
            stimulus.Timestamp = Time.time;
            _stimuli.Add(stimulus);
        }

        /// <summary>지속성 자극 등록/갱신 (전력 공급 조명 등).</summary>
        public void RegisterContinuous(Vector3Int blockPos, Stimulus stimulus)
        {
            stimulus.Continuous = true;
            _continuousStimuli[blockPos] = stimulus;
        }

        /// <summary>지속성 자극 제거 (조명 전력 끊김 등).</summary>
        public void RemoveContinuous(Vector3Int blockPos)
        {
            _continuousStimuli.Remove(blockPos);
        }

        // ═══════════════════════════════════════
        // 자극 쿼리 (구울 AI용)
        // ═══════════════════════════════════════

        /// <summary>
        /// 지정 위치에서 감지 반경 내의 자극을 강도 순으로 반환.
        /// </summary>
        public List<Stimulus> Query(Vector3 position, float perceptionRadius)
        {
            var results = new List<Stimulus>();

            // 일회성 자극
            foreach (var s in _stimuli)
            {
                float dist = Vector3.Distance(position, s.Origin);
                float effectiveRange = s.CurrentIntensity; // 강도 = 유효 범위
                if (dist <= Mathf.Min(effectiveRange, perceptionRadius))
                    results.Add(s);
            }

            // 지속성 자극
            foreach (var kvp in _continuousStimuli)
            {
                var s = kvp.Value;
                float dist = Vector3.Distance(position, s.Origin);
                float effectiveRange = s.CurrentIntensity;
                if (dist <= Mathf.Min(effectiveRange, perceptionRadius))
                    results.Add(s);
            }

            // 강도 내림차순 정렬
            results.Sort((a, b) => b.CurrentIntensity.CompareTo(a.CurrentIntensity));
            return results;
        }

        /// <summary>감지 반경 내 가장 강한 자극 하나. 없으면 null.</summary>
        public Stimulus? GetStrongest(Vector3 position, float perceptionRadius)
        {
            var list = Query(position, perceptionRadius);
            return list.Count > 0 ? list[0] : null;
        }

        // ═══════════════════════════════════════
        // 이벤트 핸들러
        // ═══════════════════════════════════════

        private void OnLightningStrike(Vector3 pos)
        {
            // 낙뢰 = 거대한 빛 + 소리 자극
            Register(new Stimulus
            {
                Type = StimulusType.Light,
                Origin = pos,
                Intensity = 80f,
                Duration = 0.5f,
            });
            Register(new Stimulus
            {
                Type = StimulusType.Sound,
                Origin = pos,
                Intensity = 120f,
                Duration = 3f,
            });
        }

        /// <summary>블록 파괴 시 호출 (BlockInteraction에서).</summary>
        public void OnBlockBroken(Vector3 pos)
        {
            Register(new Stimulus
            {
                Type = StimulusType.Sound,
                Origin = pos,
                Intensity = 15f,
                Duration = 1.5f,
            });
        }

        /// <summary>무기 발사 시 호출.</summary>
        public void OnWeaponFired(Vector3 pos, float soundIntensity)
        {
            Register(new Stimulus
            {
                Type = StimulusType.Sound,
                Origin = pos,
                Intensity = soundIntensity,
                Duration = 2f,
            });
            Register(new Stimulus
            {
                Type = StimulusType.Light,
                Origin = pos,
                Intensity = soundIntensity * 0.5f,
                Duration = 0.3f,
            });
        }
    }
}
