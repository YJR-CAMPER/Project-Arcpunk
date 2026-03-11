// ── Chunk.cs ──
// 16×16×16 복셀 청크. 월드의 기본 단위.
// 1차원 배열로 저장하여 캐시 효율 극대화.
// Chunk는 순수 데이터 컨테이너 — MonoBehaviour 아님.

using UnityEngine;

namespace Arcpunk.Voxel
{
    public class Chunk
    {
        public const int SIZE = 16;
        public const int VOLUME = SIZE * SIZE * SIZE;

        /// <summary>청크 좌표 (복셀 좌표 아님). 복셀 좌표 = Coord * SIZE + local</summary>
        public Vector3Int Coord { get; }

        /// <summary>블록 타입 배열. 인덱스는 Index(x,y,z)로 계산.</summary>
        public ushort[] Blocks { get; }

        /// <summary>메시가 재생성되어야 하는지 여부.</summary>
        public bool IsDirty { get; set; }

        public Chunk(Vector3Int coord)
        {
            Coord = coord;
            Blocks = new ushort[VOLUME];
            IsDirty = true;
        }

        /// <summary>3D 로컬 좌표 → 1D 배열 인덱스. x + z*SIZE + y*SIZE*SIZE</summary>
        public static int Index(int x, int y, int z)
        {
            return x + z * SIZE + y * SIZE * SIZE;
        }

        /// <summary>로컬 좌표로 블록 조회. 범위 밖이면 Air 반환.</summary>
        public BlockType GetBlock(int x, int y, int z)
        {
            if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
                return BlockType.Air;
            return (BlockType)Blocks[Index(x, y, z)];
        }

        /// <summary>로컬 좌표에 블록 설정. 범위 밖이면 무시.</summary>
        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
                return;
            Blocks[Index(x, y, z)] = (ushort)type;
            IsDirty = true;
        }

        /// <summary>월드 좌표 → 청크 좌표 변환. 음수 좌표도 올바르게 처리.</summary>
        public static Vector3Int WorldToChunkCoord(int wx, int wy, int wz)
        {
            return new Vector3Int(
                Mathf.FloorToInt((float)wx / SIZE),
                Mathf.FloorToInt((float)wy / SIZE),
                Mathf.FloorToInt((float)wz / SIZE)
            );
        }

        /// <summary>월드 좌표 → 로컬 좌표 변환. 음수 좌표도 올바르게 처리.</summary>
        public static Vector3Int WorldToLocal(int wx, int wy, int wz)
        {
            return new Vector3Int(
                ((wx % SIZE) + SIZE) % SIZE,
                ((wy % SIZE) + SIZE) % SIZE,
                ((wz % SIZE) + SIZE) % SIZE
            );
        }
    }
}
