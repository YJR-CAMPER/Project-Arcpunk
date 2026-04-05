// ── KnappingUI.cs ──
// 날빗기 미니게임 UI. 5×5 격자에서 돌을 클릭으로 깨내어 도구 형태를 만든다.
// 작업대(Workbench)를 우클릭하면 열림.
// Canvas + Button을 런타임에 자동 생성.

using UnityEngine;
using UnityEngine.UI;
using Arcpunk.Crafting;
using Arcpunk.Inventory;

namespace Arcpunk.UI
{
    public class KnappingUI : MonoBehaviour
    {
        public static KnappingUI Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _cellSize = 70f;
        [SerializeField] private float _cellGap = 4f;
        [SerializeField] private Color _stoneColor = new Color(0.55f, 0.55f, 0.5f);
        [SerializeField] private Color _chippedColor = new Color(0.15f, 0.15f, 0.18f, 0.5f);
        [SerializeField] private Color _targetHintColor = new Color(0.35f, 0.55f, 0.35f, 0.3f);

        public bool IsOpen { get; private set; }

        // UI 요소
        private GameObject _panel;
        private Canvas _canvas;
        private Button[,] _gridButtons;
        private Image[,] _gridImages;
        private Image[,] _targetHints;
        private Text _titleText;
        private Text _statusText;
        private Button[] _recipeButtons;
        private Button _completeButton;
        private Button _resetButton;
        private Button _closeButton;

        // 게임 상태
        private bool[,] _grid;          // true = 돌 존재
        private KnappingRecipe _currentRecipe;
        private bool _isComplete;
        private bool _isRuined;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateUI();
            Close();
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        public void Open()
        {
            IsOpen = true;
            _panel.SetActive(true);

            // 마우스 커서 활성화
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 레시피 선택 화면으로
            ShowRecipeSelection();
        }

        public void Close()
        {
            IsOpen = false;
            _panel.SetActive(false);

            // 마우스 커서 다시 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _currentRecipe = null;
        }

        // ═══════════════════════════════════════
        // UI 생성
        // ═══════════════════════════════════════

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("KnappingCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // 핫바 위에
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 반투명 배경 (전체 화면)
            GameObject bgObj = CreateUIElement("Background", canvasObj.transform);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.6f);

            // 메인 패널
            _panel = CreateUIElement("KnappingPanel", canvasObj.transform);
            RectTransform panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 600);
            Image panelBG = _panel.AddComponent<Image>();
            panelBG.color = new Color(0.1f, 0.1f, 0.13f, 0.95f);

            // 타이틀
            _titleText = CreateText("Title", _panel.transform,
                new Vector2(0, 250), new Vector2(600, 40), 24, TextAnchor.MiddleCenter);
            _titleText.text = "날빗기 (Knapping)";
            _titleText.color = new Color(1f, 0.9f, 0.4f);

            // 상태 텍스트
            _statusText = CreateText("Status", _panel.transform,
                new Vector2(0, 210), new Vector2(600, 30), 16, TextAnchor.MiddleCenter);
            _statusText.color = Color.white;

            // 5×5 격자
            _gridButtons = new Button[5, 5];
            _gridImages = new Image[5, 5];
            _targetHints = new Image[5, 5];
            _grid = new bool[5, 5];

            float gridStartX = -(_cellSize + _cellGap) * 2;
            float gridStartY = (_cellSize + _cellGap) * 2 - 30;

            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
            {
                float px = gridStartX + x * (_cellSize + _cellGap);
                float py = gridStartY - y * (_cellSize + _cellGap);

                // 타겟 힌트 (셀 뒤에 반투명으로)
                GameObject hintObj = CreateUIElement($"Hint_{x}_{y}", _panel.transform);
                RectTransform hintRect = hintObj.GetComponent<RectTransform>();
                hintRect.anchoredPosition = new Vector2(px, py);
                hintRect.sizeDelta = new Vector2(_cellSize, _cellSize);
                Image hintImg = hintObj.AddComponent<Image>();
                hintImg.color = Color.clear;
                _targetHints[y, x] = hintImg;

                // 셀 버튼
                GameObject cellObj = CreateUIElement($"Cell_{x}_{y}", _panel.transform);
                RectTransform cellRect = cellObj.GetComponent<RectTransform>();
                cellRect.anchoredPosition = new Vector2(px, py);
                cellRect.sizeDelta = new Vector2(_cellSize, _cellSize);

                Image cellImg = cellObj.AddComponent<Image>();
                cellImg.color = _stoneColor;

                Button cellBtn = cellObj.AddComponent<Button>();
                int cx = x, cy = y; // 클로저 캡처
                cellBtn.onClick.AddListener(() => OnCellClicked(cx, cy));

                _gridButtons[y, x] = cellBtn;
                _gridImages[y, x] = cellImg;
            }

