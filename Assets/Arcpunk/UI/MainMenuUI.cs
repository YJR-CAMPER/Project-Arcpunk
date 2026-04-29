using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace Arcpunk.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private string _gameSceneName = "SampleScene";

        [Header("Entrance Effect")]
        [SerializeField] private AudioClip _thunderSound;
        [SerializeField] private float _darkHoldTime = 0.6f;   // 암전 유지 시간
        [SerializeField] private float _flashTime = 0.15f;     // 흰 플래시 지속
        [SerializeField] private float _uiFadeInTime = 0.8f;

        private CanvasGroup _uiGroup;     // 타이틀+버튼 묶음 (페이드용)
        private Image _flashOverlay;      // 흰색 플래시
        private Image _darkOverlay;       // 초기 암전
        private AudioSource _audio;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();
            CreateUI();
            StartCoroutine(PlayEntrance());
        }

        // ─────────────────────────────────────────
        // 등장 시퀀스: 암전 → 플래시+천둥 → UI 등장
        // ─────────────────────────────────────────
        private IEnumerator PlayEntrance()
        {
            // 시작 상태: 배경 이미지는 보이지만 그 위에 검정 오버레이
            SetAlpha(_darkOverlay, 1f);
            SetAlpha(_flashOverlay, 0f);
            _uiGroup.alpha = 0f;

            // 잠깐 암전 유지 (기대감)
            yield return new WaitForSeconds(_darkHoldTime);

            // ⚡ 꽈릉! — 사운드 + 흰 플래시 + 암전 제거
            if (_audio != null && _thunderSound != null)
                _audio.PlayOneShot(_thunderSound);

            SetAlpha(_flashOverlay, 1f);
            SetAlpha(_darkOverlay, 0f);   // 즉시 제거 — 플래시가 덮고 있음

            // 플래시 페이드아웃
            float t = 0f;
            while (t < _flashTime)
            {
                t += Time.deltaTime;
                SetAlpha(_flashOverlay, 1f - (t / _flashTime));
                yield return null;
            }
            SetAlpha(_flashOverlay, 0f);

            // UI 페이드인
            t = 0f;
            while (t < _uiFadeInTime)
            {
                t += Time.deltaTime;
                _uiGroup.alpha = Mathf.Clamp01(t / _uiFadeInTime);
                yield return null;
            }
            _uiGroup.alpha = 1f;
        }

        private void SetAlpha(Image img, float a)
        {
            var c = img.color;
            c.a = Mathf.Clamp01(a);
            img.color = c;
        }

        // ─────────────────────────────────────────
        // UI 생성
        // ─────────────────────────────────────────

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        private void CreateUI()
        {
            // Canvas
            var canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 오디오 소스
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.volume = 0.9f;

            // ── 배경 이미지 (인트로 마지막 슬라이드) ──
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgObj.AddComponent<Image>();
            if (IntroSequence.LastSlideImage != null)
            {
                bgImage.sprite = IntroSequence.LastSlideImage;
                bgImage.preserveAspect = false;   // 화면 꽉 채우기
                bgImage.color = Color.white;
            }
            else
            {
                // 인트로를 건너뛰거나 씬 단독 실행 시 폴백
                bgImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            }

            // ── UI 묶음 (CanvasGroup으로 일괄 페이드) ──
            var uiRoot = new GameObject("UIRoot");
            uiRoot.transform.SetParent(canvasObj.transform, false);
            var uiRootRect = uiRoot.AddComponent<RectTransform>();
            uiRootRect.anchorMin = Vector2.zero;
            uiRootRect.anchorMax = Vector2.one;
            uiRootRect.offsetMin = Vector2.zero;
            uiRootRect.offsetMax = Vector2.zero;
            _uiGroup = uiRoot.AddComponent<CanvasGroup>();

            // 반투명 어두운 베일 (배경 이미지 위에 깔아서 텍스트 가독성 확보)
            var veilObj = new GameObject("Veil");
            veilObj.transform.SetParent(uiRoot.transform, false);
            var veilRect = veilObj.AddComponent<RectTransform>();
            veilRect.anchorMin = Vector2.zero;
            veilRect.anchorMax = Vector2.one;
            veilRect.offsetMin = Vector2.zero;
            veilRect.offsetMax = Vector2.zero;
            veilObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);

            // 타이틀
            CreateLabel("LAND OF THUNDER", uiRoot.transform,
                new Vector2(0, 200), new Vector2(1400, 120),
                72, new Color(1f, 0.9f, 0.4f));

            CreateLabel("— Arcpunk —", uiRoot.transform,
                new Vector2(0, 120), new Vector2(600, 40),
                24, new Color(0.8f, 0.8f, 0.9f));

            // 버튼
            CreateButton("게임 시작", uiRoot.transform,
                new Vector2(0, -20), OnStartClicked);
            CreateButton("나가기", uiRoot.transform,
                new Vector2(0, -100), OnQuitClicked);

            // ── 오버레이들 (UI 위, 등장 연출용) ──
            _darkOverlay = CreateFullscreenOverlay(canvasObj.transform,
                "DarkOverlay", Color.black);
            _flashOverlay = CreateFullscreenOverlay(canvasObj.transform,
                "FlashOverlay", Color.white);
            _flashOverlay.raycastTarget = false;
            _darkOverlay.raycastTarget = false;
        }

        private Image CreateFullscreenOverlay(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private void CreateLabel(string text, Transform parent,
            Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var obj = new GameObject($"Label_{text}");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var txt = obj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
            txt.raycastTarget = false;
            obj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        }

        private void CreateButton(string label, Transform parent,
            Vector2 pos, System.Action onClick)
        {
            var obj = new GameObject($"Btn_{label}");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(320, 64);

            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.18f, 0.24f, 0.95f);

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = bg;   // ← InventoryUI에서 배운 교훈
            var colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.24f, 0.95f);
            colors.highlightedColor = new Color(0.30f, 0.30f, 0.42f, 1f);
            colors.pressedColor = new Color(0.12f, 0.12f, 0.18f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            // 라벨
            var labelObj = new GameObject("Text");
            labelObj.transform.SetParent(obj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var txt = labelObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(1f, 0.95f, 0.8f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.raycastTarget = false;
        }

        private void OnStartClicked()
        {
            Debug.Log("[MainMenu] Start clicked → loading game scene");
            SceneManager.LoadScene(_gameSceneName);
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenu] Quit clicked");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
