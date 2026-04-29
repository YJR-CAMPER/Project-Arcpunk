// ── IntroSequence.cs ──
// 게임 시작 시 재생되는 인트로. 이미지 + 내레이션 텍스트가 페이드인.
// Space/Esc로 스킵 가능. 끝나면 MainMenu 씬으로 이동.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace Arcpunk.UI
{
    public class IntroSequence : MonoBehaviour
    {
        public static Sprite LastSlideImage;

        [System.Serializable]
        public class IntroSlide
        {
            public Sprite Image;
            [TextArea(2, 4)] public string Narration;
            public float Duration = 4f;   // 이 슬라이드가 화면에 머무르는 시간
        }

        [Header("Slides (Inspector)")]
        [SerializeField] private IntroSlide[] _slides;

        [Header("Transition")]
        [SerializeField] private float _fadeInTime = 1.0f;
        [SerializeField] private float _fadeOutTime = 0.8f;
        [SerializeField] private string _nextSceneName = "MainMenu";

        [Header("Style")]
        [SerializeField] private int _narrationFontSize = 42;
        [SerializeField] private Color _narrationColor = new Color(1f, 0.95f, 0.85f);

        private Image _imageDisplay;
        private Text _narrationText;
        private Image _blackOverlay;       // 슬라이드 사이 페이드용
        private CanvasGroup _skipHintGroup;

        private bool _skipRequested;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;   // 인트로 중엔 숨김

            EnsureEventSystem();
            BuildUI();
            StartCoroutine(PlaySequence());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                _skipRequested = true;
        }

        // ═══════════════════════════════════════
        // Sequence
        // ═══════════════════════════════════════

        private IEnumerator PlaySequence()
        {
            // 검정에서 시작
            SetOverlayAlpha(1f);

            for (int i = 0; i < _slides.Length; i++)
            {
                if (_skipRequested) break;

                var slide = _slides[i];
                _imageDisplay.sprite = slide.Image;
                _imageDisplay.enabled = slide.Image != null;
                _narrationText.text = slide.Narration ?? "";

                // 페이드 인 (검정 → 화면)
                yield return Fade(1f, 0f, _fadeInTime);
                if (_skipRequested) break;

                // 표시 시간 (스킵 감지하면서 대기)
                float t = 0f;
                while (t < slide.Duration && !_skipRequested)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                if (_skipRequested) break;

                // 페이드 아웃 (화면 → 검정)
                yield return Fade(0f, 1f, _fadeOutTime);
            }

            // 스킵됐든 끝났든, 검정으로 마감 후 씬 전환
            if (_skipRequested)
                yield return Fade(GetOverlayAlpha(), 1f, 0.3f);

            // 마지막 슬라이드 이미지를 메인 메뉴로 전달
            if (_slides.Length > 0)
                LastSlideImage = _slides[_slides.Length - 1].Image;

            SceneManager.LoadScene(_nextSceneName);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (_skipRequested) yield break;
                t += Time.deltaTime;
                SetOverlayAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetOverlayAlpha(to);
        }

        // ═══════════════════════════════════════
        // UI 생성
        // ═══════════════════════════════════════

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        private void BuildUI()
        {
            // Canvas
            var canvasObj = new GameObject("IntroCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 검정 배경 (이미지 뒤)
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            StretchFull(bgRect);
            bgObj.AddComponent<Image>().color = Color.black;

            // 메인 이미지 (화면 가운데, 16:9 유지)
            var imgObj = new GameObject("SlideImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            var imgRect = imgObj.AddComponent<RectTransform>();
            imgRect.anchorMin = new Vector2(0.5f, 0.5f);
            imgRect.anchorMax = new Vector2(0.5f, 0.5f);
            imgRect.sizeDelta = new Vector2(1600, 900);
            imgRect.anchoredPosition = new Vector2(0, 60);
            _imageDisplay = imgObj.AddComponent<Image>();
            _imageDisplay.preserveAspect = true;

            // 내레이션 텍스트 (하단)
            var textObj = new GameObject("Narration");
            textObj.transform.SetParent(canvasObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0, 80);
            textRect.sizeDelta = new Vector2(1600, 200);
            _narrationText = textObj.AddComponent<Text>();
            _narrationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _narrationText.fontSize = _narrationFontSize;
            _narrationText.fontStyle = FontStyle.Bold;
            _narrationText.color = _narrationColor;
            _narrationText.alignment = TextAnchor.MiddleCenter;
            _narrationText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _narrationText.verticalOverflow = VerticalWrapMode.Overflow;
            textObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);

            // 페이드용 검정 오버레이 (이미지 + 텍스트 위)
            var overlayObj = new GameObject("FadeOverlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);
            var overlayRect = overlayObj.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            _blackOverlay = overlayObj.AddComponent<Image>();
            _blackOverlay.color = Color.black;
            _blackOverlay.raycastTarget = false;

            // 스킵 힌트 (우하단)
            var hintObj = new GameObject("SkipHint");
            hintObj.transform.SetParent(canvasObj.transform, false);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1, 0);
            hintRect.anchorMax = new Vector2(1, 0);
            hintRect.pivot = new Vector2(1, 0);
            hintRect.anchoredPosition = new Vector2(-40, 30);
            hintRect.sizeDelta = new Vector2(400, 40);
            var hint = hintObj.AddComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 18;
            hint.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            hint.alignment = TextAnchor.MiddleRight;
            hint.text = "Space / Esc — 건너뛰기";
            _skipHintGroup = hintObj.AddComponent<CanvasGroup>();
        }

        private void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetOverlayAlpha(float a)
        {
            var c = _blackOverlay.color;
            c.a = Mathf.Clamp01(a);
            _blackOverlay.color = c;
        }

        private float GetOverlayAlpha() => _blackOverlay.color.a;
    }
}