            // 레시피 선택 버튼들 (오른쪽에 배치)
            _recipeButtons = new Button[KnappingRecipes.All.Length];
            for (int i = 0; i < KnappingRecipes.All.Length; i++)
            {
                var recipe = KnappingRecipes.All[i];
                float btnY = 140 - i * 55;

                GameObject btnObj = CreateUIElement($"Recipe_{i}", _panel.transform);
                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(250, btnY);
                btnRect.sizeDelta = new Vector2(180, 45);

                Image btnBG = btnObj.AddComponent<Image>();
                btnBG.color = new Color(0.2f, 0.2f, 0.3f);

                Button btn = btnObj.AddComponent<Button>();
                int recipeIndex = i;
                btn.onClick.AddListener(() => SelectRecipe(recipeIndex));

                Text btnText = CreateText("Text", btnObj.transform,
                    Vector2.zero, new Vector2(170, 40), 14, TextAnchor.MiddleCenter);
                btnText.text = $"{recipe.Name}\n({recipe.MaterialItem} ×{recipe.MaterialCost})";
                btnText.color = Color.white;

                _recipeButtons[i] = btn;
            }

            // 완성 버튼
            {
                GameObject btnObj = CreateUIElement("CompleteBtn", _panel.transform);
                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(250, -100);
                btnRect.sizeDelta = new Vector2(180, 50);

                Image btnBG = btnObj.AddComponent<Image>();
                btnBG.color = new Color(0.2f, 0.6f, 0.3f);

                _completeButton = btnObj.AddComponent<Button>();
                _completeButton.onClick.AddListener(OnComplete);

                Text btnText = CreateText("Text", btnObj.transform,
                    Vector2.zero, new Vector2(170, 45), 18, TextAnchor.MiddleCenter);
                btnText.text = "완성!";
                btnText.color = Color.white;
                btnText.fontStyle = FontStyle.Bold;
            }

            // 리셋 버튼
            {
                GameObject btnObj = CreateUIElement("ResetBtn", _panel.transform);
                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(250, -160);
                btnRect.sizeDelta = new Vector2(180, 40);

                Image btnBG = btnObj.AddComponent<Image>();
                btnBG.color = new Color(0.6f, 0.3f, 0.2f);

                _resetButton = btnObj.AddComponent<Button>();
                _resetButton.onClick.AddListener(ResetGrid);

                Text btnText = CreateText("Text", btnObj.transform,
                    Vector2.zero, new Vector2(170, 35), 14, TextAnchor.MiddleCenter);
                btnText.text = "다시 하기";
                btnText.color = Color.white;
            }

