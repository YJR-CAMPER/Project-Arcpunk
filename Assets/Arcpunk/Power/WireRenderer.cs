// ── WireRenderer.cs ──
// 와이어 연결마다 LineRenderer를 생성하여 카테너리 곡선 케이블을 표시.
// WireManager의 OnWireAdded/OnWireRemoved 이벤트를 구독하여 자동 동기화.
//
// 카테너리 곡선: 실제 전선이 중력에 의해 늘어지는 자연스러운 형태.
// cosh 함수 기반으로 거리가 길수록 처짐이 커짐.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Power
{
    public class WireRenderer : MonoBehaviour
    {
        public static WireRenderer Instance { get; private set; }

        // ── 설정 ──
        [Header("Cable Appearance")]
        [SerializeField] private int _segmentCount = 14;
        [SerializeField] private float _cableWidth = 0.04f;
        [SerializeField] private Color _cableColor = new Color(0.72f, 0.45f, 0.2f, 1f); // 구리색
        [SerializeField] private Color _poweredColor = new Color(0.3f, 0.75f, 1f, 1f);  // 전력 공급 시
        [SerializeField] private float _sagFactor = 0.12f;        // 처짐 강도 배수
        [SerializeField] private float _minSag = 0.3f;            // 최소 처짐량
        [SerializeField] private float _maxSag = 5f;              // 최대 처짐량

        [Header("Preview")]
        [SerializeField] private Color _previewColor = new Color(1f, 1f, 0.5f, 0.5f);

        // ── 내부 데이터 ──
        // wireId → 렌더링 오브젝트
        private Dictionary<int, CableInstance> _cables = new();
        private Material _cableMaterial;
        private Material _poweredMaterial;

        // 프리뷰 라인 (WireTool에서 사용)
        private LineRenderer _previewLine;

        // =====================================================
        //  구조체
        // =====================================================

        private struct CableInstance
        {
            public GameObject Object;
            public LineRenderer Line;
            public Vector3 PointA;
            public Vector3 PointB;
        }

        // =====================================================
        //  초기화
        // =====================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateMaterials();
            CreatePreviewLine();
        }

        private void Start()
        {
            var wm = WireManager.Instance;
            if (wm != null)
            {
                wm.OnWireAdded += OnWireAdded;
                wm.OnWireRemoved += OnWireRemoved;
            }
        }

        private void OnDestroy()
        {
            var wm = WireManager.Instance;
            if (wm != null)
            {
                wm.OnWireAdded -= OnWireAdded;
                wm.OnWireRemoved -= OnWireRemoved;
            }
        }

        private void CreateMaterials()
        {
            // 기본 케이블 머티리얼
            _cableMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            if (_cableMaterial != null)
            {
                _cableMaterial.SetColor("_Color", _cableColor);
            }

            // 전력 공급 시 머티리얼
            _poweredMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            if (_poweredMaterial != null)
            {
                _poweredMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _poweredMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                _poweredMaterial.SetColor("_Color", _poweredColor);
            }
        }

        private void CreatePreviewLine()
        {
            var obj = new GameObject("WirePreview");
            obj.transform.SetParent(transform, false);

            _previewLine = obj.AddComponent<LineRenderer>();
            _previewLine.positionCount = _segmentCount;
            _previewLine.startWidth = _cableWidth * 1.2f;
            _previewLine.endWidth = _cableWidth * 1.2f;
            _previewLine.useWorldSpace = true;
            _previewLine.enabled = false;

            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat != null)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetColor("_Color", _previewColor);
            }
            _previewLine.material = mat;
        }

        // =====================================================
        //  이벤트 핸들러
        // =====================================================

        private void OnWireAdded(WireData wire)
        {
            CreateCable(wire);
        }

        private void OnWireRemoved(WireData wire)
        {
            DestroyCable(wire.WireId);
        }

        // =====================================================
        //  케이블 생성 / 삭제
        // =====================================================

        private void CreateCable(WireData wire)
        {
            if (_cables.ContainsKey(wire.WireId)) return;

            var obj = new GameObject($"Cable_{wire.WireId}");
            obj.transform.SetParent(transform, false);

            var lr = obj.AddComponent<LineRenderer>();
            lr.positionCount = _segmentCount;
            lr.startWidth = _cableWidth;
            lr.endWidth = _cableWidth;
            lr.useWorldSpace = true;
            lr.material = _cableMaterial;

            // 블록 중앙 좌표
            Vector3 a = BlockCenter(wire.BlockA);
            Vector3 b = BlockCenter(wire.BlockB);

            var cable = new CableInstance
            {
                Object = obj,
                Line = lr,
                PointA = a,
                PointB = b,
            };

            ApplyCatenary(lr, a, b);
            _cables[wire.WireId] = cable;
        }

        private void DestroyCable(int wireId)
        {
            if (_cables.TryGetValue(wireId, out var cable))
            {
                if (cable.Object != null)
                    Destroy(cable.Object);
                _cables.Remove(wireId);
            }
        }

        // =====================================================
        //  카테너리 곡선 계산
        // =====================================================

        /// <summary>from→to 사이에 자연스러운 처짐 곡선을 적용.</summary>
        private void ApplyCatenary(LineRenderer lr, Vector3 from, Vector3 to)
        {
            float distance = Vector3.Distance(from, to);
            float sag = Mathf.Clamp(distance * _sagFactor, _minSag, _maxSag);

            lr.positionCount = _segmentCount;

            for (int i = 0; i < _segmentCount; i++)
            {
                float t = i / (float)(_segmentCount - 1);
                Vector3 pos = Vector3.Lerp(from, to, t);

                // 카테너리 근사: -4 * sag * t * (1 - t)
                // t=0, t=1에서 0, t=0.5에서 최대 처짐
                float catenary = -4f * sag * t * (1f - t);
                pos.y += catenary;

                lr.SetPosition(i, pos);
            }
        }

        // =====================================================
        //  프리뷰 (WireTool 연동)
        // =====================================================

        /// <summary>블록A에서 마우스 위치까지 프리뷰 케이블 표시.</summary>
        public void ShowPreview(Vector3 from, Vector3 to)
        {
            if (_previewLine == null) return;

            _previewLine.enabled = true;
            ApplyCatenary(_previewLine, from, to);
        }

        /// <summary>프리뷰 케이블 숨기기.</summary>
        public void HidePreview()
        {
            if (_previewLine != null)
                _previewLine.enabled = false;
        }

        // =====================================================
        //  전력 상태 시각 피드백
        // =====================================================

        /// <summary>전력 상태에 따라 케이블 색상 업데이트.
        /// VoxelPowerSystem 틱 후 호출.</summary>
        public void UpdatePoweredVisuals()
        {
            var powerSys = VoxelPowerSystem.Instance;
            var wm = WireManager.Instance;
            if (powerSys == null || wm == null) return;

            foreach (var kvp in _cables)
            {
                int wireId = kvp.Key;
                var cable = kvp.Value;
                if (cable.Line == null) continue;

                // 와이어 양쪽 끝 중 하나라도 전력 공급이면 powered 색상
                if (wm.AllWires.TryGetValue(wireId, out var wire))
                {
                    bool powered = powerSys.IsBlockPowered(wire.BlockA)
                                || powerSys.IsBlockPowered(wire.BlockB);
                    cable.Line.material = powered ? _poweredMaterial : _cableMaterial;
                }
            }
        }

        // =====================================================
        //  월드 로드 시 전체 복원
        // =====================================================

        /// <summary>WireManager 로드 후 모든 케이블을 렌더링.</summary>
        public void RebuildAllCables()
        {
            // 기존 케이블 전부 정리
            foreach (var cable in _cables.Values)
            {
                if (cable.Object != null)
                    Destroy(cable.Object);
            }
            _cables.Clear();

            // WireManager에서 전체 와이어를 가져와 생성
            var wm = WireManager.Instance;
            if (wm == null) return;

            foreach (var kvp in wm.AllWires)
            {
                CreateCable(kvp.Value);
            }

            Debug.Log($"[WireRenderer] {_cables.Count}개 케이블 렌더링 복원 완료.");
        }

        // =====================================================
        //  유틸
        // =====================================================

        private static Vector3 BlockCenter(Vector3Int pos)
        {
            return new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        }
    }
}