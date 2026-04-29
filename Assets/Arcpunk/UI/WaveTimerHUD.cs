// ── WaveTimerHUD.cs ──
// 화면 좌측 시계 스타일 HUD.
// 다음 호드 공세까지 남은 시간을 시:분 형식으로 표시 (실제로는 분:초).
// WeatherSystem의 Phase 타이밍과 WaveDirector의 웨이브 정보를 연동.
// 런타임 Canvas 생성 — 에디터 에셋 불필요.

using UnityEngine;
using UnityEngine.UI;
using Arcpunk.Weather;
using Arcpunk.Ghoul;

namespace Arcpunk.UI
{
    public class WaveTimerHUD : MonoBehaviour
    {
        public static WaveTimerHUD Instance { get; private set; }

        [Header("Wave Cycle (실제 초 단위)")]
        [SerializeField] private float _calmDuration = 180f;       // 평화 시간 (3분)
        [SerializeField] private float _buildingDuration = 60f;    // 경고/준비 시간 (1분)
        [SerializeField] private float _stormDuration = 120f;      // 전투 시간 (2분)
        [SerializeField] private float _subsidingDuration = 30f;   // 정리 시간 (30초)

        [Header("Display")]
        [SerializeField] private float _warningFlashSpeed = 3f;    // 경고 깜빡임 속도

        // UI 요소
        private Canvas _canvas;
        private Text _dayText;
        private Text _timeText;
        private Text _phaseText;
        private Text _directionText;
        private Image _timerBG;
        private Image _timerFill;

        // 타이밍
        private float _phaseTimer;
        private float _currentPhaseDuration;
        private int _currentDay = 1;
        private WeatherPhase _lastPhase = WeatherPhase.Calm;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateUI();
            _phaseTimer = _calmDuration;
            _currentPhaseDuration = _calmDuration;

            // WaveDirector 이벤트 구독
            var waveDir = WaveDirector.Instance;
            if (waveDir != null)
            {
                waveDir.OnWaveAnnounced += OnWaveAnnounced;
                waveDir.OnWaveCompleted += OnWaveCompleted;
            }
        }

        private void OnDestroy()
        {
            var waveDir = WaveDirector.Instance;
            if (waveDir != null)
            {
                waveDir.OnWaveAnnounced -= OnWaveAnnounced;
                waveDir.OnWaveCompleted -= OnWaveCompleted;
            }
        }

        private void Update()
        {
            var weather = WeatherSystem.Instance;
            if (weather == null) return;

            WeatherPhase currentPhase = weather.CurrentWeather.Phase;

            // Phase 전환 감지
            if (currentPhase != _lastPhase)
            {
                OnPhaseChanged(currentPhase);
                _lastPhase = currentPhase;
            }

            // 타이머 카운트다운
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer < 0) _phaseTimer = 0;

