// ── InventoryUI.cs ──
// E키로 여는 풀 인벤토리 UI. 핫바 9칸 + 메인 27칸.
// 좌클릭: 아이템 집기/놓기, 우클릭: 1개 놓기/절반 집기.
// Canvas를 런타임에 자동 생성.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Arcpunk.Inventory;

namespace Arcpunk.UI
{
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _slotSize = 56f;
        [SerializeField] private float _slotGap = 4f;
        [SerializeField] private Color _slotColor = new Color(0.18f, 0.18f, 0.24f, 0.9f);
        [SerializeField] private Color _slotHoverColor = new Color(0.3f, 0.3f, 0.4f, 0.9f);
        [SerializeField] private Texture2D _atlasTexture;

        public bool IsOpen { get; private set; }

        // UI 요소
        private GameObject _panel;
        private Canvas _canvas;
        private SlotUI[] _slotUIs;
        private int _atlasSize = 4;

        // 커서 아이템 (드래그 중)
        private ItemStack _cursorItem = new ItemStack();
        private RawImage _cursorIcon;
        private Text _cursorCount;
        private GameObject _cursorObj;

        private struct SlotUI
        {
            public RectTransform Rect;
            public Image Background;
            public RawImage Icon;
            public Text Count;
            public int SlotIndex;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateUI();
            Close();

            var inv = PlayerInventory.Instance;
            if (inv != null)
                inv.OnSlotChanged += OnSlotChanged;
        }

        private void OnDestroy()
        {
            var inv = PlayerInventory.Instance;
            if (inv != null)
                inv.OnSlotChanged -= OnSlotChanged;
        }

        private void Update()
        {
            if (IsOpen && Input.GetMouseButtonDown(0))
            {
                Debug.Log($"[InvUI] Click at {Input.mousePosition}, cursor visible={Cursor.visible}, lockState={Cursor.lockState}");

                var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                ped.position = Input.mousePosition;
                var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, results);
                Debug.Log($"[InvUI] Raycast hit {results.Count} objects:");
                foreach (var r in results)
                    Debug.Log($"  → {r.gameObject.name} (canvas sortOrder / depth)");
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (IsOpen) Close();
                else Open();
            }

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();

