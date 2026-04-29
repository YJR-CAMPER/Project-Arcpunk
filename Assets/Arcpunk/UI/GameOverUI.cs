// ── GameOverUI.cs ──
// 사망 화면. 런타임 Canvas 생성 (에디터 에셋 불필요).
// "YOU DIED" + 리스폰 버튼 + 페이드인 연출.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Arcpunk.UI
{
    public class GameOverUI : MonoBehaviour
    {
        public static GameOverUI Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float _fadeInDuration = 1.5f;
        [SerializeField] private float _textDelay = 0.8f;       // 텍스트 등장 지연
        [SerializeField] private float _buttonDelay = 2.5f;     // 버튼 등장 지연

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Image _bgImage;
        private Text _deathText;
        private Text _subText;
        private Button _respawnButton;
        private Text _respawnButtonText;

        private bool _isShowing;

        private void Awake()
        {
            Instance = this;
            CreateUI();
            _canvas.enabled = false;
        }

        /// <summary>게임오버 화면 표시.</summary>
        public void Show()
        {
            if (_isShowing) return;
            _isShowing = true;
            _canvas.enabled = true;
            StartCoroutine(FadeInRoutine());
        }

        /// <summary>게임오버 화면 숨기기.</summary>
        public void Hide()
        {
            _isShowing = false;
            _canvas.enabled = false;
            StopAllCoroutines();
        }

        private IEnumerator FadeInRoutine()
        {
            // 초기 상태: 전부 투명
            _canvasGroup.alpha = 0;
            SetTextAlpha(_deathText, 0);
            SetTextAlpha(_subText, 0);
            _respawnButton.gameObject.SetActive(false);

            // 배경 페이드인
            float t = 0;
            while (t < _fadeInDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(t / _fadeInDuration) * 0.85f;
                yield return null;
            }

            // "YOU DIED" 텍스트 등장
            yield return new WaitForSeconds(_textDelay - _fadeInDuration);
            yield return FadeText(_deathText, 0.6f);

            // 부제 텍스트
            yield return new WaitForSeconds(0.3f);
            yield return FadeText(_subText, 0.5f);

            // 리스폰 버튼 등장
            yield return new WaitForSeconds(_buttonDelay - _textDelay - 1f);
            _respawnButton.gameObject.SetActive(true);

            // 버튼 깜빡임 효과
            StartCoroutine(ButtonPulse());
        }

        private IEnumerator FadeText(Text text, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(text, Mathf.Clamp01(t / duration));
                yield return null;
            }
        }

        private IEnumerator ButtonPulse()
        {
            var btnText = _respawnButtonText;
            while (_isShowing)
            {
                float pulse = (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f;
                SetTextAlpha(btnText, 0.6f + pulse * 0.4f);
                yield return null;
            }
        }

        private void SetTextAlpha(Text text, float alpha)
        {
            if (text == null) return;
            var c = text.color;
            c.a = alpha;
            text.color = c;
        }

        private void OnRespawn()
        {
            Hide();

            var player = Player.PlayerController.Instance;
            if (player != null)
            {
                var health = player.GetComponent<Player.PlayerHealth>();
                if (health != null) health.Respawn();

                player.ResetDeathState();
            }
        }

        // ═══════════════════════════════════════
        // 런타임 UI 생성
        // ═══════════════════════════════════════

        private void CreateUI()
        {
            // Canvas
            var canvasGo = new GameObject("GameOverCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;

            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();

            // 배경 (어두운 반투명)
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            _bgImage = bgGo.AddComponent<Image>();
            _bgImage.color = new Color(0.05f, 0, 0, 0.85f);
            _bgImage.raycastTarget = true;

            var bgRect = _bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // "YOU DIED" 텍스트
            var deathGo = new GameObject("DeathText");
            deathGo.transform.SetParent(canvasGo.transform, false);
            _deathText = deathGo.AddComponent<Text>();
            _deathText.text = "YOU DIED";
            _deathText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _deathText.fontSize = 72;
            _deathText.fontStyle = FontStyle.Bold;
            _deathText.color = new Color(0.8f, 0.15f, 0.1f, 0);
            _deathText.alignment = TextAnchor.MiddleCenter;

            var deathRect = _deathText.rectTransform;
            deathRect.anchorMin = new Vector2(0.5f, 0.55f);
            deathRect.anchorMax = new Vector2(0.5f, 0.55f);
            deathRect.sizeDelta = new Vector2(600, 100);
            deathRect.anchoredPosition = Vector2.zero;

            // 부제
            var subGo = new GameObject("SubText");
            subGo.transform.SetParent(canvasGo.transform, false);
            _subText = subGo.AddComponent<Text>();
            _subText.text = "The storm claims another...";
            _subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _subText.fontSize = 24;
            _subText.color = new Color(0.6f, 0.4f, 0.35f, 0);
            _subText.alignment = TextAnchor.MiddleCenter;

            var subRect = _subText.rectTransform;
            subRect.anchorMin = new Vector2(0.5f, 0.45f);
            subRect.anchorMax = new Vector2(0.5f, 0.45f);
            subRect.sizeDelta = new Vector2(500, 50);
            subRect.anchoredPosition = Vector2.zero;

            // 리스폰 버튼
            var btnGo = new GameObject("RespawnButton");
            btnGo.transform.SetParent(canvasGo.transform, false);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            _respawnButton = btnGo.AddComponent<Button>();
            _respawnButton.targetGraphic = btnImage;
            _respawnButton.onClick.AddListener(OnRespawn);

            // 버튼 색상 세팅
            var colors = _respawnButton.colors;
            colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            colors.highlightedColor = new Color(0.3f, 0.1f, 0.1f, 0.9f);
            colors.pressedColor = new Color(0.5f, 0.15f, 0.1f, 1f);
            _respawnButton.colors = colors;

            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.3f);
            btnRect.anchorMax = new Vector2(0.5f, 0.3f);
            btnRect.sizeDelta = new Vector2(250, 55);
            btnRect.anchoredPosition = Vector2.zero;

            // 버튼 텍스트
            var btnTextGo = new GameObject("ButtonText");
            btnTextGo.transform.SetParent(btnGo.transform, false);

            _respawnButtonText = btnTextGo.AddComponent<Text>();
            _respawnButtonText.text = "RESPAWN";
            _respawnButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _respawnButtonText.fontSize = 28;
            _respawnButtonText.fontStyle = FontStyle.Bold;
            _respawnButtonText.color = new Color(0.9f, 0.3f, 0.2f, 1f);
            _respawnButtonText.alignment = TextAnchor.MiddleCenter;

            var btnTextRect = _respawnButtonText.rectTransform;
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            btnGo.SetActive(false);
        }
    }
}
