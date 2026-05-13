// ── HeldVoxelItem.cs ──
// 마인크래프트 스타일 1인칭 손에 든 아이템.
// 텍스처를 양면 쿼드에 그대로 표시.
// 피벗은 하단(손잡이 위치)에 고정.
// CameraHolder 자식 오브젝트로 배치.

using UnityEngine;

namespace Arcpunk.Voxel
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HeldVoxelItem : MonoBehaviour
    {
        public static HeldVoxelItem Instance { get; private set; }

        [Header("Position & Rotation (1인칭 손 위치)")]
        [SerializeField] private Vector3 _holdPosition = new(0.4f, -0.35f, 0.5f);
        [SerializeField] private Vector3 _holdRotation = new(10f, -25f, -15f);
        [SerializeField] private float _holdScale = 0.4f;

        [Header("Edit Mode (씬 뷰에서 직접 조절)")]
        [Tooltip("체크하면 씬 뷰에서 기즈모로 위치/회전/스케일 조절 가능. 조절 후 Apply 버튼을 누르세요.")]
        [SerializeField] private bool _editMode = false;
        [Tooltip("현재 Transform 값을 Hold Position/Rotation/Scale에 저장")]
        [SerializeField] private bool _applyCurrentTransform = false;

        [Header("Swing Animation")]
        [SerializeField] private float _swingSpeed = 10f;
        [SerializeField] private float _swingScale = 1f;

        [Header("Swing Curves (Inspector에서 곡선 편집)")]
        [Tooltip("X축 회전 (위아래 휘두르기). -1=위로, +1=아래로")]
        [SerializeField] private AnimationCurve _swingCurveX = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, -0.4f),    // 준비: 뒤로 들어올림
            new Keyframe(0.5f, 1f),         // 타격: 아래로 내려침
            new Keyframe(1f, 0f)            // 복귀
        );
        [Tooltip("Z축 회전 (좌우 기울기)")]
        [SerializeField] private AnimationCurve _swingCurveZ = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 0.1f),
            new Keyframe(0.5f, -0.1f),
            new Keyframe(1f, 0f)
        );
        [Tooltip("Y 위치 오프셋 (위아래 이동)")]
        [SerializeField] private AnimationCurve _swingOffsetY = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 0.06f),     // 들어올림
            new Keyframe(0.5f, -0.05f),     // 내려침
            new Keyframe(1f, 0f)
        );
        [Tooltip("Z 위치 오프셋 (앞뒤 이동)")]
        [SerializeField] private AnimationCurve _swingOffsetZ = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, -0.04f),    // 뒤로
            new Keyframe(0.5f, 0.08f),      // 앞으로 찌름
            new Keyframe(1f, 0f)
        );

        [Header("Muzzle Point (총구)")]
        [SerializeField] private Vector3 _muzzleOffset = new(0f, 1.5f, 0.1f);

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Texture2D _currentIcon;
        private Material _itemMaterial;
        private float _swingTimer;
        private bool _swinging;
        private Transform _muzzleTransform;

        /// <summary>총구 위치. CombatSystem에서 참조.</summary>
        public Transform MuzzlePoint => _muzzleTransform;

        private void Awake()
        {
            Instance = this;
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.enabled = false;
            CreateMaterial();
            CreateMuzzlePoint();
        }

        public void ShowItem(Texture2D icon)
        {
            if (icon == null) { Hide(); return; }
            if (icon == _currentIcon && _meshRenderer.enabled) return;

            _currentIcon = icon;
            _meshFilter.mesh = CreateQuadMesh(icon);
            _meshRenderer.material = _itemMaterial;
            _itemMaterial.mainTexture = icon;
            _meshRenderer.enabled = true;

            if (!_editMode)
            {
                transform.localPosition = _holdPosition;
                transform.localEulerAngles = _holdRotation;
                transform.localScale = Vector3.one * _holdScale;
            }
        }

        public void Hide()
        {
            _meshRenderer.enabled = false;
            _currentIcon = null;
        }

        public void TriggerSwing()
        {
            _swinging = true;
            _swingTimer = 0f;
        }

        private void Update()
        {
            if (!_meshRenderer.enabled) return;

            // Apply 버튼: 현재 Transform 값을 저장
            if (_applyCurrentTransform)
            {
                _applyCurrentTransform = false;
                _holdPosition = transform.localPosition;
                _holdRotation = transform.localEulerAngles;
                _holdScale = transform.localScale.x;
                Debug.Log($"[HeldVoxelItem] Applied! Pos={_holdPosition} Rot={_holdRotation} Scale={_holdScale}");
            }

            // Edit Mode면 Transform 안 건드림 → 씬 뷰에서 자유롭게 조절 가능
            if (_editMode) return;

            if (_swinging)
            {
                _swingTimer += Time.deltaTime * _swingSpeed;
                float t = Mathf.Clamp01(_swingTimer);

                // AnimationCurve에서 값 읽기 (-1~1 범위 → 각도/거리에 매핑)
                float rotX = _swingCurveX.Evaluate(t) * _swingScale * 60f;
                float rotZ = _swingCurveZ.Evaluate(t) * _swingScale * 30f;
                float offY = _swingOffsetY.Evaluate(t) * _swingScale;
                float offZ = _swingOffsetZ.Evaluate(t) * _swingScale;

                transform.localEulerAngles = _holdRotation + new Vector3(rotX, 0, rotZ);
                transform.localPosition = _holdPosition + new Vector3(0, offY, offZ);

                if (_swingTimer >= 1f)
                {
                    _swinging = false;
                    transform.localEulerAngles = _holdRotation;
                    transform.localPosition = _holdPosition;
                }
            }
            else
            {
                // 아이들 밥
                float bobY = Mathf.Sin(Time.time * 1.5f) * 0.008f;
                float bobX = Mathf.Sin(Time.time * 0.8f) * 0.004f;
                transform.localPosition = _holdPosition + new Vector3(bobX, bobY, 0);
            }
        }

        // ═══════════════════════════════════════
        // 메시: 앞뒤 양면 쿼드
        // ═══════════════════════════════════════

        /// <summary>
        /// 마크 스타일 아이템 메시. 양면 쿼드.
        /// 피벗은 하단 중앙 (손잡이 위치).
        /// </summary>
        private Mesh CreateQuadMesh(Texture2D tex)
        {
            // 텍스처 비율 유지
            float aspect = (float)tex.width / tex.height;
            float w = aspect;
            float h = 1f;

            // 피벗: 하단 중앙 (0, 0) → 손잡이 위치
            Vector3[] verts = {
                // 앞면
                new(-w*0.5f, 0, 0),       // 좌하
                new(-w*0.5f, h, 0),       // 좌상
                new( w*0.5f, h, 0),       // 우상
                new( w*0.5f, 0, 0),       // 우하
                // 뒷면 (반전)
                new( w*0.5f, 0, 0),
                new( w*0.5f, h, 0),
                new(-w*0.5f, h, 0),
                new(-w*0.5f, 0, 0),
            };

            Vector2[] uvs = {
                new(0,0), new(0,1), new(1,1), new(1,0),
                new(0,0), new(0,1), new(1,1), new(1,0),
            };

            int[] tris = {
                0,1,2, 0,2,3,   // 앞면
                4,5,6, 4,6,7,   // 뒷면
            };

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.name = "HeldItemQuad";
            return mesh;
        }

        private void CreateMaterial()
        {
            var shader = Shader.Find("Standard");
            _itemMaterial = new Material(shader);
            _itemMaterial.name = "HeldItemMaterial";

            // Cutout (투명 배경 제거)
            _itemMaterial.SetFloat("_Mode", 1);
            _itemMaterial.SetFloat("_Cutoff", 0.5f);
            _itemMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _itemMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            _itemMaterial.SetInt("_ZWrite", 1);
            _itemMaterial.EnableKeyword("_ALPHATEST_ON");
            _itemMaterial.DisableKeyword("_ALPHABLEND_ON");
            _itemMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _itemMaterial.renderQueue = 2450;
        }

        private void CreateMuzzlePoint()
        {
            var go = new GameObject("MuzzlePoint");
            go.transform.SetParent(transform);
            go.transform.localPosition = _muzzleOffset;
            go.transform.localRotation = Quaternion.identity;
            _muzzleTransform = go.transform;
        }
    }
}
