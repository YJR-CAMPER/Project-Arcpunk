// ── ChunkMesher.cs ──
// 면 컬링 메싱: 보이는 면만 생성 + 꼭짓점 AO.
// 그리디 메싱보다 정점 수가 많지만 구현이 단순하고 확실하게 작동한다.
// 프로토타입 완성 후 그리디 메싱으로 업그레이드 가능.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Voxel
{
    public static class ChunkMesher
    {
        // 6면 방향: Right, Left, Up, Down, Forward, Back
        private static readonly Vector3Int[] FaceDirections =
        {
            new(1, 0, 0),   // 0: Right  (+X)
            new(-1, 0, 0),  // 1: Left   (-X)
            new(0, 1, 0),   // 2: Up     (+Y)
            new(0, -1, 0),  // 3: Down   (-Y)
            new(0, 0, 1),   // 4: Forward(+Z)
            new(0, 0, -1),  // 5: Back   (-Z)
        };

        // 각 면의 4개 꼭짓점 오프셋 (Unity 좌표계에서 외향 법선을 생성하는 순서)
        private static readonly Vector3[][] FaceVertices = new Vector3[][]
        {
            // Right (+X)
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

        // AO 계산용: 각 면의 각 꼭짓점에 대해 체크할 이웃 2방향
        // [면][꼭짓점][0=side1, 1=side2]
        // side1, side2, corner(=side1+side2) 세 이웃으로 AO 레벨 결정
        private static readonly Vector3Int[][][] AONeighbors = BuildAONeighbors();

        /// <summary>
        /// 청크의 메시를 생성한다.
        /// </summary>
        /// <param name="chunk">메싱할 청크</param>
        /// <param name="getWorldBlock">월드 좌표로 블록을 조회하는 함수 (청크 경계 처리용)</param>
        /// <returns>생성된 Mesh</returns>
        public static Mesh GenerateMesh(Chunk chunk, System.Func<int, int, int, BlockType> getWorldBlock)
        {
            var vertices  = new List<Vector3>();
            var triangles = new List<int>();
            var uvs       = new List<Vector2>();
            var colors    = new List<Color>(); // AO를 Color 채널에 저장

            int atlasSize = BlockData.AtlasSize;
            float tileSize = 1f / atlasSize;

            for (int y = 0; y < Chunk.SIZE; y++)
            for (int z = 0; z < Chunk.SIZE; z++)
            for (int x = 0; x < Chunk.SIZE; x++)
            {
                BlockType type = chunk.GetBlock(x, y, z);
                if (type == BlockType.Air) continue;

                ref BlockDef def = ref BlockData.Defs[(ushort)type];
                if (!def.IsSolid) continue; // 비고체(배선 등)는 메싱 스킵

                // 월드 좌표
                int wx = chunk.Coord.x * Chunk.SIZE + x;
                int wy = chunk.Coord.y * Chunk.SIZE + y;
                int wz = chunk.Coord.z * Chunk.SIZE + z;

                // 6면 각각에 대해
                for (int face = 0; face < 6; face++)
                {
                    Vector3Int dir = FaceDirections[face];
                    int nx = wx + dir.x;
                    int ny = wy + dir.y;
                    int nz = wz + dir.z;

                    // 이웃이 투명(공기, 배선 등)이면 이 면을 그린다
                    BlockType neighbor = getWorldBlock(nx, ny, nz);
                    if (!BlockData.IsTransparent(neighbor)) continue;

                    // 텍스처 인덱스 결정 (상/하/옆)
                    int texIndex = face switch
                    {
                        2 => def.TexTop,
                        3 => def.TexBottom,
                        _ => def.TexSide,
                    };

                    // 아틀라스 UV 계산
                    int texCol = texIndex % atlasSize;
                    int texRow = texIndex / atlasSize;
                    // UV 원점은 좌하단. 행은 위에서부터 세므로 뒤집기.
                    float u0 = texCol * tileSize;
                    float v0 = 1f - (texRow + 1) * tileSize;
                    float u1 = u0 + tileSize;
                    float v1 = v0 + tileSize;

                    // UV에 약간의 패딩 (텍스처 블리딩 방지)
                    float pad = 0.001f;
                    u0 += pad; v0 += pad;
                    u1 -= pad; v1 -= pad;

                    Vector2[] faceUVs = {
                        new(u0, v0), new(u0, v1), new(u1, v1), new(u1, v0)
                    };

                    // AO 계산
                    float[] ao = new float[4];
                    for (int v = 0; v < 4; v++)
                    {
                        ao[v] = CalculateAO(wx, wy, wz, face, v, getWorldBlock);
                    }

                    // 정점 추가
                    int vertStart = vertices.Count;
                    for (int v = 0; v < 4; v++)
                    {
                        vertices.Add(new Vector3(x, y, z) + FaceVertices[face][v]);
                        uvs.Add(faceUVs[v]);

                        // AO를 Color.r에 저장 (셰이더에서 곱셈)
                        float aoValue = ao[v];
                        colors.Add(new Color(aoValue, aoValue, aoValue, 1f));
                    }

                    // 삼각형: AO 기반 대각선 선택 (AO flip 방지)
                    // AO가 대각선에서 불균형이면 삼각형 분할 방향을 바꿔서
                    // 어둠이 대각선으로 번지는 아티팩트를 방지
                    if (ao[0] + ao[2] > ao[1] + ao[3])
                    {
                        triangles.Add(vertStart + 0);
                        triangles.Add(vertStart + 1);
                        triangles.Add(vertStart + 2);
                        triangles.Add(vertStart + 0);
                        triangles.Add(vertStart + 2);
                        triangles.Add(vertStart + 3);
                    }
                    else
                    {
                        triangles.Add(vertStart + 1);
                        triangles.Add(vertStart + 2);
                        triangles.Add(vertStart + 3);
                        triangles.Add(vertStart + 1);
                        triangles.Add(vertStart + 3);
                        triangles.Add(vertStart + 0);
                    }
                }
            }

            if (vertices.Count == 0) return null;

            Mesh mesh = new Mesh();
            mesh.name = $"Chunk_{chunk.Coord}";

            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// 꼭짓점 AO 계산. 0~1 (0=가장 어두움, 1=밝음).
        /// 이웃 블록 3개(side1, side2, corner)를 체크.
        /// </summary>
        private static float CalculateAO(
            int wx, int wy, int wz,
            int face, int vertex,
            System.Func<int, int, int, BlockType> getWorldBlock)
        {
            var neighbors = AONeighbors[face][vertex];
            Vector3Int s1Dir = neighbors[0];
            Vector3Int s2Dir = neighbors[1];

            int bx = wx + FaceDirections[face].x;
            int by = wy + FaceDirections[face].y;
            int bz = wz + FaceDirections[face].z;

            bool side1 = BlockData.IsSolid(getWorldBlock(bx + s1Dir.x, by + s1Dir.y, bz + s1Dir.z));
            bool side2 = BlockData.IsSolid(getWorldBlock(bx + s2Dir.x, by + s2Dir.y, bz + s2Dir.z));
            bool corner = BlockData.IsSolid(getWorldBlock(
                bx + s1Dir.x + s2Dir.x,
                by + s1Dir.y + s2Dir.y,
                bz + s1Dir.z + s2Dir.z));

            int aoLevel;
            if (side1 && side2)
                aoLevel = 0; // 양쪽 막힘 → 가장 어두움
            else
                aoLevel = 3 - ((side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0));

            // 0→0.2, 1→0.5, 2→0.8, 3→1.0
            return aoLevel switch
            {
                0 => 0.2f,
                1 => 0.5f,
                2 => 0.8f,
                _ => 1.0f,
            };
        }

        /// <summary>
        /// AO 이웃 방향 테이블 구축.
        /// 각 면(6)의 각 꼭짓점(4)에 대해 체크할 두 방향 [side1, side2].
        /// corner = side1 + side2.
        /// </summary>
        private static Vector3Int[][][] BuildAONeighbors()
        {
            var table = new Vector3Int[6][][];

            // Right (+X) face: 면의 법선이 +X. 면 위의 축은 Y, Z.
            table[0] = new[]
            {
                new[] { new Vector3Int(0,-1,0), new Vector3Int(0,0,-1) }, // v0: (1,0,0)
                new[] { new Vector3Int(0, 1,0), new Vector3Int(0,0,-1) }, // v1: (1,1,0)
                new[] { new Vector3Int(0, 1,0), new Vector3Int(0,0, 1) }, // v2: (1,1,1)
                new[] { new Vector3Int(0,-1,0), new Vector3Int(0,0, 1) }, // v3: (1,0,1)
            };

            // Left (-X)
            table[1] = new[]
            {
                new[] { new Vector3Int(0,-1,0), new Vector3Int(0,0, 1) }, // v0: (0,0,1)
                new[] { new Vector3Int(0, 1,0), new Vector3Int(0,0, 1) }, // v1: (0,1,1)
                new[] { new Vector3Int(0, 1,0), new Vector3Int(0,0,-1) }, // v2: (0,1,0)
                new[] { new Vector3Int(0,-1,0), new Vector3Int(0,0,-1) }, // v3: (0,0,0)
            };

            // Up (+Y)
            table[2] = new[]
            {
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,0,-1) }, // v0: (0,1,0)
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,0, 1) }, // v1: (0,1,1)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0,0, 1) }, // v2: (1,1,1)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0,0,-1) }, // v3: (1,1,0)
            };

            // Down (-Y)
            table[3] = new[]
            {
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,0, 1) }, // v0: (0,0,1)
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,0,-1) }, // v1: (0,0,0)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0,0,-1) }, // v2: (1,0,0)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0,0, 1) }, // v3: (1,0,1)
            };

            // Forward (+Z)
            table[4] = new[]
            {
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0,-1,0) }, // v0: (1,0,1)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0, 1,0) }, // v1: (1,1,1)
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0, 1,0) }, // v2: (0,1,1)
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,-1,0) }, // v3: (0,0,1)
            };

            // Back (-Z)
            table[5] = new[]
            {
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0,-1,0) }, // v0: (0,0,0)
                new[] { new Vector3Int(-1,0,0), new Vector3Int(0, 1,0) }, // v1: (0,1,0)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0, 1,0) }, // v2: (1,1,0)
                new[] { new Vector3Int( 1,0,0), new Vector3Int(0,-1,0) }, // v3: (1,0,0)
            };

            return table;
        }
    }
}
