// ── VoxelSpriteCache.cs ──
// 아이템 아이콘 → 복셀 메시 캐시.
// 같은 아이콘의 메시를 반복 생성하지 않도록 캐싱.
// HeldItemDisplay, ItemEntity 등에서 사용.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Voxel
{
    public class VoxelSpriteCache : MonoBehaviour
    {
        public static VoxelSpriteCache Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int _depth = 1;              // 복셀 두께 (1~3)
        [SerializeField] private float _pixelSize = 0.0625f;  // 복셀 크기 (1/16 = 16px 아이콘이 1유닛)

        [Header("Item Icon Atlas")]
        [Tooltip("개별 아이템 아이콘 텍스처들. ItemType 순서에 맞게 배치.")]
        [SerializeField] private Texture2D[] _itemIcons;

        // 캐시된 메시 (키: 텍스처 instanceID)
        private Dictionary<int, Mesh> _meshCache = new();

        // 버텍스 컬러 머티리얼 (모든 복셀 스프라이트가 공유)
        private Material _voxelSpriteMaterial;

        public Material SharedMaterial => _voxelSpriteMaterial;

        private void Awake()
        {
            Instance = this;
            CreateMaterial();
        }

        /// <summary>텍스처에서 복셀 메시를 가져온다. 캐시에 없으면 생성.</summary>
        public Mesh GetMesh(Texture2D icon)
        {
            if (icon == null) return null;

            int key = icon.GetInstanceID();
            if (_meshCache.TryGetValue(key, out Mesh cached))
                return cached;

            Mesh mesh = VoxelSpriteMesher.Generate(icon, _depth, _pixelSize);
            _meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>ItemType 인덱스로 복셀 메시를 가져온다.</summary>
        public Mesh GetMeshByIndex(int index)
        {
            if (_itemIcons == null || index < 0 || index >= _itemIcons.Length)
                return null;

            var icon = _itemIcons[index];
            if (icon == null) return null;

            return GetMesh(icon);
        }

        /// <summary>아이콘 텍스처를 가져온다.</summary>
        public Texture2D GetIcon(int index)
        {
            if (_itemIcons == null || index < 0 || index >= _itemIcons.Length)
                return null;
            return _itemIcons[index];
        }

        /// <summary>캐시를 전부 비운다 (아이콘 교체 시).</summary>
        public void ClearCache()
        {
            foreach (var mesh in _meshCache.Values)
            {
                if (mesh != null) Destroy(mesh);
            }
            _meshCache.Clear();
        }

        private void CreateMaterial()
        {
            // 커스텀 버텍스 컬러 셰이더 사용
            var shader = Shader.Find("Arcpunk/VoxelSprite");
            if (shader == null)
            {
                Debug.LogWarning("[VoxelSpriteCache] Arcpunk/VoxelSprite 셰이더를 찾을 수 없습니다. " +
                                 "VoxelSprite.shader를 프로젝트에 추가하세요. 폴백으로 Standard 사용.");
                shader = Shader.Find("Standard");
            }

            _voxelSpriteMaterial = new Material(shader);
            _voxelSpriteMaterial.name = "VoxelSpriteMaterial";
            _voxelSpriteMaterial.color = Color.white;
        }

        private void OnDestroy()
        {
            ClearCache();
            if (_voxelSpriteMaterial != null)
                Destroy(_voxelSpriteMaterial);
        }
    }
}
