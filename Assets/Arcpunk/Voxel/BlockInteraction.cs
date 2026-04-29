// ── BlockInteraction.cs ── (v2 — 인벤토리 연동)
// 기존 BlockInteraction을 완전 교체.
// 핫바에서 ItemStack을 읽어 블록 설치, 도구 기반 파괴 속도/드롭.

using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Inventory;

namespace Arcpunk.Voxel
{
    public class BlockInteraction : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _reachDistance = 6f;
        [SerializeField] private LayerMask _voxelLayer;

        [Header("Melee Combat")]
        [SerializeField] private float _meleeRange = 3.5f;
        [SerializeField] private float _meleeCooldown = 0.4f;
        [SerializeField] private LayerMask _ghoulLayer;

        [Header("Melee Damage (도구별)")]
        [SerializeField] private float _handDamage = 2f;
        [SerializeField] private float _pickaxeDamage = 8f;
        [SerializeField] private float _axeDamage = 12f;
        [SerializeField] private float _knifeDamage = 18f;

        [Header("Visual")]
        [SerializeField] private GameObject _selectionHighlight;

        private VoxelWorld _world;
        private Camera _cam;
        private PlayerInventory _inventory;

        // 파괴 진행
        private float _breakProgress;
        private Vector3Int _breakingBlockPos;
        private bool _isBreaking;
        private float _swingRepeatTimer;

        // 근접 공격 쿨타임
        private float _meleeTimer;

        private void Start()
        {
            _world = VoxelWorld.Instance;
            _cam = Camera.main;
            _inventory = PlayerInventory.Instance;
        }

        private void Update()
        {
            if (UI.KnappingUI.Instance != null && UI.KnappingUI.Instance.IsOpen) return;
            if (UI.InventoryUI.Instance != null && UI.InventoryUI.Instance.IsOpen) return;

            if (_meleeTimer > 0) _meleeTimer -= Time.deltaTime;

            HandleBlockInteraction();
        }

        private void HandleBlockInteraction()
        {
            if (_cam == null || _world == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _voxelLayer))
            {
                ResetBreaking();
                HideHighlight();

                // 블록이 안 맞았으면 → 근접 공격 시도
                if (Input.GetMouseButton(0))
                    TryMeleeAttack();

                return;
            }

            // 블록 좌표 계산
            Vector3 blockInside = hit.point - hit.normal * 0.01f;
            Vector3Int targetBlock = new(
                Mathf.FloorToInt(blockInside.x),
                Mathf.FloorToInt(blockInside.y),
                Mathf.FloorToInt(blockInside.z)
            );

            Vector3 blockOutside = hit.point + hit.normal * 0.01f;
            Vector3Int placeBlock = new(
                Mathf.FloorToInt(blockOutside.x),
                Mathf.FloorToInt(blockOutside.y),
                Mathf.FloorToInt(blockOutside.z)
            );

            ShowHighlight(targetBlock);

            // ── 좌클릭: 파괴 ──
            if (Input.GetMouseButton(0))
            {
                BlockType targetType = _world.GetBlock(
                    targetBlock.x, targetBlock.y, targetBlock.z);
                if (targetType == BlockType.Air) return;

                // 현재 도구 정보
                var toolItem = _inventory?.GetSelectedItem();
                ToolTier toolTier = ToolTier.Hand;
                float speedMult = 1f;

                if (toolItem != null && !toolItem.IsEmpty)
                {
                    var def = toolItem.Def;
                    toolTier = def.ToolTier;
                    speedMult = def.SpeedMultiplier > 0 ? def.SpeedMultiplier : 1f;
                }

                // 파괴 가능 체크
                if (!DropTable.CanBreak(targetType, toolTier))
                    return; // 도구 티어 부족

                ref BlockDef blockDef = ref BlockData.Get(targetType);

                // 새로운 블록 파괴 시작
                if (!_isBreaking || _breakingBlockPos != targetBlock)
                {
                    _breakingBlockPos = targetBlock;
                    _breakProgress = 0;
                    _isBreaking = true;
                    HeldVoxelItem.Instance?.TriggerSwing();
                }

                // 도구 속도 적용
                _breakProgress += Time.deltaTime * speedMult;

                // 캐는 동안 반복 스윙 (마크 스타일)
                _swingRepeatTimer -= Time.deltaTime;
                if (_swingRepeatTimer <= 0)
                {
                    HeldVoxelItem.Instance?.TriggerSwing();
                    _swingRepeatTimer = 0.35f; // 스윙 반복 간격
                }

                if (_breakProgress >= blockDef.Hardness)
                {
                    // 블록 파괴!
                    _world.SetBlock(targetBlock.x, targetBlock.y, targetBlock.z,
                        BlockType.Air);
                    _world.RebuildDirtyChunks();

                    // 드롭 아이템 스폰
                    var drops = DropTable.GetDrops(targetType, toolTier);
                    foreach (var (itemType, count) in drops)
                    {
                        Vector3 dropPos = new Vector3(
                            targetBlock.x + 0.5f,
                            targetBlock.y + 0.5f,
                            targetBlock.z + 0.5f
                        );
                        ItemEntity.Spawn(dropPos, itemType, count);
                    }

                    // 도구 내구도 감소
                    if (toolItem != null && !toolItem.IsEmpty && toolItem.Durability > 0)
                    {
                        toolItem.Durability--;
                        if (toolItem.Durability <= 0)
                        {
                            toolItem.Clear();
                            Debug.Log("[BlockInteraction] Tool broke!");
                        }
                    }

                    // 자극 등록
                    Ghoul.StimulusManager.Instance?.OnBlockBroken(
                        new Vector3(targetBlock.x + 0.5f,
                                   targetBlock.y + 0.5f,
                                   targetBlock.z + 0.5f));

                    ResetBreaking();
                }
            }
            else
            {
                ResetBreaking();
            }

