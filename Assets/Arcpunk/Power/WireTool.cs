// ── WireTool.cs ──
// 플레이어가 CopperWire 아이템을 들고 블록 간 와이어 연결을 생성/삭제.
// 게리 모드 Wire Tool 스타일:
//   LMB 첫 클릭 → 블록A 선택 (프리뷰 케이블 시작)
//   LMB 두번째 클릭 → 블록B 확정 (와이어 생성, 아이템 소모)
//   RMB → 기존 와이어 삭제 (와이어가 연결된 블록 클릭)
//   ESC → 선택 취소
//
// Player 오브젝트에 부착. BlockInteraction과 공존하며,
// CopperWire 아이템 장착 시에만 활성화.

using UnityEngine;

namespace Arcpunk.Power
{
    public class WireTool : MonoBehaviour
    {
        // ── 설정 ──
        [Header("Raycast")]
        [SerializeField] private float _reachDistance = 100f;  // 무제한 거리 허용
        [SerializeField] private LayerMask _blockLayer;        // 복셀 지형 레이어

        [Header("UI Feedback")]
        [SerializeField] private Color _validHighlight = new Color(0.3f, 1f, 0.5f, 0.6f);
        [SerializeField] private Color _invalidHighlight = new Color(1f, 0.3f, 0.3f, 0.6f);

        // ── 상태 ──
        private bool _isActive;            // CopperWire 장착 시 true
        private bool _hasFirstBlock;       // 블록A 선택 완료 여부
        private Vector3Int _firstBlock;    // 선택된 블록A 좌표
        private Camera _cam;

        // ── 참조 ──
        private WireManager _wireManager;
        private WireRenderer _wireRenderer;

        // ── 하이라이트 큐브 ──
        private GameObject _highlightCube;
        private Renderer _highlightRenderer;

        // =====================================================
        //  초기화
        // =====================================================

        private void Start()
        {
            _cam = Camera.main;
            _wireManager = WireManager.Instance;
            _wireRenderer = WireRenderer.Instance;
            CreateHighlightCube();
        }

