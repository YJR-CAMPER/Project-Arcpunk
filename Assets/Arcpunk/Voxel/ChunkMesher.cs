// ── ChunkMesher.cs ──
// 면 컬링 메싱 + 꼭짓점 AO + 크로스 빌보드 서브메시.
// static 클래스 — VoxelWorld에서 ChunkMesher.GenerateMesh(chunk, GetBlock)로 호출.
// 그리디 메싱은 프로토타입에서 득보다 실이 크므로 면 컬링이 올바른 선택.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Voxel
{
    public static class ChunkMesher
    {
        // 6면 방향: Right(+X), Left(-X), Up(+Y), Down(-Y), Forward(+Z), Back(-Z)
        private static readonly Vector3Int[] FaceDirections =
        {
            new(1, 0, 0),   // 0: Right
            new(-1, 0, 0),  // 1: Left
            new(0, 1, 0),   // 2: Up
            new(0, -1, 0),  // 3: Down
            new(0, 0, 1),   // 4: Forward
            new(0, 0, -1),  // 5: Back
        };

        // 각 면의 4개 꼭짓점 오프셋 (Unity 좌표계에서 외향 법선 와인딩)
        private static readonly Vector3[][] FaceVertices =
        {
            // Right (+X) — 외향 법선 = +X
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            // Left (-X)
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
            // Up (+Y)
            new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            // Down (-Y)
            new[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
            // Forward (+Z)
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            // Back (-Z)
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
        };

        // AO 계산용: 각 면의 각 꼭짓점에 대해 [side1, side2] 방향
        // corner = side1 + side2
        private static readonly Vector3Int[][][] AONeighbors = BuildAONeighbors();

        // =====================================================
        //  공개 API
        // =====================================================

        /// <summary>
        /// 청크의 메시를 생성한다.
        /// 서브메시 0 = 블록 면 (블록 아틀라스), 서브메시 1 = 크로스 빌보드 (크로스 아틀라스).
        /// </summary>
        /// <param name="chunk">메싱할 청크</param>
        /// <param name="getWorldBlock">월드 좌표로 블록 조회하는 함수 (VoxelWorld.GetBlock)</param>
        public static Mesh GenerateMesh(Chunk chunk, Func<int, int, int, BlockType> getWorldBlock)
        {
            var vertices = new List<Vector3>(4096);
            var uvs = new List<Vector2>(4096);
            var colors = new List<Color>(4096);

            var blockTris = new List<int>(6144);  // 서브메시 0: 블록 면
            var crossTris = new List<int>(512);   // 서브메시 1: 크로스 빌보드

            int chunkWorldX = chunk.Coord.x * Chunk.SIZE;
            int chunkWorldY = chunk.Coord.y * Chunk.SIZE;
            int chunkWorldZ = chunk.Coord.z * Chunk.SIZE;

            int atlasSize = BlockData.AtlasSize; // 4×4
            float tileUV = 1f / atlasSize;
            float uvPad = 0.002f; // 블리딩 방지 패딩

            int crossCols = BlockData.CrossAtlasCols; // 2
            int crossRows = BlockData.CrossAtlasRows; // 1
            float crossTileU = 1f / crossCols;
            float crossTileV = 1f / crossRows;

            for (int lx = 0; lx < Chunk.SIZE; lx++)
                for (int ly = 0; ly < Chunk.SIZE; ly++)
                    for (int lz = 0; lz < Chunk.SIZE; lz++)
                    {
                        BlockType type = chunk.GetBlock(lx, ly, lz);
                        if (type == BlockType.Air) continue;

                        ref BlockDef def = ref BlockData.Defs[(ushort)type];

                        int wx = chunkWorldX + lx;
                        int wy = chunkWorldY + ly;
                        int wz = chunkWorldZ + lz;

                        // ── 크로스 빌보드 블록 (버섯, 피뢰침) ──
                        if (def.IsCross)
                        {
                            AddCrossBillboard(vertices, crossTris, uvs, colors,
                                lx, ly, lz, def.TexCross,
                                crossCols, crossRows, crossTileU, crossTileV);
                            continue; // 크로스 블록은 면 메싱 안 함
                        }

                        // 비고체 + 크로스 아닌 블록은 메싱 스킵 (Air, CopperWire 등)
                        if (!def.IsSolid) continue;

                        // ── 6면 컬링 메싱 ──
                        for (int d = 0; d < 6; d++)
                        {
                            int nx = wx + FaceDirections[d].x;
                            int ny = wy + FaceDirections[d].y;
                            int nz = wz + FaceDirections[d].z;

                            BlockType neighbor = getWorldBlock(nx, ny, nz);
                            ref BlockDef neighborDef = ref BlockData.Defs[(ushort)neighbor];

                            // 이웃이 투명하면 (Air이거나 비고체) 면 생성
                            if (neighborDef.IsTransparent)
                            {
                                int texIndex = GetTexForFace(ref def, d);
                                AddFace(vertices, blockTris, uvs, colors,
                                    lx, ly, lz, d, texIndex,
                                    atlasSize, tileUV, uvPad,
                                    chunk, getWorldBlock, chunkWorldX, chunkWorldY, chunkWorldZ);
                            }
                        }
                    }

            // ── 메시 조립 ──
            if (vertices.Count == 0)
                return null;

            Mesh mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);

            // 서브메시 설정
            bool hasCross = crossTris.Count > 0;
            mesh.subMeshCount = hasCross ? 2 : 1;
            mesh.SetTriangles(blockTris, 0);
            if (hasCross)
                mesh.SetTriangles(crossTris, 1);

            mesh.RecalculateNormals();
            return mesh;
        }

        // =====================================================
        //  면 추가 (AO 포함)
        // =====================================================

        private static void AddFace(
            List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols,
            int lx, int ly, int lz, int face, int texIndex,
            int atlasSize, float tileUV, float uvPad,
            Chunk chunk, Func<int, int, int, BlockType> getWorldBlock,
            int cwx, int cwy, int cwz)
        {
            int startIdx = verts.Count;
            Vector3 blockPos = new Vector3(lx, ly, lz);

            // 꼭짓점 4개
            Vector3[] fv = FaceVertices[face];
            verts.Add(blockPos + fv[0]);
            verts.Add(blockPos + fv[1]);
            verts.Add(blockPos + fv[2]);
            verts.Add(blockPos + fv[3]);

            // ── AO 계산 ──
            int wx = cwx + lx;
            int wy = cwy + ly;
            int wz = cwz + lz;

            int[] ao = new int[4];
            for (int v = 0; v < 4; v++)
            {
                ao[v] = CalculateVertexAO(
                    getWorldBlock, wx, wy, wz, face, v);
            }

            // AO → 색상 (0=가장 어두움, 3=가장 밝음)
            float[] aoColor = { AOToFloat(ao[0]), AOToFloat(ao[1]),
                                AOToFloat(ao[2]), AOToFloat(ao[3]) };

            cols.Add(new Color(aoColor[0], aoColor[0], aoColor[0]));
            cols.Add(new Color(aoColor[1], aoColor[1], aoColor[1]));
            cols.Add(new Color(aoColor[2], aoColor[2], aoColor[2]));
            cols.Add(new Color(aoColor[3], aoColor[3], aoColor[3]));

            // ── UV (아틀라스) ──
            int atlasX = texIndex % atlasSize;
            int atlasY = texIndex / atlasSize;
            // Unity UV: Y=0이 하단. 아틀라스가 좌상→우하로 배치되므로 Y 반전.
            float uMin = atlasX * tileUV + uvPad;
            float uMax = (atlasX + 1) * tileUV - uvPad;
            float vMax = 1f - (atlasY * tileUV) - uvPad;       // 상단
            float vMin = 1f - ((atlasY + 1) * tileUV) + uvPad; // 하단

            uvs.Add(new Vector2(uMin, vMin));
            uvs.Add(new Vector2(uMin, vMax));
            uvs.Add(new Vector2(uMax, vMax));
            uvs.Add(new Vector2(uMax, vMin));

            // ── 삼각형 (AO 기반 대각선 플립) ──
            // ao[0]+ao[2] vs ao[1]+ao[3] 비교로 어두운 대각선 방지
            if (ao[0] + ao[2] > ao[1] + ao[3])
            {
                tris.Add(startIdx + 0);
                tris.Add(startIdx + 1);
                tris.Add(startIdx + 2);
                tris.Add(startIdx + 0);
                tris.Add(startIdx + 2);
                tris.Add(startIdx + 3);
            }
            else
            {
                tris.Add(startIdx + 1);
                tris.Add(startIdx + 2);
                tris.Add(startIdx + 3);
                tris.Add(startIdx + 1);
                tris.Add(startIdx + 3);
                tris.Add(startIdx + 0);
            }
        }

        // =====================================================
        //  AO 계산 (0fps.net 표준 알고리즘)
        // =====================================================

        /// <summary>
        /// vertexAO(side1, side2, corner):
        ///   side1, side2 모두 불투명이면 0 (가장 어두움)
        ///   아니면 3 - (side1 + side2 + corner)
        /// </summary>
        private static int CalculateVertexAO(
            Func<int, int, int, BlockType> getWorldBlock,
            int wx, int wy, int wz, int face, int vertex)
        {
            Vector3Int[] neighbors = AONeighbors[face][vertex];
            Vector3Int s1Dir = neighbors[0];
            Vector3Int s2Dir = neighbors[1];

            // 면의 법선 방향으로 한 칸 나간 위치에서 체크
            Vector3Int normal = FaceDirections[face];
            int bx = wx + normal.x;
            int by = wy + normal.y;
            int bz = wz + normal.z;

            int side1 = IsOpaqueAt(getWorldBlock, bx + s1Dir.x, by + s1Dir.y, bz + s1Dir.z) ? 1 : 0;
            int side2 = IsOpaqueAt(getWorldBlock, bx + s2Dir.x, by + s2Dir.y, bz + s2Dir.z) ? 1 : 0;
            int corner = IsOpaqueAt(getWorldBlock,
                bx + s1Dir.x + s2Dir.x,
                by + s1Dir.y + s2Dir.y,
                bz + s1Dir.z + s2Dir.z) ? 1 : 0;

            if (side1 == 1 && side2 == 1)
                return 0;

            return 3 - (side1 + side2 + corner);
        }

        private static bool IsOpaqueAt(Func<int, int, int, BlockType> getWorldBlock,
            int wx, int wy, int wz)
        {
            BlockType type = getWorldBlock(wx, wy, wz);
            return type != BlockType.Air && BlockData.IsSolid(type);
        }

        private static float AOToFloat(int ao)
        {
            // 0→0.4, 1→0.6, 2→0.8, 3→1.0
            return 0.4f + ao * 0.2f;
        }

        // =====================================================
        //  크로스 빌보드 (X자 쿼드 2장, 마크 꽃 스타일)
        // =====================================================

        private static void AddCrossBillboard(
            List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols,
            int lx, int ly, int lz, int crossTexIndex,
            int crossCols, int crossRows, float tileU, float tileV)
        {
            // X자: 대각선 두 장 (0.5, 0, 0.5) 중심
            float cx = lx + 0.5f;
            float cz = lz + 0.5f;
            float half = 0.45f; // 0.5보다 살짝 작게 → 블록 경계 관통 방지

            // UV 계산 (크로스 아틀라스)
            int atlasX = crossTexIndex % crossCols;
            int atlasY = crossTexIndex / crossCols;
            float pad = 0.005f;
            float uMin = atlasX * tileU + pad;
            float uMax = (atlasX + 1) * tileU - pad;
            float vMin = 1f - ((atlasY + 1) * tileV) + pad;
            float vMax = 1f - (atlasY * tileV) - pad;

            Color white = Color.white;

            // ── 쿼드 A: 대각선 (/방향) — 앞면 + 뒷면 ──
            AddCrossQuad(verts, tris, uvs, cols,
                new Vector3(cx - half, ly, cz - half),
                new Vector3(cx - half, ly + 1, cz - half),
                new Vector3(cx + half, ly + 1, cz + half),
                new Vector3(cx + half, ly, cz + half),
                uMin, uMax, vMin, vMax, white);

            // ── 쿼드 B: 대각선 (\방향) — 앞면 + 뒷면 ──
            AddCrossQuad(verts, tris, uvs, cols,
                new Vector3(cx + half, ly, cz - half),
                new Vector3(cx + half, ly + 1, cz - half),
                new Vector3(cx - half, ly + 1, cz + half),
                new Vector3(cx - half, ly, cz + half),
                uMin, uMax, vMin, vMax, white);
        }

        /// <summary>양면 쿼드 추가 (앞면 + 뒷면).</summary>
        private static void AddCrossQuad(
            List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            float uMin, float uMax, float vMin, float vMax, Color color)
        {
            int idx = verts.Count;

            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);

            uvs.Add(new Vector2(uMin, vMin));
            uvs.Add(new Vector2(uMin, vMax));
            uvs.Add(new Vector2(uMax, vMax));
            uvs.Add(new Vector2(uMax, vMin));

            cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);

            // 앞면
            tris.Add(idx + 0); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx + 0); tris.Add(idx + 2); tris.Add(idx + 3);
            // 뒷면
            tris.Add(idx + 2); tris.Add(idx + 1); tris.Add(idx + 0);
            tris.Add(idx + 3); tris.Add(idx + 2); tris.Add(idx + 0);
        }

        // =====================================================
        //  텍스처 인덱스 (면별 top/side/bottom)
        // =====================================================

        private static int GetTexForFace(ref BlockDef def, int direction)
        {
            return direction switch
            {
                2 => def.TexTop,     // Up
                3 => def.TexBottom,  // Down
                _ => def.TexSide,    // 나머지 4면
            };
        }

        // =====================================================
        //  AO 이웃 방향 테이블 빌드
        // =====================================================

        /// <summary>
        /// 각 면(6)의 각 꼭짓점(4)에 대해 AO 체크할 [side1, side2] 방향 생성.
        /// corner = side1 + side2 방향.
        /// </summary>
        private static Vector3Int[][][] BuildAONeighbors()
        {
            var result = new Vector3Int[6][][];

            // 면별 로컬 U, V 축 정의
            // 각 면의 FaceVertices 순서와 일치하도록 축 방향 설정
            Vector3Int[][] faceAxes =
            {
                // Right (+X): U=+Z, V=+Y
                new[] { new Vector3Int(0,0,1), new Vector3Int(0,1,0) },
                // Left (-X): U=-Z, V=+Y
                new[] { new Vector3Int(0,0,-1), new Vector3Int(0,1,0) },
                // Up (+Y): U=+X, V=+Z
                new[] { new Vector3Int(1,0,0), new Vector3Int(0,0,1) },
                // Down (-Y): U=+X, V=-Z
                new[] { new Vector3Int(1,0,0), new Vector3Int(0,0,-1) },
                // Forward (+Z): U=-X, V=+Y
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,1,0) },
                // Back (-Z): U=+X, V=+Y
                new[] { new Vector3Int(1,0,0), new Vector3Int(0,1,0) },
            };

            // 꼭짓점 순서에 따른 side1, side2 부호
            // 꼭짓점 0: (-U, -V), 1: (-U, +V), 2: (+U, +V), 3: (+U, -V)
            int[][] signs =
            {
                new[] { -1, -1 }, // vertex 0
                new[] { -1,  1 }, // vertex 1
                new[] {  1,  1 }, // vertex 2
                new[] {  1, -1 }, // vertex 3
            };

            for (int face = 0; face < 6; face++)
            {
                result[face] = new Vector3Int[4][];
                Vector3Int uAxis = faceAxes[face][0];
                Vector3Int vAxis = faceAxes[face][1];

                for (int vert = 0; vert < 4; vert++)
                {
                    int su = signs[vert][0];
                    int sv = signs[vert][1];

                    Vector3Int side1 = new Vector3Int(uAxis.x * su, uAxis.y * su, uAxis.z * su);
                    Vector3Int side2 = new Vector3Int(vAxis.x * sv, vAxis.y * sv, vAxis.z * sv);

                    result[face][vert] = new[] { side1, side2 };
                }
            }

            return result;
        }
    }
}