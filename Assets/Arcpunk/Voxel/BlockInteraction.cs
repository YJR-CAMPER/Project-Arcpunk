// ── BlockInteraction.cs ──
// 플레이어의 블록 파괴/설치 입력 처리.
// 화면 중앙 레이캐스트로 블록 선택, 좌클릭=파괴, 우클릭=설치.
// 플레이어 오브젝트에 부착.

using UnityEngine;

namespace Arcpunk.Voxel
{
    public class BlockInteraction : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _reachDistance = 6f;
        [SerializeField] private LayerMask _voxelLayer;

        [Header("Block Selection")]
        [SerializeField] private BlockType _selectedBlock = BlockType.StoneBrick;

        [Header("Visual")]
        [SerializeField] private GameObject _selectionHighlight; // 선택 블록 하이라이트 (큐브 와이어프레임)

        private VoxelWorld _world;
        private Camera _cam;

        // 파괴 진행
        private float _breakProgress;
        private Vector3Int _breakingBlockPos;
        private bool _isBreaking;

        // 핫바 (숫자키로 블록 전환)
        private readonly BlockType[] _hotbar = new[]
        {
            BlockType.StoneBrick,
            BlockType.CopperPlate,
            BlockType.WoodRod,
            BlockType.CopperRod,
            BlockType.CopperWire,
            BlockType.CopperBattery,
            BlockType.Light,
            BlockType.Sentry,
            BlockType.Workbench,
        };
        private int _hotbarIndex;

        private void Start()
        {
            _world = VoxelWorld.Instance;
            _cam = Camera.main;
            _selectedBlock = _hotbar[0];

            if (_selectionHighlight != null)
                _selectionHighlight.SetActive(false);
        }

        private void Update()
        {
            HandleHotbarInput();
            HandleBlockInteraction();
        }

        private void HandleHotbarInput()
        {
            for (int i = 0; i < _hotbar.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _hotbarIndex = i;
                    _selectedBlock = _hotbar[i];
                    Debug.Log($"[BlockInteraction] Selected: {BlockData.Get(_selectedBlock).Name}");
                }
            }

            // 마우스 휠로도 전환
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                _hotbarIndex = (_hotbarIndex + (scroll > 0 ? -1 : 1) + _hotbar.Length) % _hotbar.Length;
                _selectedBlock = _hotbar[_hotbarIndex];
            }
        }

        private void HandleBlockInteraction()
        {
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _voxelLayer))
            {
                ResetBreaking();
                HideHighlight();
                return;
            }

            // ── 블록 좌표 계산 ──
            // 히트 지점에서 법선 반대로 살짝 밀어서 블록 내부 좌표를 얻음
            Vector3 blockInside = hit.point - hit.normal * 0.01f;
            Vector3Int targetBlock = new(
                Mathf.FloorToInt(blockInside.x),
                Mathf.FloorToInt(blockInside.y),
                Mathf.FloorToInt(blockInside.z)
            );

            // 설치할 위치는 히트 면의 바깥쪽
            Vector3 blockOutside = hit.point + hit.normal * 0.01f;
            Vector3Int placeBlock = new(
                Mathf.FloorToInt(blockOutside.x),
                Mathf.FloorToInt(blockOutside.y),
                Mathf.FloorToInt(blockOutside.z)
            );

            // 하이라이트 표시
            ShowHighlight(targetBlock);

            // ── 좌클릭: 파괴 ──
            if (Input.GetMouseButton(0))
            {
                BlockType targetType = _world.GetBlock(targetBlock.x, targetBlock.y, targetBlock.z);
                if (targetType == BlockType.Air) return;

                ref BlockDef def = ref BlockData.Get(targetType);

                // 새로운 블록을 파괴 시작하면 진행 리셋
                if (!_isBreaking || _breakingBlockPos != targetBlock)
                {
                    _breakingBlockPos = targetBlock;
                    _breakProgress = 0;
                    _isBreaking = true;
                }

                _breakProgress += Time.deltaTime;

                if (_breakProgress >= def.Hardness)
                {
                    // 블록 파괴!
                    _world.SetBlock(targetBlock.x, targetBlock.y, targetBlock.z, BlockType.Air);
                    _world.RebuildDirtyChunks();
                    ResetBreaking();

                    // TODO: 아이템 드롭, 자극 발생 등
                }
            }
            else
            {
                ResetBreaking();
            }

            // ── 우클릭: 설치 ──
            if (Input.GetMouseButtonDown(1))
            {
                // 설치 위치에 이미 블록이 있으면 무시
                BlockType existing = _world.GetBlock(placeBlock.x, placeBlock.y, placeBlock.z);
                if (existing != BlockType.Air) return;

                // 플레이어와 겹치지 않는지 확인
                // (간단한 체크: 플레이어 발 위치와 머리 위치의 블록)
                Vector3Int playerFeet = VoxelWorld.WorldPosToBlockCoord(transform.position);
                Vector3Int playerHead = VoxelWorld.WorldPosToBlockCoord(
                    transform.position + Vector3.up * 1.5f);

                if (placeBlock == playerFeet || placeBlock == playerHead)
                    return; // 자기 자신 안에 블록 설치 방지

                _world.SetBlock(placeBlock.x, placeBlock.y, placeBlock.z, _selectedBlock);
                _world.RebuildDirtyChunks();
            }
        }

        private void ResetBreaking()
        {
            _isBreaking = false;
            _breakProgress = 0;
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
        public BlockType SelectedBlock => _selectedBlock;
        public float BreakProgress => _isBreaking ? _breakProgress : 0;
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
        public int HotbarIndex => _hotbarIndex;
    }
}
