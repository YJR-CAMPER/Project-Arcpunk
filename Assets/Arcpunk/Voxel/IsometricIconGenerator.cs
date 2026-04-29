// ── IsometricIconGenerator.cs ──
// 텍스처 아틀라스에서 블록의 3면(위/좌/우)을 아이소메트릭 투영하여
// 마인크래프트 스타일 3D 큐브 아이콘을 런타임 생성.
// 인벤토리 UI에서 블록 아이템의 아이콘으로 사용.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Voxel
{
    public class IsometricIconGenerator : MonoBehaviour
    {
        public static IsometricIconGenerator Instance { get; private set; }

        [Header("Atlas")]
        [Tooltip("블록 텍스처 아틀라스 (Read/Write 활성화 필요)")]
        [SerializeField] private Texture2D _atlas;
        [SerializeField] private int _atlasCols = 4;   // 아틀라스 가로 타일 수
        [SerializeField] private int _atlasRows = 4;    // 아틀라스 세로 타일 수

        [Header("Icon Settings")]
        [SerializeField] private int _iconSize = 64;    // 생성될 아이콘 크기 (px)

        [Header("Face Brightness")]
        [SerializeField] private float _topBright = 1.0f;
        [SerializeField] private float _leftBright = 0.75f;
        [SerializeField] private float _rightBright = 0.55f;

        // 캐시
        private Dictionary<BlockType, Sprite> _iconCache = new();
        private int _tileSize;

        private void Awake()
        {
            Instance = this;

            if (_atlas != null)
                _tileSize = _atlas.width / _atlasCols;
        }

        /// <summary>
        /// BlockType에 해당하는 아이소메트릭 아이콘 Sprite를 반환.
        /// 캐시에 없으면 생성.
        /// </summary>
        public Sprite GetIcon(BlockType type)
        {
            if (_iconCache.TryGetValue(type, out Sprite cached))
                return cached;

            var faceInfo = GetFaceUV(type);
            if (faceInfo == null) return null;

            Texture2D icon = GenerateIcon(faceInfo.Value);
            Sprite sprite = Sprite.Create(
                icon,
                new Rect(0, 0, _iconSize, _iconSize),
                new Vector2(0.5f, 0.5f),
                _iconSize);
            sprite.name = $"BlockIcon_{type}";

            _iconCache[type] = sprite;
            return sprite;
        }

        /// <summary>블록 타입별 아틀라스 좌표 매핑. 3면 동일하면 하나만 지정.</summary>
        private (Vector2Int top, Vector2Int left, Vector2Int right)? GetFaceUV(BlockType type)
        {
            // 아틀라스 그리드 좌표 (col, row). row 0 = 아틀라스 상단.
            // ※ BlockType enum에 맞게 수정 필요
            return type switch
            {
                BlockType.Stone       => (new(0,0), new(0,0), new(0,0)),
                BlockType.Dirt        => (new(1,0), new(1,0), new(1,0)),
                BlockType.DeadWood    => (new(3,0), new(2,0), new(2,0)), // 위=단면, 옆=측면
                BlockType.CopperOre   => (new(0,1), new(0,1), new(0,1)),
                BlockType.IronOre     => (new(1,1), new(1,1), new(1,1)),
                BlockType.MushroomBlock => (new(2,1), new(2,1), new(2,1)),
                BlockType.StoneBrick  => (new(3,1), new(3,1), new(3,1)),
                BlockType.CopperPlate => (new(0,2), new(0,2), new(0,2)),
                BlockType.Workbench   => (new(1,2), new(1,2), new(1,2)),
                BlockType.CopperBattery => (new(2,2), new(2,2), new(2,2)),
                BlockType.Sentry      => (new(3,2), new(3,2), new(3,2)),
                BlockType.CopperRod => (new(0,3), new(0,3), new(0,3)),
                BlockType.Light       => (new(1,3), new(1,3), new(1,3)),
                BlockType.CopperWire  => (new(2,3), new(2,3), new(2,3)),
                _ => null,
            };
        }

        // ═══════════════════════════════════════
        // 아이소메트릭 투영 렌더링
        // ═══════════════════════════════════════

        private Texture2D GenerateIcon((Vector2Int top, Vector2Int left, Vector2Int right) faces)
        {
            var icon = new Texture2D(_iconSize, _iconSize, TextureFormat.RGBA32, false);
            icon.filterMode = FilterMode.Point;

            // 투명으로 초기화
            var clearPixels = new Color32[_iconSize * _iconSize];
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = new Color32(0, 0, 0, 0);
            icon.SetPixels32(clearPixels);

            // 타일 픽셀 읽기
            Color[] topPixels = GetTilePixels(faces.top);
            Color[] leftPixels = GetTilePixels(faces.left);
            Color[] rightPixels = GetTilePixels(faces.right);

            float s = _iconSize;
            float hs = s * 0.45f;
            float cx = s / 2f;
            float cy = s * 0.65f;  // Y축 반전 (텍스처 좌표계는 하단이 0)

            int res = Mathf.Min(Mathf.FloorToInt(hs / 2f), 32);

            // 위 면 (가장 밝음)
            DrawTopFace(icon, topPixels, cx, cy, hs, res, _topBright);
            // 왼쪽 면
            DrawLeftFace(icon, leftPixels, cx, cy, hs, res, _leftBright);
            // 오른쪽 면
            DrawRightFace(icon, rightPixels, cx, cy, hs, res, _rightBright);

            icon.Apply();
            return icon;
        }

        private Color[] GetTilePixels(Vector2Int tileCoord)
        {
            // 아틀라스에서 tileCoord 위치의 타일 픽셀을 읽음
            // Unity 텍스처는 좌하단이 (0,0)이므로 row를 뒤집어야 함
            int flippedRow = (_atlasRows - 1) - tileCoord.y;
            int x = tileCoord.x * _tileSize;
            int y = flippedRow * _tileSize;

            return _atlas.GetPixels(x, y, _tileSize, _tileSize);
        }

        private Color SampleTile(Color[] pixels, float u, float v)
        {
            // u, v: 0~1 정규화 좌표
            int tx = Mathf.Clamp(Mathf.FloorToInt(u * _tileSize), 0, _tileSize - 1);
            int ty = Mathf.Clamp(Mathf.FloorToInt(v * _tileSize), 0, _tileSize - 1);
            // 타일 픽셀 배열은 좌하단→우상단 순서
            return pixels[ty * _tileSize + tx];
        }

        private void SetPixelSafe(Texture2D tex, int x, int y, Color col)
        {
            if (x >= 0 && x < _iconSize && y >= 0 && y < _iconSize)
                tex.SetPixel(x, y, col);
        }

        // ── 위 면 (다이아몬드) ──
        private void DrawTopFace(Texture2D tex, Color[] tile, float cx, float cy, float hs, int res, float bright)
        {
            for (int u = 0; u < res; u++)
            for (int v = 0; v < res; v++)
            {
                float nu = (float)u / res;
                float nv = (float)v / res;

                float px = cx + (nu - nv) * hs;
                float py = cy - (nu + nv) * hs * 0.5f + hs;

                Color col = SampleTile(tile, nu, nv) * bright;
                col.a = 1f;

                // 다이아몬드 셀 채우기
                float cellW = hs / res;
                float cellH = hs * 0.5f / res;
                FillDiamond(tex, px, py, cellW, cellH, col);
            }
        }

        // ── 왼쪽 면 (평행사변형) ──
        private void DrawLeftFace(Texture2D tex, Color[] tile, float cx, float cy, float hs, int res, float bright)
        {
            for (int u = 0; u < res; u++)
            for (int v = 0; v < res; v++)
            {
                float nu = (float)u / res;
                float nv = (float)v / res;

                float px = cx - hs + nu * hs;
                float py = cy - nu * hs * 0.5f - nv * hs;

                Color col = SampleTile(tile, nu, nv) * bright;
                col.a = 1f;

                float cellW = hs / res;
                float cellH = hs / res;
                FillParallelogramLeft(tex, px, py, cellW, cellH, hs, res, col);
            }
        }

        // ── 오른쪽 면 (평행사변형) ──
        private void DrawRightFace(Texture2D tex, Color[] tile, float cx, float cy, float hs, int res, float bright)
        {
            for (int u = 0; u < res; u++)
            for (int v = 0; v < res; v++)
            {
                float nu = (float)u / res;
                float nv = (float)v / res;

                float px = cx + nu * hs;
                float py = cy - hs + nu * hs * 0.5f - nv * hs;

                Color col = SampleTile(tile, nu, nv) * bright;
                col.a = 1f;

                float cellW = hs / res;
                float cellH = hs / res;
                FillParallelogramRight(tex, px, py, cellW, cellH, hs, res, col);
            }
        }

        // ── 도형 채우기 헬퍼 ──

        private void FillDiamond(Texture2D tex, float cx, float cy, float hw, float hh, Color col)
        {
            int minX = Mathf.FloorToInt(cx - hw);
            int maxX = Mathf.CeilToInt(cx + hw);
            int minY = Mathf.FloorToInt(cy - hh);
            int maxY = Mathf.CeilToInt(cy + hh);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = Mathf.Abs(x - cx) / hw;
                float dy = Mathf.Abs(y - cy) / hh;
                if (dx + dy <= 1.1f)
                    SetPixelSafe(tex, x, y, col);
            }
        }

        private void FillParallelogramLeft(Texture2D tex, float px, float py, float cw, float ch, float hs, int res, Color col)
        {
            float slopeY = hs * 0.5f / hs; // 0.5 rise per 1 run
            int steps = Mathf.CeilToInt(cw) + 2;
            int rows = Mathf.CeilToInt(ch) + 2;

            for (int dy = 0; dy < rows; dy++)
            for (int dx = 0; dx < steps; dx++)
            {
                int x = Mathf.FloorToInt(px) + dx;
                int y = Mathf.FloorToInt(py - dy + dx * slopeY);
                SetPixelSafe(tex, x, y, col);
            }
        }

        private void FillParallelogramRight(Texture2D tex, float px, float py, float cw, float ch, float hs, int res, Color col)
        {
            float slopeY = -hs * 0.5f / hs;
            int steps = Mathf.CeilToInt(cw) + 2;
            int rows = Mathf.CeilToInt(ch) + 2;

            for (int dy = 0; dy < rows; dy++)
            for (int dx = 0; dx < steps; dx++)
            {
                int x = Mathf.FloorToInt(px) + dx;
                int y = Mathf.FloorToInt(py - dy + dx * slopeY);
                SetPixelSafe(tex, x, y, col);
            }
        }

        // ═══════════════════════════════════════
        // 유틸
        // ═══════════════════════════════════════

        /// <summary>캐시 전부 클리어.</summary>
        public void ClearCache()
        {
            foreach (var kvp in _iconCache)
            {
                if (kvp.Value != null && kvp.Value.texture != null)
                    Destroy(kvp.Value.texture);
            }
            _iconCache.Clear();
        }

        private void OnDestroy()
        {
            ClearCache();
        }
    }
}