        private void CreateHighlightCube()
        {
            _highlightCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _highlightCube.name = "WireToolHighlight";
            _highlightCube.transform.localScale = Vector3.one * 1.02f;

            // 콜라이더 제거 (시각 전용)
            var col = _highlightCube.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _highlightRenderer = _highlightCube.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat != null)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetColor("_Color", _validHighlight);
                mat.renderQueue = 3100;
            }
            _highlightRenderer.material = mat;
            _highlightCube.SetActive(false);
        }

        // =====================================================
        //  Update 루프
        // =====================================================

        private void Update()
        {
            // ── 활성 상태 체크 ──
            _isActive = IsHoldingWireItem();

            if (!_isActive)
            {
                CancelSelection();
                _highlightCube.SetActive(false);
                return;
            }

            // ── ESC: 선택 취소 ──
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSelection();
                return;
            }

            // ── 레이캐스트 ──
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _blockLayer))
            {
                _highlightCube.SetActive(false);
                if (_hasFirstBlock)
                    _wireRenderer?.HidePreview();
                return;
            }

            // 히트 위치에서 블록 좌표 추출 (히트 노말 반대 방향으로 살짝 안쪽)
            Vector3 inBlock = hit.point - hit.normal * 0.1f;
            Vector3Int blockPos = Vector3Int.FloorToInt(inBlock);

            // ── 유효한 전력 블록인지 체크 ──
            bool isValid = IsValidPowerBlock(blockPos);

            // 하이라이트 표시
            _highlightCube.SetActive(true);
            _highlightCube.transform.position = new Vector3(
                blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            _highlightRenderer.material.SetColor("_Color",
                isValid ? _validHighlight : _invalidHighlight);

            // ── 프리뷰 케이블 (블록A 선택 후) ──
            if (_hasFirstBlock && _wireRenderer != null)
            {
                Vector3 from = new Vector3(
                    _firstBlock.x + 0.5f, _firstBlock.y + 0.5f, _firstBlock.z + 0.5f);
                Vector3 to = new Vector3(
                    blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
                _wireRenderer.ShowPreview(from, to);
            }

            // ── LMB: 연결 생성 ──
            if (Input.GetMouseButtonDown(0) && isValid)
            {
                HandleLeftClick(blockPos);
            }

            // ── RMB: 연결 삭제 ──
            if (Input.GetMouseButtonDown(1))
            {
                HandleRightClick(blockPos);
            }
        }

        // =====================================================
        //  클릭 핸들러
        // =====================================================

        private void HandleLeftClick(Vector3Int blockPos)
        {
            if (!_hasFirstBlock)
            {
                // ── 첫 번째 블록 선택 ──
                _firstBlock = blockPos;
                _hasFirstBlock = true;
                Debug.Log($"[WireTool] 블록A 선택: {blockPos}. 두 번째 블록을 클릭하세요.");
            }
            else
            {
                // ── 두 번째 블록 → 와이어 생성 ──
                if (blockPos == _firstBlock)
                {
                    Debug.LogWarning("[WireTool] 같은 블록입니다. 다른 블록을 선택하세요.");
                    return;
                }

                int wireId = _wireManager.AddWire(_firstBlock, blockPos);
                if (wireId >= 0)
                {
                    // 성공: 아이템 소모
                    ConsumeWireItem();
                    Debug.Log($"[WireTool] 와이어 생성 완료: {_firstBlock} ↔ {blockPos}");
                }

                // 선택 리셋 (성공/실패 모두)
                CancelSelection();
            }
        }

        private void HandleRightClick(Vector3Int blockPos)
        {
            // 클릭한 블록에 연결된 와이어 중 가장 최근 것을 삭제
            // (여러 와이어가 있을 수 있으므로 하나씩 삭제)
            if (_wireManager == null) return;

            var wireIds = _wireManager.GetWireIdsAt(blockPos);
            int lastId = -1;
            foreach (int id in wireIds)
            {
                lastId = id; // 마지막 것
            }

            if (lastId >= 0)
            {
                _wireManager.RemoveWire(lastId);
                Debug.Log($"[WireTool] 와이어 #{lastId} 삭제.");
            }
            else
            {
                Debug.Log($"[WireTool] {blockPos}에 연결된 와이어 없음.");
            }
        }

        // =====================================================
        //  선택 취소
        // =====================================================

        private void CancelSelection()
        {
            if (_hasFirstBlock)
            {
                _hasFirstBlock = false;
                _wireRenderer?.HidePreview();
                Debug.Log("[WireTool] 선택 취소.");
            }
        }

        // =====================================================
        //  아이템 연동
        // =====================================================

        private Inventory.PlayerInventory _inventory;

        private void InitInventory()
        {
            if (_inventory == null)
                _inventory = Inventory.PlayerInventory.Instance;
        }

        /// <summary>현재 핫바에서 CopperWire 아이템을 들고 있는지 체크.</summary>
        private bool IsHoldingWireItem()
        {
            InitInventory();
            if (_inventory == null) return false;

            var item = _inventory.GetSelectedItem();
            if (item == null || item.IsEmpty) return false;

            return item.Type == Inventory.ItemType.CopperWire;
        }

        /// <summary>와이어 생성 성공 시 인벤토리에서 CopperWire 1개 소모.</summary>
        private void ConsumeWireItem()
        {
            InitInventory();
            if (_inventory == null) return;

            _inventory.ConsumeSelected();
        }

        /// <summary>해당 좌표가 전력 블록인지 확인.
        /// 와이어는 전력 블록끼리만 연결 가능.</summary>
        private bool IsValidPowerBlock(Vector3Int pos)
        {
            var world = Voxel.VoxelWorld.Instance;
            if (world == null) return false;

            var bt = world.GetBlock(pos.x, pos.y, pos.z);
            if (bt == Voxel.BlockType.Air) return false;

            // 기존 BlockDef.IsPowerBlock 필드 사용
            Voxel.BlockDef def = Voxel.BlockData.Defs[(ushort)bt];
            return def.IsPowerBlock;
        }

        // =====================================================
        //  정리
        // =====================================================

        private void OnDisable()
        {
            CancelSelection();
            if (_highlightCube != null)
                _highlightCube.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_highlightCube != null)
                Destroy(_highlightCube);
        }
    }
}