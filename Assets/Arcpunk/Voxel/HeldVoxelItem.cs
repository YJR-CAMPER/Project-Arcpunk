// ── HeldVoxelItem.cs ──
// 플레이어 손에 들린 아이템을 복셀 스프라이트로 렌더링.
// 기존 HeldBlockDisplay와 병행 — 블록은 HeldBlockDisplay, 도구는 HeldVoxelItem.
// 카메라의 자식 오브젝트로 배치하여 1인칭 시점에서 보이게 함.

using UnityEngine;

namespace Arcpunk.Voxel
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HeldVoxelItem : MonoBehaviour
    {
        public static HeldVoxelItem Instance { get; private set; }

        [Header("Position & Rotation (1인칭 손 위치)")]
        [SerializeField] private Vector3 _holdPosition = new(0.4f, -0.3f, 0.6f);
        [SerializeField] private Vector3 _holdRotation = new(0f, 45f, 0f);
        [SerializeField] private float _holdScale = 2.5f;

        [Header("Swing Animation")]
        [SerializeField] private float _swingSpeed = 8f;
        [SerializeField] private float _swingAngle = 40f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Texture2D _currentIcon;
        private float _swingTimer;
        private bool _swinging;

        private void Awake()
        {
            Instance = this;
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.enabled = false;
        }

        /// <summary>
        /// 들고 있는 아이템을 복셀 스프라이트로 표시.
        /// icon이 null이면 숨김.
        /// </summary>
        public void ShowItem(Texture2D icon)
        {
            if (icon == null)
            {
                Hide();
                return;
            }

            // 같은 아이콘이면 메시 재생성 안 함
            if (icon == _currentIcon && _meshRenderer.enabled)
                return;

            _currentIcon = icon;

            var cache = VoxelSpriteCache.Instance;
            if (cache == null)
            {
                Debug.LogWarning("[HeldVoxelItem] VoxelSpriteCache not found!");
                return;
            }

            _meshFilter.mesh = cache.GetMesh(icon);
            _meshRenderer.material = cache.SharedMaterial;
            _meshRenderer.enabled = true;

            // 위치·회전·스케일 적용
            transform.localPosition = _holdPosition;
            transform.localEulerAngles = _holdRotation;
            transform.localScale = Vector3.one * _holdScale;
        }

        /// <summary>아이템 숨기기.</summary>
        public void Hide()
        {
            _meshRenderer.enabled = false;
            _currentIcon = null;
        }

        /// <summary>공격/채굴 시 스윙 애니메이션 트리거.</summary>
        public void TriggerSwing()
        {
            _swinging = true;
            _swingTimer = 0f;
        }

        private void Update()
        {
            if (!_meshRenderer.enabled) return;

            if (_swinging)
            {
                _swingTimer += Time.deltaTime * _swingSpeed;

                // 사인파로 자연스러운 스윙
                float swing = Mathf.Sin(_swingTimer * Mathf.PI) * _swingAngle;

                transform.localEulerAngles = _holdRotation + new Vector3(swing, 0f, 0f);

                if (_swingTimer >= 1f)
                {
                    _swinging = false;
                    transform.localEulerAngles = _holdRotation;
                }
            }

            // 미세한 아이들 밥(bob) 모션
            float bob = Mathf.Sin(Time.time * 1.5f) * 0.01f;
            transform.localPosition = _holdPosition + new Vector3(0f, bob, 0f);
        }
    }
}
