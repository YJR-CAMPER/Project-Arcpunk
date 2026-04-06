// ── MainMenuUI.cs ──
// 메인 메뉴 씬의 UI. Canvas를 런타임에 자동 생성.
// Start: 게임 씬 로드 / Quit: 애플리케이션 종료

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace Arcpunk.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private string _gameSceneName = "SampleScene";

        private void Start()
        {
            // 커서 보이게
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();
            CreateUI();
        }

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

            // 배경 (어두운 단색 — 나중에 스카이박스/이미지로 교체 가능)
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgObj.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);

            // 타이틀
            CreateLabel("LAND OF THUNDER",
                canvasObj.transform,
                new Vector2(0, 200), new Vector2(1200, 100),
                72, new Color(1f, 0.9f, 0.4f));

            // 서브타이틀
            CreateLabel("— Arcpunk —",
                canvasObj.transform,
                new Vector2(0, 130), new Vector2(600, 40),
                24, new Color(0.7f, 0.7f, 0.8f));

            // 버튼들
            CreateButton("게임 시작", canvasObj.transform,
                new Vector2(0, -20), OnStartClicked);
            CreateButton("나가기", canvasObj.transform,
                new Vector2(0, -100), OnQuitClicked);
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
