// ── ChunkRenderer.cs ──
// 청크 렌더러. VoxelWorld가 생성하고 관리.
// [최적화] UpdateMeshOnly: 메시만 갱신 (빠름)
//          UpdateCollider: MeshCollider만 갱신 (무거움, 지연 호출)

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

            int layer = LayerMask.NameToLayer("Voxel");
            if (layer >= 0)
                gameObject.layer = layer;
        }

        /// <summary>메시 + 콜라이더 모두 갱신. 초기 로드용.</summary>
        public void UpdateMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                _meshFilter.sharedMesh = null;
                _meshCollider.sharedMesh = null;
                ChunkData.IsDirty = false;
                return;
            }

            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = mesh;
            ChunkData.IsDirty = false;
        }

        /// <summary>메시만 갱신 (콜라이더 제외). 프레임 드랍 방지.</summary>
        public void UpdateMeshOnly(Mesh mesh)
        {
            if (mesh == null)
            {
                _meshFilter.sharedMesh = null;
                ChunkData.IsDirty = false;
                return;
            }

            _meshFilter.sharedMesh = mesh;
            ChunkData.IsDirty = false;
        }

        /// <summary>MeshCollider만 갱신. 지연 호출용.</summary>
        public void UpdateCollider()
        {
            _meshCollider.sharedMesh = null; // 먼저 비워야 갱신됨
            _meshCollider.sharedMesh = _meshFilter.sharedMesh;
        }
    }
}
