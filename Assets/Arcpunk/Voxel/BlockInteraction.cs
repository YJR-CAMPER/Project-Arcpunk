// ── BlockInteraction.cs (수정 v2) ──
// 프로젝트 실제 API에 맞춰 수정:
//   - PlayerInventory.GetSelectedItem() (NOT GetSelectedHotbarItem)
//   - ItemStack.Def / ItemStack.IsEmpty / ItemStack.Type
//   - ItemEntity.Spawn(Vector3, ItemType, int)
//   - CraftingUI 존재 여부 안전 체크
//   - WireTool 직접 참조 제거 (컴파일 순서 독립)

using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Inventory;

namespace Arcpunk.Voxel
{
    public class BlockInteraction : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _reachDistance = 6f;

        [Header("Melee Combat")]
        [SerializeField] private float _meleeRange = 3f;
        [SerializeField] private float _baseMeleeDamage = 10f;
        [SerializeField] private float _meleeKnockback = 4f;
        [SerializeField] private LayerMask _ghoulLayer;

        [Header("Visual")]
        [SerializeField] private GameObject _selectionHighlightPrefab;

        // ── 참조 ──
        private VoxelWorld _world;
        private Camera _cam;
        private PlayerInventory _inventory;
        private GameObject _highlight;
        private Renderer _highlightRenderer;

        // ── 파괴 진행 상태 ──
        private float _breakProgress;
        private Vector3Int _breakingBlockPos;
        private bool _isBreaking;
        private float _swingRepeatTimer;

        // =====================================================
        //  초기화
        // =====================================================

        private void Start()
        {
            _world = VoxelWorld.Instance;
            _cam = Camera.main;
            _inventory = PlayerInventory.Instance;
            CreateHighlight();
        }