            // 닫기 버튼
            {
                GameObject btnObj = CreateUIElement("CloseBtn", _panel.transform);
                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(320, 265);
                btnRect.sizeDelta = new Vector2(40, 40);

                Image btnBG = btnObj.AddComponent<Image>();
                btnBG.color = new Color(0.7f, 0.2f, 0.2f);

                _closeButton = btnObj.AddComponent<Button>();
                _closeButton.onClick.AddListener(Close);

                Text btnText = CreateText("Text", btnObj.transform,
                    Vector2.zero, new Vector2(35, 35), 20, TextAnchor.MiddleCenter);
                btnText.text = "X";
                btnText.color = Color.white;
                btnText.fontStyle = FontStyle.Bold;
            }
        }

        // ═══════════════════════════════════════
        // 게임 로직
        // ═══════════════════════════════════════

        private void ShowRecipeSelection()
        {
            _statusText.text = "만들 도구를 선택하세요";
            _completeButton.gameObject.SetActive(false);
            _resetButton.gameObject.SetActive(false);

            // 재료 체크하여 버튼 활성/비활성
            var inv = PlayerInventory.Instance;
            for (int i = 0; i < KnappingRecipes.All.Length; i++)
            {
                var recipe = KnappingRecipes.All[i];
                bool hasEnough = inv != null &&
                    inv.CountItem(recipe.MaterialItem) >= recipe.MaterialCost;
                _recipeButtons[i].interactable = hasEnough;

                var bg = _recipeButtons[i].GetComponent<Image>();
                bg.color = hasEnough
                    ? new Color(0.2f, 0.2f, 0.3f)
                    : new Color(0.15f, 0.15f, 0.15f);
            }

            // 격자 숨기기
            SetGridVisible(false);
        }

        private void SelectRecipe(int index)
        {
            _currentRecipe = KnappingRecipes.All[index];

            // 재료 소모
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            int removed = inv.RemoveItem(
                _currentRecipe.MaterialItem, _currentRecipe.MaterialCost);
            if (removed < _currentRecipe.MaterialCost)
            {
                _statusText.text = "재료가 부족합니다!";
                return;
            }

            // 격자 초기화 (모두 돌로 채움)
            _isComplete = false;
            _isRuined = false;
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                _grid[y, x] = true;

            // 레시피 버튼 숨기기
            foreach (var btn in _recipeButtons)
                btn.gameObject.SetActive(false);

            // 격자 + 버튼 표시
            SetGridVisible(true);
            _completeButton.gameObject.SetActive(true);
            _resetButton.gameObject.SetActive(true);

            _titleText.text = $"날빗기: {_currentRecipe.Name}";
            _statusText.text = "클릭하여 돌을 깨세요. 목표 형태를 만드세요!";

            RefreshGrid();
        }

        private void OnCellClicked(int x, int y)
        {
            if (_currentRecipe == null || _isComplete || _isRuined) return;
            if (!_grid[y, x]) return; // 이미 깨진 칸

            // 돌 깨기! (되돌리기 불가)
            _grid[y, x] = false;

            // 돌 깨는 소리 (자극 등록)
            Ghoul.StimulusManager.Instance?.OnBlockBroken(
                Player.PlayerController.Instance?.transform.position ?? Vector3.zero);

            // 상태 체크
            CheckState();
            RefreshGrid();
        }

        private void CheckState()
        {
            if (_currentRecipe == null) return;

            bool matches = true;
            int remainingStone = 0;
            int targetStone = 0;

            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
            {
                if (_grid[y, x]) remainingStone++;
                if (_currentRecipe.Pattern[y, x]) targetStone++;

                // 목표에서 돌이 있어야 하는데 깨져있으면 → 실패
                if (_currentRecipe.Pattern[y, x] && !_grid[y, x])
                {
                    _isRuined = true;
                }
                // 목표에서 돌이 없어야 하는데 있으면 → 아직 미완성
                if (!_currentRecipe.Pattern[y, x] && _grid[y, x])
                {
                    matches = false;
                }
            }

            if (_isRuined)
            {
                _statusText.text = "<color=red>실패! 필요한 부분을 깨버렸습니다.</color>";
                _statusText.color = new Color(1f, 0.3f, 0.3f);
                _completeButton.interactable = false;
            }
            else if (matches)
            {
                _isComplete = true;
                _statusText.text = "<color=green>완성! [완성] 버튼을 누르세요!</color>";
                _statusText.color = new Color(0.3f, 1f, 0.4f);
                _completeButton.interactable = true;
            }
            else
            {
                _statusText.text = $"남은 돌: {remainingStone} / 목표: {targetStone}";
                _statusText.color = Color.white;
                _completeButton.interactable = false;
            }
        }

        private void OnComplete()
        {
            if (!_isComplete || _currentRecipe == null) return;

            // 결과물 인벤토리에 추가
            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                inv.AddItem(_currentRecipe.ResultItem, _currentRecipe.ResultCount);
                Debug.Log($"[Knapping] Crafted: {_currentRecipe.Name} x{_currentRecipe.ResultCount}");
            }

            // 레시피 선택 화면으로 돌아가기
            _currentRecipe = null;
            foreach (var btn in _recipeButtons)
                btn.gameObject.SetActive(true);
            ShowRecipeSelection();
        }

        private void ResetGrid()
        {
            if (_currentRecipe == null) return;

            // 재료 환불 없이 리셋 (실수의 대가)
            // 다시 재료 소모
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            if (inv.CountItem(_currentRecipe.MaterialItem) < _currentRecipe.MaterialCost)
            {
                _statusText.text = "재료가 부족하여 다시 할 수 없습니다!";
                return;
            }

            inv.RemoveItem(_currentRecipe.MaterialItem, _currentRecipe.MaterialCost);

            _isComplete = false;
            _isRuined = false;
            _statusText.color = Color.white;
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                _grid[y, x] = true;

            _statusText.text = "클릭하여 돌을 깨세요.";
            RefreshGrid();
        }

        // ═══════════════════════════════════════
        // UI 갱신
        // ═══════════════════════════════════════

        private void RefreshGrid()
        {
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    bool hasStone = _grid[y, x];
                    bool shouldKeep = _currentRecipe != null && _currentRecipe.Pattern[y, x];

                    if (hasStone)
                    {
                        if (shouldKeep)
                        {
                            // 남겨야 할 돌 → 초록 테두리
                            _gridImages[y, x].color = new Color(0.4f, 0.65f, 0.4f);
                        }
                        else
                        {
                            // 깨야 할 돌 → 붉은 톤 (클릭 유도)
                            _gridImages[y, x].color = new Color(0.6f, 0.35f, 0.3f);
                        }
                        _gridButtons[y, x].interactable = !_isComplete && !_isRuined;
                    }
                    else
                    {
                        // 이미 깨진 칸
                        if (shouldKeep)
                        {
                            // 남겨야 했는데 깨버림 → 빨간 경고
                            _gridImages[y, x].color = new Color(0.7f, 0.15f, 0.15f, 0.6f);
                        }
                        else
                        {
                            // 정상적으로 깬 칸 → 어두운 빈 칸
                            _gridImages[y, x].color = _chippedColor;
                        }
                        _gridButtons[y, x].interactable = false;
                    }

                    _targetHints[y, x].color = Color.clear; // 힌트 레이어는 이제 불필요
                }
            }

        private void SetGridVisible(bool visible)
        {
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
            {
                _gridButtons[y, x].gameObject.SetActive(visible);
                _targetHints[y, x].gameObject.SetActive(visible);
            }
        }

        // ═══════════════════════════════════════
        // UI 헬퍼
        // ═══════════════════════════════════════

        private GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        private Text CreateText(string name, Transform parent,
            Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            GameObject obj = CreateUIElement(name, parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;

            Shadow shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            return text;
        }
    }
}
