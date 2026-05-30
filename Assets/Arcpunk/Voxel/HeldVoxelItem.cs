// ── HeldVoxelItem.cs ──
// 마인크래프트 스타일 1인칭 손에 든 아이템.
// 텍스처를 양면 쿼드에 그대로 표시.
// 피벗은 하단(손잡이 위치)에 고정.
// CameraHolder 자식 오브젝트로 배치.
// ★ 아이템 타입(도구/총)에 따라 hold transform 분리.

using UnityEngine;

namespace Arcpunk.Voxel
{
    /// <summary>아이템을 손에 들 때의 타입. hold transform이 달라짐.</summary>
    public enum HeldItemType
    {
        Tool,   // 곡괭이, 도끼, 삽 등 — 아래쪽 손잡이 기준
        Gun     // 총기류 — 총구가 앞을 향하도록
    }

    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HeldVoxelItem : MonoBehaviour
    {
        public static HeldVoxelItem Instance { get; private set; }

        // ─── Tool Hold (곡괭이·도끼 등) ───
        // ★ 기존 필드 이름 유지 → Unity Inspector 직렬화 값 보존
        [Header("Tool Hold (도구류)")]
        [SerializeField] private Vector3 _holdPosition = new(0.4f, -0.35f, 0.5f);
        [SerializeField] private Vector3 _holdRotation = new(10f, -25f, -15f);
        [SerializeField] private float _holdScale = 0.4f;

        // ─── Gun Hold (총기류) ───
        [Header("Gun Hold (총기류)")]
        [SerializeField] private Vector3 _gunPosition = new(0.35f, -0.25f, 0.45f);
        [SerializeField] private Vector3 _gunRotation = new(0f, -10f, 0f);
        [SerializeField] private float _gunScale = 0.4f;

        [Header("Edit Mode (씬 뷰에서 직접 조절)")]
        [Tooltip("체크하면 씬 뷰에서 기즈모로 위치/회전/스케일 조절 가능. 조절 후 Apply 버튼을 누르세요.")]
        [SerializeField] private bool _editMode = false;
        [Tooltip("현재 Transform 값을 현재 타입의 Hold 설정에 저장")]
        [SerializeField] private bool _applyCurrentTransform = false;

        [Header("Swing Animation")]
        [SerializeField] private float _swingSpeed = 10f;
        [SerializeField] private float _swingScale = 1f;

        [Header("Swing Curves (Inspector에서 곡선 편집)")]
        [Tooltip("X축 회전 (위아래 휘두르기). -1=위로, +1=아래로")]
        [SerializeField]
        private AnimationCurve _swingCurveX = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, -0.4f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );
        [Tooltip("Z축 회전 (좌우 기울기)")]
        [SerializeField]
        private AnimationCurve _swingCurveZ = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 0.1f),
            new Keyframe(0.5f, -0.1f),
            new Keyframe(1f, 0f)
        );
        [Tooltip("Y 위치 오프셋 (위아래 이동)")]
        [SerializeField]
        private AnimationCurve _swingOffsetY = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 0.06f),
            new Keyframe(0.5f, -0.05f),
            new Keyframe(1f, 0f)
        );
        [Tooltip("Z 위치 오프셋 (앞뒤 이동)")]
        [SerializeField]
        private AnimationCurve _swingOffsetZ = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, -0.04f),
            new Keyframe(0.5f, 0.08f),
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

        // ★ 현재 들고 있는 아이템 타입
        private HeldItemType _currentType = HeldItemType.Tool;

        /// <summary>총구 위치. CombatSystem에서 참조.</summary>
        public Transform MuzzlePoint => _muzzleTransform;

        /// <summary>현재 타입에 맞는 hold position</summary>
        private Vector3 ActivePosition => _currentType == HeldItemType.Gun ? _gunPosition : _holdPosition;
        /// <summary>현재 타입에 맞는 hold rotation</summary>
        private Vector3 ActiveRotation => _currentType == HeldItemType.Gun ? _gunRotation : _holdRotation;
        /// <summary>현재 타입에 맞는 hold scale</summary>
        private float ActiveScale => _currentType == HeldItemType.Gun ? _gunScale : _holdScale;

        private void Awake()
        {
            Instance = this;
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.enabled = false;
            CreateMaterial();
            CreateMuzzlePoint();
        }

        /// <summary>
        /// 아이템을 손에 표시한다.
        /// type을 생략하면 기존처럼 Tool로 동작 (하위 호환).
        /// </summary>
        public void ShowItem(Texture2D icon, HeldItemType type = HeldItemType.Tool)
        {
            if (icon == null) { Hide(); return; }

            // 같은 아이콘 + 같은 타입이면 스킵
            if (icon == _currentIcon && type == _currentType && _meshRenderer.enabled) return;

            _currentIcon = icon;
            _currentType = type;

            _meshFilter.mesh = CreateQuadMesh(icon);
            _meshRenderer.material = _itemMaterial;
            _itemMaterial.mainTexture = icon;
            _meshRenderer.enabled = true;

            if (!_editMode)
            {
                transform.localPosition = ActivePosition;
                transform.localEulerAngles = ActiveRotation;
                transform.localScale = Vector3.one * ActiveScale;
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

            // Apply 버튼: 현재 Transform 값을 현재 타입 설정에 저장
            if (_applyCurrentTransform)
            {
                _applyCurrentTransform = false;
                if (_currentType == HeldItemType.Gun)
                {
                    _gunPosition = transform.localPosition;
                    _gunRotation = transform.localEulerAngles;
                    _gunScale = transform.localScale.x;
                    Debug.Log($"[HeldVoxelItem] Applied GUN! Pos={_gunPosition} Rot={_gunRotation} Scale={_gunScale}");
                }
                else
                {
                    _holdPosition = transform.localPosition;
                    _holdRotation = transform.localEulerAngles;
                    _holdScale = transform.localScale.x;
                    Debug.Log($"[HeldVoxelItem] Applied TOOL! Pos={_holdPosition} Rot={_holdRotation} Scale={_holdScale}");
                }
            }

            // Edit Mode면 Transform 안 건드림 → 씬 뷰에서 자유롭게 조절 가능
            if (_editMode) return;

            if (_swinging)
            {
                _swingTimer += Time.deltaTime * _swingSpeed;
                float t = Mathf.Clamp01(_swingTimer);

                float rotX = _swingCurveX.Evaluate(t) * _swingScale * 60f;
                float rotZ = _swingCurveZ.Evaluate(t) * _swingScale * 30f;
                float offY = _swingOffsetY.Evaluate(t) * _swingScale;
                float offZ = _swingOffsetZ.Evaluate(t) * _swingScale;

                transform.localEulerAngles = ActiveRotation + new Vector3(rotX, 0, rotZ);
                transform.localPosition = ActivePosition + new Vector3(0, offY, offZ);

                if (_swingTimer >= 1f)
                {
                    _swinging = false;
                    transform.localEulerAngles = ActiveRotation;
                    transform.localPosition = ActivePosition;
                }
            }
            else
            {
                // 아이들 밥
                float bobY = Mathf.Sin(Time.time * 1.5f) * 0.008f;
                float bobX = Mathf.Sin(Time.time * 0.8f) * 0.004f;
                transform.localPosition = ActivePosition + new Vector3(bobX, bobY, 0);
            }
        }

        // ═══════════════════════════════════════
        // 메시: 앞뒤 양면 쿼드
        // ═══════════════════════════════════════

        private Mesh CreateQuadMesh(Texture2D tex)
        {
            float aspect = (float)tex.width / tex.height;
            float w = aspect;
            float h = 1f;

            Vector3[] verts = {
                new(-w*0.5f, 0, 0),
                new(-w*0.5f, h, 0),
                new( w*0.5f, h, 0),
                new( w*0.5f, 0, 0),
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
                0,1,2, 0,2,3,
                4,5,6, 4,6,7,
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
            // ★ Unlit/Transparent Cutout — 빌드 시 Standard _ALPHATEST_ON 스트리핑 방지
            var shader = Shader.Find("Unlit/Transparent Cutout");
            if (shader == null)
            {
                // 폴백: 만약 못 찾으면 Sprites/Default (항상 포함됨)
                shader = Shader.Find("Sprites/Default");
                Debug.LogWarning("[HeldVoxelItem] Unlit/Transparent Cutout not found, using Sprites/Default");
            }
            _itemMaterial = new Material(shader);
            _itemMaterial.name = "HeldItemMaterial";
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