            // 커서 아이템 마우스 따라다니기
            if (IsOpen && _cursorObj != null)
            {
                _cursorObj.transform.position = Input.mousePosition;
                bool hasCursor = !_cursorItem.IsEmpty;
                _cursorObj.SetActive(hasCursor);
            }
        }

        // ═══════════════════════════════════════
        // 열기/닫기
        // ═══════════════════════════════════════

        public void Open()
        {
            _canvas.enabled = true;
            IsOpen = true;
            _panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAll();
        }

        public void Close()
        {
            // 커서에 아이템이 있으면 인벤토리에 돌려놓기
            if (!_cursorItem.IsEmpty)
            {
                var inv = PlayerInventory.Instance;
                if (inv != null)
                {
                    int leftover = inv.AddItem(_cursorItem.Type, _cursorItem.Count);
                    if (leftover > 0)
                    {
                        // 인벤토리 가득 — 바닥에 드롭
                        var player = Player.PlayerController.Instance;
                        if (player != null)
                            ItemEntity.Spawn(player.transform.position + Vector3.up,
                                _cursorItem.Type, leftover);
                    }
                }
                _cursorItem.Clear();
            }

            _canvas.enabled = false;
            IsOpen = false;
            _panel.SetActive(false);
            if (_cursorObj != null) _cursorObj.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ═══════════════════════════════════════
        // UI 생성
        // ═══════════════════════════════════════

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("InventoryCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 150;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 반투명 배경
            GameObject bgObj = new GameObject("BG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);

            // 메인 패널
            _panel = new GameObject("InvPanel");
            _panel.transform.SetParent(canvasObj.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            float panelW = 9 * (_slotSize + _slotGap) + 30;
            float panelH = 5 * (_slotSize + _slotGap) + 80;
            panelRect.sizeDelta = new Vector2(panelW, panelH);
            _panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            // 타이틀
            CreateLabel("인벤토리", _panel.transform,
                new Vector2(0, panelH / 2 - 25), new Vector2(panelW, 30),
                20, new Color(1f, 0.9f, 0.4f));

            // 슬롯 생성
            int totalSlots = PlayerInventory.TOTAL_SIZE; // 36
            _slotUIs = new SlotUI[totalSlots];

            float startX = -(9 * (_slotSize + _slotGap) - _slotGap) / 2 + _slotSize / 2;

            // 메인 인벤토리 (27칸: 3행 × 9열) — 위쪽
            for (int i = 0; i < PlayerInventory.MAIN_SIZE; i++)
            {
                int slotIndex = PlayerInventory.HOTBAR_SIZE + i; // 9~35
                int col = i % 9;
                int row = i / 9;

                float x = startX + col * (_slotSize + _slotGap);
                float y = 60 - row * (_slotSize + _slotGap);

                CreateSlot(slotIndex, new Vector2(x, y));
            }

            // 구분선 라벨
            CreateLabel("핫바", _panel.transform,
                new Vector2(0, 60 - 3 * (_slotSize + _slotGap) - 5),
                new Vector2(panelW, 20), 13, new Color(0.6f, 0.6f, 0.7f));

            // 핫바 (9칸) — 아래쪽
            for (int i = 0; i < PlayerInventory.HOTBAR_SIZE; i++)
            {
                float x = startX + i * (_slotSize + _slotGap);
                float y = 60 - 3 * (_slotSize + _slotGap) - 25;

                CreateSlot(i, new Vector2(x, y));
            }

            // 커서 아이템 (마우스 따라다니는 아이콘)
            _cursorObj = new GameObject("CursorItem");
            _cursorObj.transform.SetParent(canvasObj.transform, false);
            var cursorRect = _cursorObj.AddComponent<RectTransform>();
            cursorRect.sizeDelta = new Vector2(_slotSize, _slotSize);

            _cursorIcon = new GameObject("Icon").AddComponent<RawImage>();
            _cursorIcon.transform.SetParent(_cursorObj.transform, false);
            var iconRect = _cursorIcon.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4, 4);
            iconRect.offsetMax = new Vector2(-4, -4);
            _cursorIcon.raycastTarget = false;

            var countObj = new GameObject("Count");
            countObj.transform.SetParent(_cursorObj.transform, false);
            var countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1, 0);
            countRect.anchorMax = new Vector2(1, 0);
            countRect.pivot = new Vector2(1, 0);
            countRect.anchoredPosition = new Vector2(-2, 2);
            countRect.sizeDelta = new Vector2(40, 20);
            _cursorCount = countObj.AddComponent<Text>();
            _cursorCount.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _cursorCount.fontSize = 16;
            _cursorCount.fontStyle = FontStyle.Bold;
            _cursorCount.color = Color.white;
            _cursorCount.alignment = TextAnchor.LowerRight;
            _cursorCount.raycastTarget = false;
            countObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            _cursorObj.SetActive(false);
        }

        private void CreateSlot(int slotIndex, Vector2 position)
        {
            GameObject slotObj = new GameObject($"Slot_{slotIndex}");
            slotObj.transform.SetParent(_panel.transform, false);

            var rect = slotObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(_slotSize, _slotSize);

            var bg = slotObj.AddComponent<Image>();
            bg.color = _slotColor;

            var handler = slotObj.AddComponent<SlotClickHandler>();
            handler.SlotIndex = slotIndex;
            handler.OnLeft = OnSlotLeftClick;
            handler.OnRight = OnSlotRightClick;

            // 아이콘
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4, 4);
            iconRect.offsetMax = new Vector2(-4, -4);
            var icon = iconObj.AddComponent<RawImage>();
            icon.enabled = false;
            icon.raycastTarget = false;

            // 수량
            var countObj = new GameObject("Count");
            countObj.transform.SetParent(slotObj.transform, false);
            var countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1, 0);
            countRect.anchorMax = new Vector2(1, 0);
            countRect.pivot = new Vector2(1, 0);
            countRect.anchoredPosition = new Vector2(-2, 2);
            countRect.sizeDelta = new Vector2(40, 20);
            var count = countObj.AddComponent<Text>();
            count.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            count.fontSize = 14;
            count.fontStyle = FontStyle.Bold;
            count.color = Color.white;
            count.alignment = TextAnchor.LowerRight;
            count.raycastTarget = false;
            countObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            _slotUIs[slotIndex] = new SlotUI
            {
                Rect = rect,
                Background = bg,
                Icon = icon,
                Count = count,
                SlotIndex = slotIndex,
            };
        }

        // ═══════════════════════════════════════
        // 슬롯 클릭
        // ═══════════════════════════════════════

        private void OnSlotLeftClick(int slotIndex)
        {
            Debug.Log($"[InventoryUI] Slot {slotIndex} clicked!");

            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            var slot = inv.Slots[slotIndex];

            if (_cursorItem.IsEmpty)
            {
                // 커서 비어있음 → 슬롯에서 집기
                if (!slot.IsEmpty)
                {
                    _cursorItem = slot.Clone();
                    slot.Clear();
                    inv.NotifySlotChanged(slotIndex);
                }
            }
            else
            {
                if (slot.IsEmpty)
                {
                    // 슬롯 비어있음 → 커서 아이템 놓기
                    inv.Slots[slotIndex] = _cursorItem.Clone();
                    _cursorItem.Clear();
                    inv.NotifySlotChanged(slotIndex);
                }
                else if (slot.Type == _cursorItem.Type &&
                         slot.Count < slot.Def.MaxStack)
                {
                    // 같은 타입 → 합치기
                    int remaining = slot.MergeFrom(_cursorItem);
                    if (remaining <= 0)
                        _cursorItem.Clear();
                    else
                    {
                        _cursorItem.Count = remaining;
                    }
                    inv.NotifySlotChanged(slotIndex);
                }
                else
                {
                    // 다른 타입 → 교환
                    var temp = slot.Clone();
                    inv.Slots[slotIndex] = _cursorItem.Clone();
                    _cursorItem = temp;
                    inv.NotifySlotChanged(slotIndex);
                }
            }

            RefreshCursor();
            RefreshSlot(slotIndex);

            // 핫바 UI도 갱신
            if (slotIndex < PlayerInventory.HOTBAR_SIZE)
                HotbarUI.Instance?.GetType().GetMethod("RefreshAll",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                    ?.Invoke(HotbarUI.Instance, null);
        }

        private void OnSlotRightClick(int slotIndex)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            var slot = inv.Slots[slotIndex];

            if (_cursorItem.IsEmpty)
            {
                // 커서 비어있음 → 절반 집기
                if (!slot.IsEmpty && slot.Count > 1)
                {
                    int half = slot.Count / 2;
                    _cursorItem = new ItemStack(slot.Type, half);
                    _cursorItem.Durability = slot.Durability;
                    slot.Count -= half;
                    inv.NotifySlotChanged(slotIndex);
                }
                else if (!slot.IsEmpty)
                {
                    // 1개면 전부 집기
                    _cursorItem = slot.Clone();
                    slot.Clear();
                    inv.NotifySlotChanged(slotIndex);
                }
            }
            else
            {
                // 커서에 아이템 있음 → 1개만 놓기
                if (slot.IsEmpty)
                {
                    inv.Slots[slotIndex] = new ItemStack(_cursorItem.Type, 1);
                    inv.Slots[slotIndex].Durability = _cursorItem.Durability;
                    _cursorItem.Count--;
                    if (_cursorItem.Count <= 0) _cursorItem.Clear();
                    inv.NotifySlotChanged(slotIndex);
                }
                else if (slot.Type == _cursorItem.Type &&
                         slot.Count < slot.Def.MaxStack)
                {
                    slot.Count++;
                    _cursorItem.Count--;
                    if (_cursorItem.Count <= 0) _cursorItem.Clear();
                    inv.NotifySlotChanged(slotIndex);
                }
            }

            RefreshCursor();
            RefreshSlot(slotIndex);
        }

        // ═══════════════════════════════════════
        // 갱신
        // ═══════════════════════════════════════

        private void OnSlotChanged(int slotIndex)
        {
            if (IsOpen && slotIndex < _slotUIs.Length)
                RefreshSlot(slotIndex);
        }

        private void RefreshAll()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            for (int i = 0; i < PlayerInventory.TOTAL_SIZE; i++)
                RefreshSlot(i);
            RefreshCursor();
        }

        private void RefreshSlot(int index)
        {
            if (index >= _slotUIs.Length) return;
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            var ui = _slotUIs[index];
            var item = inv.Slots[index];

            if (item.IsEmpty)
            {
                ui.Icon.enabled = false;
                ui.Count.text = "";
            }
            else
            {
                if (_atlasTexture != null)
                {
                    ui.Icon.texture = _atlasTexture;
                    ui.Icon.uvRect = GetAtlasUV(item.Def.IconIndex);
                    ui.Icon.enabled = true;
                }
                ui.Count.text = item.Count > 1 ? item.Count.ToString() : "";
            }
        }

        private void RefreshCursor()
        {
            if (_cursorItem.IsEmpty)
            {
                _cursorObj.SetActive(false);
            }
            else
            {
                _cursorObj.SetActive(true);
                if (_atlasTexture != null)
                {
                    _cursorIcon.texture = _atlasTexture;
                    _cursorIcon.uvRect = GetAtlasUV(_cursorItem.Def.IconIndex);
                    _cursorIcon.enabled = true;
                }
                _cursorCount.text = _cursorItem.Count > 1
                    ? _cursorItem.Count.ToString() : "";
            }
        }

        private Rect GetAtlasUV(int index)
        {
            int col = index % _atlasSize;
            int row = index / _atlasSize;
            float size = 1f / _atlasSize;
            return new Rect(col * size, 1f - (row + 1) * size, size, size);
        }

        private void CreateLabel(string text, Transform parent,
            Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var obj = new GameObject(text);
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
        }
    }
    public class SlotClickHandler : MonoBehaviour,
    UnityEngine.EventSystems.IPointerClickHandler
    {
        public int SlotIndex;
        public System.Action<int> OnLeft;
        public System.Action<int> OnRight;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                OnLeft?.Invoke(SlotIndex);
            else if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                OnRight?.Invoke(SlotIndex);
        }
    }
}
