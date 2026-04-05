// ── CraftingUI.cs ──
// 조합 레시피 목록 UI. 인벤토리(E키) 열린 상태에서 오른쪽에 표시.
// 재료가 충분한 레시피만 활성화, 클릭으로 즉시 제작.
// InventoryUI와 함께 작동.

using UnityEngine;
using UnityEngine.UI;
using Arcpunk.Crafting;
using Arcpunk.Inventory;

namespace Arcpunk.UI
{
    public class CraftingUI : MonoBehaviour
    {
        public static CraftingUI Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private Color _availableColor = new Color(0.2f, 0.25f, 0.3f, 0.9f);
        [SerializeField] private Color _unavailableColor = new Color(0.12f, 0.12f, 0.15f, 0.6f);

        private GameObject _panel;
        private Canvas _canvas;
        private GameObject _scrollContent;
        private Button[] _recipeButtons;
        private Text[] _recipeTexts;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateUI();
            _panel.SetActive(false);
        }

        private void Update()
        {
            // InventoryUI와 동기화
            var invUI = InventoryUI.Instance;
            if (invUI == null) return;

            if (invUI.IsOpen && !_panel.activeSelf)
            {
                _panel.SetActive(true);
                Refresh();
            }
            else if (!invUI.IsOpen && _panel.activeSelf)
            {
                _panel.SetActive(false);
            }
        }

        public void Refresh()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            for (int i = 0; i < SimpleCrafting.Recipes.Count && i < _recipeButtons.Length; i++)
            {
                var recipe = SimpleCrafting.Recipes[i];
                bool canCraft = SimpleCrafting.CanCraft(inv, recipe);

                _recipeButtons[i].interactable = canCraft;
                _recipeButtons[i].GetComponent<Image>().color =
                    canCraft ? _availableColor : _unavailableColor;

                // 재료 텍스트 구성
                string ingredients = "";
                foreach (var (item, count) in recipe.Ingredients)
                {
                    string name = ItemDatabase.Get(item).Name;
                    int have = inv.CountItem(item);
                    string color = have >= count ? "white" : "red";
                    ingredients += $"  <color={color}>{name} {have}/{count}</color>\n";
                }

                _recipeTexts[i].text = $"<b>{recipe.Name}</b>" +
                    (recipe.ResultCount > 1 ? $" x{recipe.ResultCount}" : "") +
                    $"\n{ingredients}";
            }
        }

        private void CreateUI()
        {
            // Canvas (InventoryUI와 같은 레이어)
            GameObject canvasObj = new GameObject("CraftingCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 155; // InventoryUI(150) 위
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 패널 (화면 오른쪽)
            _panel = new GameObject("CraftPanel");
            _panel.transform.SetParent(canvasObj.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.5f);
            panelRect.anchorMax = new Vector2(1, 0.5f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-20, 0);
            panelRect.sizeDelta = new Vector2(300, 600);
            _panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            // 타이틀
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_panel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -5);
            titleRect.sizeDelta = new Vector2(0, 35);
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.9f, 0.4f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "조합 (Crafting)";

            // 레시피 버튼들
            int recipeCount = SimpleCrafting.Recipes.Count;
            _recipeButtons = new Button[recipeCount];
            _recipeTexts = new Text[recipeCount];

            float btnHeight = 65f;
            float gap = 3f;
            float startY = -45f;

            for (int i = 0; i < recipeCount; i++)
            {
                float yPos = startY - i * (btnHeight + gap);

                var btnObj = new GameObject($"Recipe_{i}");
                btnObj.transform.SetParent(_panel.transform, false);
                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 1);
                btnRect.anchorMax = new Vector2(1, 1);
                btnRect.pivot = new Vector2(0.5f, 1);
                btnRect.anchoredPosition = new Vector2(0, yPos);
                btnRect.sizeDelta = new Vector2(-16, btnHeight);

                var btnBG = btnObj.AddComponent<Image>();
                btnBG.color = _unavailableColor;

                var btn = btnObj.AddComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnRecipeClicked(idx));

                // 텍스트
                var textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8, 4);
                textRect.offsetMax = new Vector2(-8, -4);

                var text = textObj.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = Color.white;
                text.alignment = TextAnchor.UpperLeft;
                text.supportRichText = true;
                text.raycastTarget = false;

                _recipeButtons[i] = btn;
                _recipeTexts[i] = text;
            }
        }

        private void OnRecipeClicked(int index)
        {
            if (index >= SimpleCrafting.Recipes.Count) return;

            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            var recipe = SimpleCrafting.Recipes[index];
            if (SimpleCrafting.Craft(inv, recipe))
            {
                Refresh(); // 재료 변동 반영
            }
        }
    }
}