            // ── 우클릭: 설치 ──
            if (Input.GetMouseButtonDown(1))
            {
                // 날빗기 UI가 열려있으면 블록 설치 안 함
                if (UI.KnappingUI.Instance != null && UI.KnappingUI.Instance.IsOpen)
                    return;

                // 작업대를 바라보고 있으면 블록 설치 대신 상호작용
                BlockType lookingAt = _world.GetBlock(
                    targetBlock.x, targetBlock.y, targetBlock.z);
                if (lookingAt == BlockType.Workbench)
                    return; // WorkbenchInteraction이 처리

                var selectedItem = _inventory?.GetSelectedItem();
                if (selectedItem == null || selectedItem.IsEmpty) return;
                if (!selectedItem.Def.IsPlaceable) return;

                // 설치 위치 체크
                BlockType existing = _world.GetBlock(
                    placeBlock.x, placeBlock.y, placeBlock.z);
                if (existing != BlockType.Air) return;

                // 플레이어와 겹치지 않는지
                Vector3Int playerFeet = VoxelWorld.WorldPosToBlockCoord(
                    transform.position);
                Vector3Int playerHead = VoxelWorld.WorldPosToBlockCoord(
                    transform.position + Vector3.up * 1.5f);
                if (placeBlock == playerFeet || placeBlock == playerHead) return;

                // 블록 설치
                _world.SetBlock(placeBlock.x, placeBlock.y, placeBlock.z,
                    selectedItem.Def.BlockType);
                _world.RebuildDirtyChunks();
                HeldVoxelItem.Instance?.TriggerSwing();

                // 인벤토리에서 1개 소모
                _inventory.ConsumeSelected();
            }
        }

        // ═══════════════════════════════════════
        // 근접 공격
        // ═══════════════════════════════════════

        private void TryMeleeAttack()
        {
            if (_meleeTimer > 0) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

            // 구울 레이어에 레이캐스트
            if (!Physics.Raycast(ray, out RaycastHit hit, _meleeRange, _ghoulLayer))
                return;

            // 구울 컴포넌트 찾기 (자식에 있을 수 있으므로 부모도 체크)
            var ghoul = hit.collider.GetComponent<Ghoul.SimpleGhoul>();
            if (ghoul == null)
                ghoul = hit.collider.GetComponentInParent<Ghoul.SimpleGhoul>();
            if (ghoul == null || !ghoul.IsAlive)
                return;

            // 데미지 계산
            float damage = GetMeleeDamage();
            ghoul.TakeDamage(damage);

            // 쿨타임 적용
            _meleeTimer = _meleeCooldown;

            // 스윙 애니메이션
            HeldVoxelItem.Instance?.TriggerSwing();

            Debug.Log($"[Melee] Hit {ghoul.name} for {damage} damage!");
        }

        /// <summary>현재 들고 있는 도구에 따른 근접 데미지.</summary>
        private float GetMeleeDamage()
        {
            var item = _inventory?.GetSelectedItem();
            if (item == null || item.IsEmpty)
                return _handDamage;

            return item.Def.ToolType switch
            {
                ToolType.Knife => _knifeDamage,
                ToolType.Axe => _axeDamage,
                ToolType.Pickaxe => _pickaxeDamage,
                _ => _handDamage,
            };
        }

        private void ResetBreaking()
        {
            _isBreaking = false;
            _breakProgress = 0;
            _swingRepeatTimer = 0;
        }

        private void ShowHighlight(Vector3Int blockPos)
        {
            if (_selectionHighlight == null) return;
            _selectionHighlight.SetActive(true);
            _selectionHighlight.transform.position =
                new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
        }

        private void HideHighlight()
        {
            if (_selectionHighlight != null)
                _selectionHighlight.SetActive(false);
        }

        // ── 공개 API ──
        public float BreakProgressNormalized
        {
            get
            {
                if (!_isBreaking) return 0;
                BlockType t = _world.GetBlock(
                    _breakingBlockPos.x, _breakingBlockPos.y, _breakingBlockPos.z);
                float hardness = BlockData.Get(t).Hardness;
                return hardness > 0 ? _breakProgress / hardness : 0;
            }
        }
    }
}