            UpdateDisplay(currentPhase);
        }

        // ═══════════════════════════════════════
        // Phase 전환 처리
        // ═══════════════════════════════════════

        private void OnPhaseChanged(WeatherPhase newPhase)
        {
            switch (newPhase)
            {
                case WeatherPhase.Calm:
                    _currentDay++;
                    _phaseTimer = _calmDuration;
                    _currentPhaseDuration = _calmDuration;
                    break;
                case WeatherPhase.Building:
                    _phaseTimer = _buildingDuration;
                    _currentPhaseDuration = _buildingDuration;
                    break;
                case WeatherPhase.Storm:
                    _phaseTimer = _stormDuration;
                    _currentPhaseDuration = _stormDuration;
                    break;
                case WeatherPhase.Subsiding:
                    _phaseTimer = _subsidingDuration;
                    _currentPhaseDuration = _subsidingDuration;
                    break;
            }
        }

        private void OnWaveAnnounced(WavePlan plan)
        {
            // 공격 방향 표시
            string dirs = "";
            foreach (var d in plan.Directions)
            {
                string arrow = d switch
                {
                    HordeDirection.North => "▲ 북",
                    HordeDirection.South => "▼ 남",
                    HordeDirection.East  => "► 동",
                    HordeDirection.West  => "◄ 서",
                    _ => "?"
                };
                if (dirs.Length > 0) dirs += ", ";
                dirs += arrow;
            }

            if (_directionText != null)
                _directionText.text = $"공세 방향: {dirs}";
        }

        private void OnWaveCompleted(int wave)
        {
            if (_directionText != null)
                _directionText.text = "";
        }

        // ═══════════════════════════════════════
        // 디스플레이 업데이트
        // ═══════════════════════════════════════

        private void UpdateDisplay(WeatherPhase phase)
        {
            // Day 표시
            var waveDir = WaveDirector.Instance;
            int waveNum = waveDir != null ? waveDir.CurrentWave : 0;
            _dayText.text = $"Day {_currentDay}";

            // 시간 표시 (초 → 분:초, "시:분" 형식으로 표현)
            int totalSeconds = Mathf.CeilToInt(_phaseTimer);
            int displayMin = totalSeconds / 60;
            int displaySec = totalSeconds % 60;

            switch (phase)
            {
                case WeatherPhase.Calm:
                    // 다음 공세까지 전체 남은 시간 (Calm + Building)
                    float totalRemaining = _phaseTimer + _buildingDuration;
                    int totalSec = Mathf.CeilToInt(totalRemaining);
                    _timeText.text = $"{totalSec / 60:D2}:{totalSec % 60:D2}";
                    _timeText.color = Color.white;
                    _phaseText.text = "다음 공세까지";
                    _phaseText.color = new Color(0.7f, 0.9f, 0.7f);
                    break;

                case WeatherPhase.Building:
                    _timeText.text = $"{displayMin:D2}:{displaySec:D2}";
                    // 경고 깜빡임
                    float flash = (Mathf.Sin(Time.time * _warningFlashSpeed) + 1f) * 0.5f;
                    _timeText.color = Color.Lerp(Color.yellow, Color.red, flash);
                    _phaseText.text = $"⚠ Wave {waveNum + 1} 접근 중!";
                    _phaseText.color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), flash);
                    break;

                case WeatherPhase.Storm:
                    _timeText.text = $"{displayMin:D2}:{displaySec:D2}";
                    _timeText.color = new Color(1f, 0.3f, 0.2f);
                    int alive = GhoulSpawner.Instance?.ActiveGhoulCount ?? 0;
                    _phaseText.text = $"⛈ Wave {waveNum} 전투 중 | 구울: {alive}";
                    _phaseText.color = new Color(1f, 0.4f, 0.3f);
                    break;

                case WeatherPhase.Subsiding:
                    _timeText.text = $"{displayMin:D2}:{displaySec:D2}";
                    _timeText.color = new Color(0.8f, 0.8f, 0.5f);
                    _phaseText.text = "폭풍 약화 중...";
                    _phaseText.color = new Color(0.7f, 0.7f, 0.5f);
                    break;
            }

            // 타이머 바
            float ratio = _currentPhaseDuration > 0
                ? _phaseTimer / _currentPhaseDuration : 0;
            _timerFill.rectTransform.anchorMax = new Vector2(ratio, 1);

            Color barColor = phase switch
            {
                WeatherPhase.Calm      => new Color(0.3f, 0.8f, 0.4f, 0.9f),
                WeatherPhase.Building  => new Color(0.9f, 0.8f, 0.1f, 0.9f),
                WeatherPhase.Storm     => new Color(0.9f, 0.2f, 0.1f, 0.9f),
                WeatherPhase.Subsiding => new Color(0.6f, 0.6f, 0.3f, 0.9f),
                _ => Color.white,
            };
            _timerFill.color = barColor;
        }

        // ═══════════════════════════════════════
        // 런타임 UI 생성
        // ═══════════════════════════════════════

        private void CreateUI()
        {
            // Canvas
            var canvasGo = new GameObject("WaveTimerCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var cg = canvasGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 메인 패널 (좌측)
            var panelGo = new GameObject("TimerPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.8f);

            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(0, 0.5f);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.anchoredPosition = new Vector2(10, 100);
            panelRect.sizeDelta = new Vector2(220, 120);

            // Day 텍스트
            _dayText = CreateText(panelGo.transform, "DayText",
                new Vector2(0, 0.75f), new Vector2(1, 1),
                "Day 1", 20, FontStyle.Bold,
                new Color(1f, 0.9f, 0.4f));

            // 시간 텍스트 (큰 숫자)
            _timeText = CreateText(panelGo.transform, "TimeText",
                new Vector2(0, 0.35f), new Vector2(1, 0.75f),
                "00:00", 36, FontStyle.Bold,
                Color.white);

            // Phase 텍스트
            _phaseText = CreateText(panelGo.transform, "PhaseText",
                new Vector2(0, 0.12f), new Vector2(1, 0.35f),
                "다음 공세까지", 14, FontStyle.Normal,
                new Color(0.7f, 0.9f, 0.7f));

            // 타이머 바 (하단)
            var barBgGo = new GameObject("TimerBarBG");
            barBgGo.transform.SetParent(panelGo.transform, false);
            _timerBG = barBgGo.AddComponent<Image>();
            _timerBG.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.02f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.1f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            var barFillGo = new GameObject("TimerBarFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            _timerFill = barFillGo.AddComponent<Image>();
            _timerFill.color = new Color(0.3f, 0.8f, 0.4f, 0.9f);
            var barFillRect = barFillGo.GetComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;

            // 공격 방향 텍스트 (패널 아래)
            var dirGo = new GameObject("DirectionText");
            dirGo.transform.SetParent(canvasGo.transform, false);
            _directionText = dirGo.AddComponent<Text>();
            _directionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _directionText.fontSize = 16;
            _directionText.fontStyle = FontStyle.Bold;
            _directionText.color = new Color(1f, 0.6f, 0.2f);
            _directionText.alignment = TextAnchor.MiddleLeft;
            _directionText.text = "";

            var dirShadow = dirGo.AddComponent<Shadow>();
            dirShadow.effectColor = new Color(0, 0, 0, 0.9f);
            dirShadow.effectDistance = new Vector2(1, -1);

            var dirRect = dirGo.GetComponent<RectTransform>();
            dirRect.anchorMin = new Vector2(0, 0.5f);
            dirRect.anchorMax = new Vector2(0, 0.5f);
            dirRect.pivot = new Vector2(0, 1);
            dirRect.anchoredPosition = new Vector2(10, 35);
            dirRect.sizeDelta = new Vector2(250, 25);
        }

        private Text CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string defaultText, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = defaultText;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(5, 0);
            rect.offsetMax = new Vector2(-5, 0);

            return text;
        }
    }
}
