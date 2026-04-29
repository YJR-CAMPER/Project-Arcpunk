// ── VoxelSpriteMesher.cs ──
// 2D 텍스처(아이템 아이콘)를 복셀 스프라이트 3D 메시로 변환.
// 빈티지 스토리 방식: 각 불투명 픽셀 → 1개의 복셀 큐브.
// 인접 픽셀 간 면 컬링으로 최적화.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Voxel
{
    public static class VoxelSpriteMesher
    {
        /// <summary>
        /// 텍스처에서 복셀 스프라이트 메시를 생성한다.
        /// 각 불투명 픽셀이 하나의 복셀 큐브가 된다.
        /// </summary>
        /// <param name="texture">원본 아이콘 텍스처 (Read/Write 활성화 필요)</param>
        /// <param name="depth">Z축 두께 (픽셀 단위). 1이면 1복셀 두께, 2면 2복셀.</param>
        /// <param name="pixelSize">복셀 하나의 월드 크기. 0.0625f = 1/16 (16px 아이콘이 1유닛).</param>
        /// <returns>생성된 메시. 버텍스 컬러로 색상 적용됨.</returns>
        public static Mesh Generate(Texture2D texture, int depth = 1, float pixelSize = 0.0625f)
        {
            int w = texture.width;
            int h = texture.height;

            // 픽셀 데이터 읽기
            Color32[] pixels = texture.GetPixels32();
            bool[,,] solid = new bool[w, h, depth];

            // 불투명 픽셀 맵 생성
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool opaque = pixels[y * w + x].a > 128;
                for (int z = 0; z < depth; z++)
                    solid[x, y, z] = opaque;
            }

            // 메시 데이터
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color32>();

            // 중심 오프셋 (메시 피벗을 중앙 하단으로)
            float ox = -w * 0.5f * pixelSize;
            float oy = 0f; // 하단 정렬
            float oz = -depth * 0.5f * pixelSize;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            for (int z = 0; z < depth; z++)
            {
                if (!solid[x, y, z]) continue;

                Color32 col = pixels[y * w + x];
                Vector3 pos = new Vector3(
                    ox + x * pixelSize,
                    oy + y * pixelSize,
                    oz + z * pixelSize);

                // 6방향 면 검사 — 인접 복셀이 없는 면만 생성
                // +X
                if (x == w - 1 || !solid[x + 1, y, z])
                    AddFace(vertices, triangles, colors, pos, pixelSize, col, 0);
                // -X
                if (x == 0 || !solid[x - 1, y, z])
                    AddFace(vertices, triangles, colors, pos, pixelSize, col, 1);
                // +Y
                if (y == h - 1 || !solid[x, y + 1, z])
                    AddFace(vertices, triangles, colors, pos, pixelSize, col, 2);
                // -Y
                if (y == 0 || !solid[x, y - 1, z])
                    AddFace(vertices, triangles, colors, pos, pixelSize, col, 3);
                // +Z
                if (z == depth - 1 || !solid[x, y, z + 1])
                    AddFace(vertices, triangles, colors, pos, pixelSize, col, 4);
                // -Z
                if (z == 0 || !solid[x, y, z - 1])
                    AddFace(vertices, triangles, colors, pos, pixelSize, col, 5);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.name = $"VoxelSprite_{texture.name}";

            return mesh;
        }

        // 면 방향별 버텍스 오프셋
        private static readonly Vector3[][] _faceVerts = {
            // +X
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            // -X
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
            // +Y
            new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            // -Y
            new[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
            // +Z
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            // -Z
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
        };

        private static void AddFace(
            List<Vector3> verts, List<int> tris, List<Color32> colors,
            Vector3 pos, float size, Color32 col, int face)
        {
            int vi = verts.Count;

            // 면의 방향에 따라 약간의 명도 차이 (가짜 조명)
            Color32 shadedCol = ShadeFace(col, face);

            var fv = _faceVerts[face];
            for (int i = 0; i < 4; i++)
            {
                verts.Add(pos + fv[i] * size);
                colors.Add(shadedCol);
            }

            tris.Add(vi);     tris.Add(vi + 1); tris.Add(vi + 2);
            tris.Add(vi);     tris.Add(vi + 2); tris.Add(vi + 3);
        }

        /// <summary>면 방향에 따라 명도를 조절하여 입체감 부여.</summary>
        private static Color32 ShadeFace(Color32 col, int face)
        {
            float mult = face switch
            {
                2 => 1.0f,    // +Y (위) — 가장 밝음
                0 => 0.9f,    // +X
                4 => 0.85f,   // +Z
                5 => 0.8f,    // -Z
                1 => 0.75f,   // -X
                3 => 0.65f,   // -Y (아래) — 가장 어두움
                _ => 0.85f,
            };

            return new Color32(
                (byte)Mathf.Min(col.r * mult, 255),
                (byte)Mathf.Min(col.g * mult, 255),
                (byte)Mathf.Min(col.b * mult, 255),
                col.a);
        }
    }
}
