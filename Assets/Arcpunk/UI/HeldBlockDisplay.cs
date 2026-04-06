// ── HeldBlockDisplay.cs ──
// 1인칭 블록 들기. 화면 우하단에 현재 선택된 블록을 표시.
// 카메라 자식으로 작은 큐브를 배치, 핫바 변경 시 텍스처 교체.
// Player의 CameraHolder에 부착.

using UnityEngine;
using Arcpunk.Inventory;
using Arcpunk.Voxel;

namespace Arcpunk.UI
{
    public class HeldBlockDisplay : MonoBehaviour
    {
        [Header("Position (Camera Local)")]
        [SerializeField] private Vector3 _position = new Vector3(0.45f, -0.35f, 0.7f);
        [SerializeField] private Vector3 _rotation = new Vector3(15f, -35f, 5f);
        [SerializeField] private float _scale = 0.25f;

        [Header("Bob Animation")]
        [SerializeField] private float _bobSpeed = 3f;
        [SerializeField] private float _bobAmount = 0.02f;
        [SerializeField] private float _swayAmount = 0.01f;

        [Header("References")]
        [SerializeField] private Texture2D _blockAtlas; // 블록 텍스처 아틀라스

        private GameObject _blockObj;
        private MeshRenderer _blockRenderer;
        private Material _blockMaterial;
        private int _atlasSize = 4;

        private ItemType _currentItem = ItemType.None;
        private Vector3 _basePosition;
        private float _bobTimer;

        // 블록 교체 애니메이션
        private bool _isSwapping;
        private float _swapTimer;
        private float _swapDuration = 0.15f;

        private void Start()
        {
            CreateBlockDisplay();
            _basePosition = _position;

            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                inv.OnHotbarSelectionChanged += _ => UpdateDisplay();
                inv.OnSlotChanged += i =>
                {
                    if (i == inv.SelectedHotbar) UpdateDisplay();
                };
            }

            UpdateDisplay();
        }

        private void CreateBlockDisplay()
        {
            // 큐브 생성 (카메라 자식)
            _blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _blockObj.name = "HeldBlock";
            _blockObj.transform.SetParent(transform, false);
            _blockObj.transform.localPosition = _position;
            _blockObj.transform.localRotation = Quaternion.Euler(_rotation);
            _blockObj.transform.localScale = Vector3.one * _scale;

            // 충돌 제거
            var col = _blockObj.GetComponent<BoxCollider>();
            if (col != null) Destroy(col);

            // 머티리얼 생성
            _blockRenderer = _blockObj.GetComponent<MeshRenderer>();
            _blockMaterial = new Material(Shader.Find("Unlit/Texture"));
            _blockRenderer.material = _blockMaterial;
            _blockRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _blockRenderer.receiveShadows = false;

            // 레이어: UI 또는 별도 레이어로 설정하면 복셀 월드에 가려지지 않음
            // 간단 처리: 카메라 near clip 바로 앞에 두면 가려지지 않음

            _blockObj.SetActive(false);
        }

        private void Update()
        {
            if (_blockObj == null || !_blockObj.activeSelf) return;

            // 걷기 흔들림
            var player = Player.PlayerController.Instance;
            float speed = player != null ? player.CurrentSpeed : 0;
            bool isMoving = speed > 0.5f;

            if (isMoving)
                _bobTimer += Time.deltaTime * _bobSpeed * Mathf.Min(speed / 3f, 1.5f);

            float bobY = Mathf.Sin(_bobTimer * 2f) * _bobAmount;
            float bobX = Mathf.Cos(_bobTimer) * _swayAmount;

            // 블록 교체 애니메이션 (내려갔다 올라오기)
            float swapOffset = 0;
            if (_isSwapping)
            {
                _swapTimer += Time.deltaTime;
                float t = _swapTimer / _swapDuration;
                if (t >= 1f)
                {
                    _isSwapping = false;
                    swapOffset = 0;
                }
                else
                {
                    // 내려갔다 올라오는 커브
                    swapOffset = -Mathf.Sin(t * Mathf.PI) * 0.15f;
                }
            }

            _blockObj.transform.localPosition = _basePosition +
                new Vector3(bobX, bobY + swapOffset, 0);
        }

        private void UpdateDisplay()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            var item = inv.GetSelectedItem();

            if (item.IsEmpty || !item.Def.IsPlaceable)
            {
                // 블록이 아니면 숨김
                _blockObj.SetActive(false);
                _currentItem = ItemType.None;
                return;
            }

            if (item.Type == _currentItem)
                return; // 같은 블록이면 변경 없음

            _currentItem = item.Type;

            // 교체 애니메이션 시작
            _isSwapping = true;
            _swapTimer = 0;

            // 텍스처 업데이트
            if (_blockAtlas != null)
            {
                _blockMaterial.mainTexture = _blockAtlas;

                // 블록의 텍스처 인덱스로 UV 설정
                // 큐브의 모든 면에 같은 텍스처 (side 기준)
                ref BlockDef blockDef = ref BlockData.Get(item.Def.BlockType);
                int texIndex = blockDef.TexSide;

                // UV offset/scale로 아틀라스에서 해당 타일만 표시
                float tileSize = 1f / _atlasSize;
                int col = texIndex % _atlasSize;
                int row = texIndex / _atlasSize;

                _blockMaterial.mainTextureScale = new Vector2(tileSize, tileSize);
                _blockMaterial.mainTextureOffset = new Vector2(
                    col * tileSize,
                    1f - (row + 1) * tileSize
                );
            }

            _blockObj.SetActive(true);
        }
    }
}
