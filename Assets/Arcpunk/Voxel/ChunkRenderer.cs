// ── ChunkRenderer.cs ──
// 청크의 시각적 표현을 담당하는 MonoBehaviour.
// VoxelWorld가 생성하고 관리한다.

using UnityEngine;

namespace Arcpunk.Voxel
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class ChunkRenderer : MonoBehaviour
    {
        public Chunk ChunkData { get; private set; }

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();
        }

        /// <summary>청크 데이터를 할당하고 위치를 설정한다.</summary>
        public void Initialize(Chunk chunk, Material material)
        {
            ChunkData = chunk;
            _meshRenderer.material = material;
            transform.position = new Vector3(
                chunk.Coord.x * Chunk.SIZE,
                chunk.Coord.y * Chunk.SIZE,
                chunk.Coord.z * Chunk.SIZE
            );
            gameObject.name = $"Chunk_{chunk.Coord}";

            // 레이어 설정 — "Voxel" 레이어가 없으면 경고 후 Default 사용
            int voxelLayer = LayerMask.NameToLayer("Voxel");
            if (voxelLayer >= 0)
                gameObject.layer = voxelLayer;
            else
                Debug.LogWarning("[ChunkRenderer] 'Voxel' layer not found. " +
                    "Edit → Project Settings → Tags and Layers에서 추가하세요.");
        }

        /// <summary>메시를 갱신한다. VoxelWorld에서 더티 청크에 대해 호출.</summary>
        public void UpdateMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                _meshFilter.sharedMesh = null;
                _meshCollider.sharedMesh = null;
                return;
            }

            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = mesh;
            ChunkData.IsDirty = false;
        }
    }
}