        private void CreateHighlight()
        {
            if (_selectionHighlightPrefab != null)
            {
                _highlight = Instantiate(_selectionHighlightPrefab);
            }
            else
            {
                _highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _highlight.name = "BlockSelectionHighlight";
                _highlight.transform.localScale = Vector3.one * 1.005f;

                var col = _highlight.GetComponent<Collider>();
                if (col != null) Destroy(col);

                _highlightRenderer = _highlight.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Particles/Standard Unlit"));
                if (mat != null)
                {
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.15f));
                    mat.renderQueue = 3100;
                }
                if (_highlightRenderer != null)
                    _highlightRenderer.material = mat;
            }
            _highlight.SetActive(false);
        }

        // =====================================================
        //  Update 루프
        // =====================================================

        private void Update()
        {
            // UI 열려있으면 중단
            if (UI.KnappingUI.Instance != null && UI.KnappingUI.Instance.IsOpen) return;
            if (UI.InventoryUI.Instance != null && UI.InventoryUI.Instance.IsOpen) return;

            // CraftingUI 존재 여부 안전 체크 (IsOpen 프로퍼티가 없을 수 있으므로 gameObject.activeSelf 사용)
            // 프로젝트에 CraftingUI.IsOpen이 있으면 위와 같은 패턴으로 교체 가능
            // if (UI.CraftingUI.Instance != null && UI.CraftingUI.Instance.IsOpen) return;

            // WireTool 활성 시 블록 인터랙션 차단
            // (WireTool 직접 참조 대신 아이템 타입으로 판별 → 컴파일 순서 독립)
            if (IsHoldingWireItem())
            {
                HideHighlight();
                ResetBreaking();
                return;
            }

            HandleBlockInteraction();
        }

        private void HandleBlockInteraction()
        {
            if (_cam == null || _world == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

            if (RaycastVoxel(ray, _reachDistance, out Vector3Int hitBlock, out Vector3Int hitNormal))
            {
                ShowHighlight(hitBlock);

                // ── 좌클릭: 블록 파괴 ──
                if (Input.GetMouseButton(0))
                {
                    HandleBlockBreaking(hitBlock);
                }
                else
                {
                    ResetBreaking();
                }

                // ── 우클릭: 블록 설치 ──
                if (Input.GetMouseButtonDown(1))
                {
                    HandleBlockPlacement(hitBlock, hitNormal);
                }
            }
            else
            {
                HideHighlight();
                ResetBreaking();

                // ── 좌클릭 + 블록 미조준 → 근접 전투 ──
                if (Input.GetMouseButtonDown(0))
                {
                    HandleMeleeAttack();
                }
            }
        }

        // =====================================================
        //  블록 파괴
        // =====================================================

        private void HandleBlockBreaking(Vector3Int hitBlock)
        {
            if (!_isBreaking || _breakingBlockPos != hitBlock)
            {
                _isBreaking = true;
                _breakProgress = 0;
                _breakingBlockPos = hitBlock;
            }

            // 도구에 따른 파괴 속도
            float speedMult = GetToolSpeedMultiplier();
            _breakProgress += Time.deltaTime * speedMult;

            // 스윙 애니메이션 반복
            _swingRepeatTimer -= Time.deltaTime;
            if (_swingRepeatTimer <= 0f)
            {
                HeldVoxelItem.Instance?.TriggerSwing();
                _swingRepeatTimer = 0.3f;

                // ★ 채굴 타격음
                Audio.GameAudioManager.Instance?.PlayBlockHit(
                    new Vector3(hitBlock.x + 0.5f, hitBlock.y + 0.5f, hitBlock.z + 0.5f));
            }

            BlockType blockType = _world.GetBlock(hitBlock.x, hitBlock.y, hitBlock.z);
            float hardness = BlockData.Get(blockType).Hardness;

            if (hardness > 0 && _breakProgress >= hardness)
            {
                DestroyBlock(hitBlock, blockType);
                ResetBreaking();
            }
        }

        private void DestroyBlock(Vector3Int pos, BlockType blockType)
        {
            _world.SetBlock(pos.x, pos.y, pos.z, BlockType.Air);

            // ★ 블록 파괴음
            Audio.GameAudioManager.Instance?.PlayBlockBreak(
                new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f));

            // 전력 시스템에 블록 파괴 통보
            var powerSys = Power.VoxelPowerSystem.Instance;
            if (powerSys != null)
                powerSys.OnBlockChanged(pos.x, pos.y, pos.z, BlockType.Air);

            // 아이템 드롭
            SpawnBlockDrop(pos, blockType);
        }

        // =====================================================
        //  블록 설치
        // =====================================================

        private void HandleBlockPlacement(Vector3Int hitBlock, Vector3Int hitNormal)
        {
            var selectedItem = _inventory?.GetSelectedItem();
            if (selectedItem == null || selectedItem.IsEmpty) return;
            if (!selectedItem.Def.IsPlaceable) return;

            Vector3Int placePos = hitBlock + hitNormal;

            // 플레이어 위치와 겹침 방지
            Vector3 playerPos = _cam.transform.position;
            Vector3Int playerFeet = Vector3Int.FloorToInt(playerPos - new Vector3(0, 1.5f, 0));
            Vector3Int playerHead = Vector3Int.FloorToInt(playerPos - new Vector3(0, 0.5f, 0));

            if (placePos == playerFeet || placePos == playerHead)
                return;

            if (_world.GetBlock(placePos.x, placePos.y, placePos.z) != BlockType.Air)
                return;

            _world.SetBlock(placePos.x, placePos.y, placePos.z, selectedItem.Def.BlockType);
            HeldVoxelItem.Instance?.TriggerSwing();
            _inventory.ConsumeSelected();

            // ★ 블록 설치음
            Audio.GameAudioManager.Instance?.PlayBlockPlace(
                new Vector3(placePos.x + 0.5f, placePos.y + 0.5f, placePos.z + 0.5f));

            // 전력 시스템에 블록 설치 통보
            var powerSys = Power.VoxelPowerSystem.Instance;
            if (powerSys != null)
                powerSys.OnBlockChanged(placePos.x, placePos.y, placePos.z, selectedItem.Def.BlockType);
        }

        // =====================================================
        //  근접 전투
        // =====================================================

        private void HandleMeleeAttack()
        {
            HeldVoxelItem.Instance?.TriggerSwing();
            Audio.GameAudioManager.Instance?.PlayMeleeSwing();

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _meleeRange, _ghoulLayer))
                return;

            var ghoul = hit.collider.GetComponent<Ghoul.SimpleGhoul>();
            if (ghoul == null)
                ghoul = hit.collider.GetComponentInParent<Ghoul.SimpleGhoul>();

            if (ghoul == null || !ghoul.IsAlive) return;

            // 도구에 따른 데미지
            float damage = _baseMeleeDamage * GetToolDamageMultiplier();
            ghoul.TakeDamage(damage);
            Audio.GameAudioManager.Instance?.PlayMeleeHit();

            // 넉백
            var rb = ghoul.GetComponent<Rigidbody>();
            if (rb == null) rb = ghoul.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                Vector3 knockDir = (hit.point - _cam.transform.position).normalized;
                knockDir.y = Mathf.Max(knockDir.y, 0.3f);
                rb.AddForce(knockDir * _meleeKnockback, ForceMode.Impulse);
            }
        }

        // =====================================================
        //  DDA 복셀 레이캐스트
        // =====================================================

        private bool RaycastVoxel(Ray ray, float maxDistance,
            out Vector3Int hitBlock, out Vector3Int hitNormal)
        {
            hitBlock = Vector3Int.zero;
            hitNormal = Vector3Int.zero;

            Vector3 start = ray.origin;
            Vector3 dir = ray.direction.normalized;

            if (dir.x == 0f) dir.x = 1e-8f;
            if (dir.y == 0f) dir.y = 1e-8f;
            if (dir.z == 0f) dir.z = 1e-8f;

            int x = Mathf.FloorToInt(start.x);
            int y = Mathf.FloorToInt(start.y);
            int z = Mathf.FloorToInt(start.z);

            int stepX = dir.x > 0 ? 1 : -1;
            int stepY = dir.y > 0 ? 1 : -1;
            int stepZ = dir.z > 0 ? 1 : -1;

            float tDeltaX = Mathf.Abs(1f / dir.x);
            float tDeltaY = Mathf.Abs(1f / dir.y);
            float tDeltaZ = Mathf.Abs(1f / dir.z);

            float tMaxX = stepX > 0
                ? (x + 1 - start.x) * tDeltaX
                : (start.x - x) * tDeltaX;
            float tMaxY = stepY > 0
                ? (y + 1 - start.y) * tDeltaY
                : (start.y - y) * tDeltaY;
            float tMaxZ = stepZ > 0
                ? (z + 1 - start.z) * tDeltaZ
                : (start.z - z) * tDeltaZ;

            float distance = 0f;
            int lastAxis = -1;
            int maxSteps = Mathf.CeilToInt(maxDistance) * 3 + 1;

            for (int step = 0; step < maxSteps; step++)
            {
                BlockType block = _world.GetBlock(x, y, z);

                if (block != BlockType.Air && (BlockData.IsSolid(block) || BlockData.Defs[(ushort)block].IsCross))
                {
                    hitBlock = new Vector3Int(x, y, z);
                    switch (lastAxis)
                    {
                        case 0: hitNormal = new Vector3Int(-stepX, 0, 0); break;
                        case 1: hitNormal = new Vector3Int(0, -stepY, 0); break;
                        case 2: hitNormal = new Vector3Int(0, 0, -stepZ); break;
                        default: hitNormal = Vector3Int.zero; break;
                    }
                    return true;
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    { x += stepX; distance = tMaxX; tMaxX += tDeltaX; lastAxis = 0; }
                    else
                    { z += stepZ; distance = tMaxZ; tMaxZ += tDeltaZ; lastAxis = 2; }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    { y += stepY; distance = tMaxY; tMaxY += tDeltaY; lastAxis = 1; }
                    else
                    { z += stepZ; distance = tMaxZ; tMaxZ += tDeltaZ; lastAxis = 2; }
                }

                if (distance > maxDistance) break;
            }

            return false;
        }

        // =====================================================
        //  하이라이트
        // =====================================================

        private void ShowHighlight(Vector3Int pos)
        {
            if (_highlight == null) return;
            _highlight.SetActive(true);
            _highlight.transform.position = new Vector3(
                pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        }

        private void HideHighlight()
        {
            if (_highlight != null)
                _highlight.SetActive(false);
        }

        private void ResetBreaking()
        {
            _isBreaking = false;
            _breakProgress = 0f;
            _swingRepeatTimer = 0f;
        }

        // =====================================================
        //  도구 유틸
        // =====================================================

        private float GetToolSpeedMultiplier()
        {
            var item = _inventory?.GetSelectedItem();
            if (item == null || item.IsEmpty) return 1f;

            float mult = item.Def.SpeedMultiplier;
            return mult > 0 ? mult : 1f;
        }

        private float GetToolDamageMultiplier()
        {
            var item = _inventory?.GetSelectedItem();
            if (item == null || item.IsEmpty) return 1f;

            // 도구 등급에 비례한 데미지 배율
            return 1f + (int)item.Def.ToolTier * 0.5f;
        }

        /// <summary>CopperWire 아이템을 들고 있는지 체크.
        /// WireTool 직접 참조 없이 아이템 타입으로 판별.</summary>
        private bool IsHoldingWireItem()
        {
            var item = _inventory?.GetSelectedItem();
            if (item == null || item.IsEmpty) return false;

            // ItemType enum에 CopperWire가 있으면 직접 비교
            return item.Type == ItemType.CopperWire;
        }

        // =====================================================
        //  아이템 드롭
        // =====================================================

        private void SpawnBlockDrop(Vector3Int pos, BlockType blockType)
        {
            Vector3 dropPos = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);

            // 현재 도구 티어 확인
            ToolTier toolTier = ToolTier.Hand;
            var item = _inventory?.GetSelectedItem();
            if (item != null && !item.IsEmpty)
                toolTier = item.Def.ToolTier;

            // DropTable에서 드롭 목록 조회 (도구 티어에 따라 필터링)
            var drops = DropTable.GetDrops(blockType, toolTier);
            foreach (var (type, count) in drops)
            {
                ItemEntity.Spawn(dropPos, type, count);
            }
        }

        // =====================================================
        //  정리
        // =====================================================

        private void OnDestroy()
        {
            if (_highlight != null)
                Destroy(_highlight);
        }
    }
}