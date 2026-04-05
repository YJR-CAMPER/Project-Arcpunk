// ── WorkbenchInteraction.cs ──
// 작업대 블록을 우클릭하면 날빗기 UI를 여는 시스템.
// BlockInteraction에서 블록 설치 대신 작업대 상호작용을 우선 처리.
// Player 오브젝트에 부착.

using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.UI;

namespace Arcpunk.Crafting
{
    public class WorkbenchInteraction : MonoBehaviour
    {
        [SerializeField] private float _reachDistance = 6f;
        [SerializeField] private LayerMask _voxelLayer;

        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            // 날빗기 UI가 열려있으면 다른 조작 차단
            if (KnappingUI.Instance != null && KnappingUI.Instance.IsOpen)
                return;

            // 우클릭 시 작업대인지 체크
            if (Input.GetMouseButtonDown(1))
            {
                TryInteractWorkbench();
            }
        }

        private void TryInteractWorkbench()
        {
            if (_cam == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _voxelLayer))
                return;

            // 바라보고 있는 블록 확인
            Vector3 blockInside = hit.point - hit.normal * 0.01f;
            Vector3Int blockPos = new(
                Mathf.FloorToInt(blockInside.x),
                Mathf.FloorToInt(blockInside.y),
                Mathf.FloorToInt(blockInside.z)
            );

            var world = VoxelWorld.Instance;
            if (world == null) return;

            BlockType blockType = world.GetBlock(blockPos.x, blockPos.y, blockPos.z);

            if (blockType == BlockType.Workbench)
            {
                // 작업대! → 날빗기 UI 열기
                KnappingUI.Instance?.Open();
            }
        }
    }
}
