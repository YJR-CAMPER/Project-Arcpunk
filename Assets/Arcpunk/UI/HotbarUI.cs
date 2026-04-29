// ── HotbarUI.cs ──
// Canvas 기반 핫바 UI. 화면 하단에 9칸 항상 표시.
// 런타임에 Canvas + 슬롯을 자동 생성하므로 수동 UI 세팅 불필요.

using UnityEngine;
using UnityEngine.UI;
using Arcpunk.Inventory;
using Arcpunk.Voxel;

namespace Arcpunk.UI
{
    public class HotbarUI : MonoBehaviour
    {
        public static HotbarUI Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _slotSize = 60f;
        [SerializeField] private float _slotGap = 4f;
        [SerializeField] private Color _normalColor = new Color(0.15f, 0.15f, 0.2f, 0.85f);
        [SerializeField] private Color _selectedColor = new Color(0.9f, 0.7f, 0.2f, 0.9f);
        [SerializeField] private Texture2D _atlasTexture; // Inspector에서 할당

        private Canvas _canvas;
        private RectTransform[] _slotRects;
        private Image[] _slotBGs;
        private RawImage[] _slotIcons;
        private Text[] _slotCounts;
        private Text _itemNameText;
        private int _atlasSize = 4;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateUI();

            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                inv.OnSlotChanged += OnSlotChanged;
                inv.OnHotbarSelectionChanged += OnSelectionChanged;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                inv.OnSlotChanged -= OnSlotChanged;
                inv.OnHotbarSelectionChanged -= OnSelectionChanged;
            }
        }

        private void CreateUI()
        {
            // Canvas 생성
            GameObject canvasObj = new GameObject("HotbarCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution =
                new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 핫바 컨테이너
            GameObject container = new GameObject("HotbarContainer");
            container.transform.SetParent(canvasObj.transform, false);
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0);
            containerRect.anchorMax = new Vector2(0.5f, 0);
            containerRect.pivot = new Vector2(0.5f, 0);
            float totalWidth = PlayerInventory.HOTBAR_SIZE * (_slotSize + _slotGap) - _slotGap;
            containerRect.sizeDelta = new Vector2(totalWidth + 16, _slotSize + 16);
            containerRect.anchoredPosition = new Vector2(0, 10);

            // 컨테이너 배경
            Image containerBG = container.AddComponent<Image>();
            containerBG.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);

            // 슬롯 생성
            _slotRects = new RectTransform[PlayerInventory.HOTBAR_SIZE];
            _slotBGs = new Image[PlayerInventory.HOTBAR_SIZE];
            _slotIcons = new RawImage[PlayerInventory.HOTBAR_SIZE];
            _slotCounts = new Text[PlayerInventory.HOTBAR_SIZE];

            for (int i = 0; i < PlayerInventory.HOTBAR_SIZE; i++)
            {
                // 슬롯 배경
                GameObject slotObj = new GameObject($"Slot_{i}");
                slotObj.transform.SetParent(container.transform, false);
                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                float xPos = -totalWidth / 2 + i * (_slotSize + _slotGap) + _slotSize / 2;
                slotRect.anchoredPosition = new Vector2(xPos, 0);
                slotRect.sizeDelta = new Vector2(_slotSize, _slotSize);

                Image slotBG = slotObj.AddComponent<Image>();
                slotBG.color = _normalColor;

                _slotRects[i] = slotRect;
                _slotBGs[i] = slotBG;

                // 아이콘
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(slotObj.transform, false);
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(4, 4);
                iconRect.offsetMax = new Vector2(-4, -4);

                RawImage icon = iconObj.AddComponent<RawImage>();
                icon.enabled = false;
                _slotIcons[i] = icon;

                // 수량 텍스트
                GameObject countObj = new GameObject("Count");
                countObj.transform.SetParent(slotObj.transform, false);
                RectTransform countRect = countObj.AddComponent<RectTransform>();
                countRect.anchorMin = new Vector2(1, 0);
                countRect.anchorMax = new Vector2(1, 0);
                countRect.pivot = new Vector2(1, 0);
                countRect.anchoredPosition = new Vector2(-4, 2);
                countRect.sizeDelta = new Vector2(40, 20);

                Text countText = countObj.AddComponent<Text>();
                countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                countText.fontSize = 16;
                countText.fontStyle = FontStyle.Bold;
                countText.color = Color.white;
                countText.alignment = TextAnchor.LowerRight;
                countText.text = "";

                // 텍스트에 그림자 추가
                Shadow shadow = countObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(1, -1);

                _slotCounts[i] = countText;
            }

            // 아이템 이름 텍스트 (핫바 위에 표시)
            GameObject nameObj = new GameObject("ItemName");
            nameObj.transform.SetParent(canvasObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0);
            nameRect.anchorMax = new Vector2(0.5f, 0);
            nameRect.pivot = new Vector2(0.5f, 0);
            nameRect.anchoredPosition = new Vector2(0, _slotSize + 30);
            nameRect.sizeDelta = new Vector2(400, 30);

            _itemNameText = nameObj.AddComponent<Text>();
            _itemNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _itemNameText.fontSize = 18;
            _itemNameText.color = Color.white;
            _itemNameText.alignment = TextAnchor.MiddleCenter;
            _itemNameText.text = "";

            Shadow nameShadow = nameObj.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.9f);
            nameShadow.effectDistance = new Vector2(1, -1);
        }

        private void OnSlotChanged(int slotIndex)
        {
            if (slotIndex < PlayerInventory.HOTBAR_SIZE)
            {
                RefreshSlot(slotIndex);

                // 선택 중인 슬롯의 내용이 바뀌면 손에 든 아이템도 갱신
                var inv = PlayerInventory.Instance;
                if (inv != null && slotIndex == inv.SelectedHotbar)
                {
                    var item = inv.GetSelectedItem();
                    var resolver = ItemIconResolver.Instance;
                    if (resolver != null)
                        resolver.UpdateHeldItem(item.IsEmpty ? ItemType.None : item.Type);
                }
            }
        }

        private void OnSelectionChanged(int selected)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            for (int i = 0; i < PlayerInventory.HOTBAR_SIZE; i++)
                RefreshSlot(i);

            // 선택된 아이템 이름 + 손에 든 아이템 갱신
            var selectedItem = inv.GetSelectedItem();
            _itemNameText.text = selectedItem.IsEmpty ? "" : selectedItem.Def.Name;

            // 손에 든 아이템 표시 갱신
            var resolver = ItemIconResolver.Instance;
            if (resolver != null)
            {
                if (selectedItem.IsEmpty)
                    resolver.UpdateHeldItem(ItemType.None);
                else
                    resolver.UpdateHeldItem(selectedItem.Type);
            }
        }

        private void RefreshSlot(int index)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || index >= PlayerInventory.HOTBAR_SIZE) return;

            var item = inv.Slots[index];

            // 선택 하이라이트
            _slotBGs[index].color = (index == inv.SelectedHotbar)
                ? _selectedColor : _normalColor;

            if (item.IsEmpty)
            {
                _slotIcons[index].enabled = false;
                _slotCounts[index].text = "";
            }
            else
            {
                // 아이템 종류에 따라 아이콘 분기
                SetSlotIcon(_slotIcons[index], item.Type);
                _slotCounts[index].text = item.Count > 1 ? item.Count.ToString() : "";
            }
        }

        /// <summary>아이템 타입에 따라 블록 아틀라스 또는 Gemini 아이콘으로 표시.</summary>
        private void SetSlotIcon(RawImage icon, ItemType type)
        {
            var mode = ItemIconResolver.GetIconMode(type);

            if (mode == IconMode.IsometricBlock && _atlasTexture != null)
            {
                icon.texture = _atlasTexture;
                icon.uvRect = GetAtlasUV(ItemDatabase.Get(type).IconIndex);
                icon.enabled = true;
            }
            else if (mode == IconMode.GeminiSprite)
            {
                var resolver = ItemIconResolver.Instance;
                var sprite = resolver != null ? resolver.GetSprite(type) : null;
                if (sprite != null)
                {
                    ItemIconResolver.ApplyToRawImage(icon, sprite);
                }
                else if (_atlasTexture != null)
                {
                    icon.texture = _atlasTexture;
                    icon.uvRect = GetAtlasUV(ItemDatabase.Get(type).IconIndex);
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }
            }
            else
            {
                icon.enabled = false;
            }
        }

        private Rect GetAtlasUV(int index)
        {
            int col = index % _atlasSize;
            int row = index / _atlasSize;
            float size = 1f / _atlasSize;
            // UV 좌하단 원점, 행은 위에서부터
            return new Rect(col * size, 1f - (row + 1) * size, size, size);
        }
    }
}