// ── DeadTreeGenerator.cs ──
// 죽은 나무 생성기. 단순 기둥이 아닌 가지가 뻗는 다양한 형태.
// VoxelWorld.GenerateStructures()에서 호출.
//
// 5가지 변형:
//   0: 그루터기 (2~3블록, 가지 없음)
//   1: 외줄기 나무 (4~6블록, 가지 1~2개)
//   2: 갈래 나무 (중간에서 Y자로 분기)
//   3: 기울어진 나무 (비스듬히 자람)
//   4: 고목 (굵은 줄기 + 많은 가지, 드묾)

using UnityEngine;
using System;

namespace Arcpunk.Voxel
{
    public static class DeadTreeGenerator
    {
        /// <summary>
        /// 지정 위치에 죽은 나무를 생성한다.
        /// </summary>
        /// <param name="setBlock">블록 배치 함수</param>
        /// <param name="wx">줄기 바닥 월드 X</param>
        /// <param name="surfaceY">지표면 Y</param>
        /// <param name="wz">줄기 바닥 월드 Z</param>
        /// <param name="seed">이 나무의 시드 (위치 기반 해시)</param>
        public static void Generate(Action<int, int, int, BlockType> setBlock,
            int wx, int surfaceY, int wz, int seed)
        {
            // 위치 기반 결정론적 랜덤
            System.Random rng = new System.Random(seed);

            // 변형 선택 (가중치)
            int roll = rng.Next(100);
            int variant;
            if (roll < 25)      variant = 0; // 25% 그루터기
            else if (roll < 55) variant = 1; // 30% 외줄기
            else if (roll < 75) variant = 2; // 20% 갈래
            else if (roll < 90) variant = 3; // 15% 기울어짐
            else                variant = 4; // 10% 고목

            switch (variant)
            {
                case 0: GenerateStump(setBlock, wx, surfaceY, wz, rng); break;
                case 1: GenerateSingleTrunk(setBlock, wx, surfaceY, wz, rng); break;
                case 2: GenerateForked(setBlock, wx, surfaceY, wz, rng); break;
                case 3: GenerateLeaning(setBlock, wx, surfaceY, wz, rng); break;
                case 4: GenerateAncient(setBlock, wx, surfaceY, wz, rng); break;
            }
        }

        // ═══════════════════════════════════════
        // 변형 0: 그루터기
        // ═══════════════════════════════════════
        private static void GenerateStump(Action<int, int, int, BlockType> set,
            int x, int sy, int z, System.Random rng)
        {
            int height = 2 + rng.Next(2); // 2~3
            for (int dy = 1; dy <= height; dy++)
                set(x, sy + dy, z, BlockType.DeadWood);
        }

        // ═══════════════════════════════════════
        // 변형 1: 외줄기 + 가지 1~2개
        // ═══════════════════════════════════════
        private static void GenerateSingleTrunk(Action<int, int, int, BlockType> set,
            int x, int sy, int z, System.Random rng)
        {
            int height = 4 + rng.Next(3); // 4~6

            // 줄기
            for (int dy = 1; dy <= height; dy++)
                set(x, sy + dy, z, BlockType.DeadWood);

            // 가지 1~2개
            int branchCount = 1 + rng.Next(2);
            for (int b = 0; b < branchCount; b++)
            {
                int branchY = sy + 3 + rng.Next(height - 2);
                branchY = Mathf.Min(branchY, sy + height);
                AddBranch(set, x, branchY, z, rng, 2 + rng.Next(2));
            }
        }

        // ═══════════════════════════════════════
        // 변형 2: 갈래 나무 (Y자)
        // ═══════════════════════════════════════
        private static void GenerateForked(Action<int, int, int, BlockType> set,
            int x, int sy, int z, System.Random rng)
        {
            int trunkHeight = 3 + rng.Next(2); // 3~4 줄기 후 분기

            // 줄기
            for (int dy = 1; dy <= trunkHeight; dy++)
                set(x, sy + dy, z, BlockType.DeadWood);

            int forkY = sy + trunkHeight;

            // 가지 A: 한쪽으로
            int dirA = rng.Next(4);
            int lenA = 2 + rng.Next(2);
            AddBranchDirectional(set, x, forkY, z, dirA, lenA, true, rng);

            // 가지 B: 반대쪽으로
            int dirB = (dirA + 2) % 4;
            int lenB = 2 + rng.Next(2);
            AddBranchDirectional(set, x, forkY, z, dirB, lenB, true, rng);
        }

        // ═══════════════════════════════════════
        // 변형 3: 기울어진 나무
        // ═══════════════════════════════════════
        private static void GenerateLeaning(Action<int, int, int, BlockType> set,
            int x, int sy, int z, System.Random rng)
        {
            int height = 5 + rng.Next(3); // 5~7
            int leanDir = rng.Next(4);
            int dx = 0, dz = 0;
            GetDirOffset(leanDir, out dx, out dz);

            int cx = x, cz = z;
            for (int dy = 1; dy <= height; dy++)
            {
                set(cx, sy + dy, cz, BlockType.DeadWood);

                // 2~3블록마다 기울기 방향으로 1블록 이동
                if (dy % 3 == 0)
                {
                    cx += dx;
                    cz += dz;
                }
            }

            // 꼭대기에 짧은 가지
            AddBranch(set, cx, sy + height, cz, rng, 1 + rng.Next(2));
        }

        // ═══════════════════════════════════════
        // 변형 4: 고목 (굵은 줄기)
        // ═══════════════════════════════════════
        private static void GenerateAncient(Action<int, int, int, BlockType> set,
            int x, int sy, int z, System.Random rng)
        {
            int height = 6 + rng.Next(3); // 6~8

            // 2×2 굵은 줄기 (하반부만)
            int thickHeight = height / 2 + 1;
            for (int dy = 1; dy <= thickHeight; dy++)
            {
                set(x,     sy + dy, z,     BlockType.DeadWood);
                set(x + 1, sy + dy, z,     BlockType.DeadWood);
                set(x,     sy + dy, z + 1, BlockType.DeadWood);
                set(x + 1, sy + dy, z + 1, BlockType.DeadWood);
            }

            // 1×1 줄기 (상반부)
            for (int dy = thickHeight + 1; dy <= height; dy++)
                set(x, sy + dy, z, BlockType.DeadWood);

            // 가지 3~4개
            int branchCount = 3 + rng.Next(2);
            for (int b = 0; b < branchCount; b++)
            {
                int branchY = sy + thickHeight + rng.Next(height - thickHeight + 1);
                branchY = Mathf.Min(branchY, sy + height);
                AddBranch(set, x, branchY, z, rng, 2 + rng.Next(3));
            }

            // 뿌리 돌출 (지표면 주변)
            for (int r = 0; r < 3; r++)
            {
                int rootDir = rng.Next(4);
                int rdx, rdz;
                GetDirOffset(rootDir, out rdx, out rdz);
                set(x + rdx * 2, sy, z + rdz * 2, BlockType.DeadWood);
                set(x + rdx, sy + 1, z + rdz, BlockType.DeadWood);
            }
        }

        // ═══════════════════════════════════════
        // 가지 생성 헬퍼
        // ═══════════════════════════════════════

        /// <summary>랜덤 방향으로 가지 생성.</summary>
        private static void AddBranch(Action<int, int, int, BlockType> set,
            int x, int y, int z, System.Random rng, int length)
        {
            int dir = rng.Next(4);
            bool goUp = rng.Next(3) > 0; // 2/3 확률로 위로
            AddBranchDirectional(set, x, y, z, dir, length, goUp, rng);
        }

        /// <summary>지정 방향으로 가지 생성. goUp이면 중간에 위로 올라감.</summary>
        private static void AddBranchDirectional(Action<int, int, int, BlockType> set,
            int x, int y, int z, int dir, int length, bool goUp, System.Random rng)
        {
            int dx, dz;
            GetDirOffset(dir, out dx, out dz);

            int cx = x, cy = y, cz = z;
            for (int i = 1; i <= length; i++)
            {
                cx += dx;
                cz += dz;

                // 위로 올라가기 (매 블록 또는 2블록마다)
                if (goUp && (i % 2 == 1 || rng.Next(2) == 0))
                    cy++;

                set(cx, cy, cz, BlockType.DeadWood);
            }

            // 가지 끝에 30% 확률로 작은 가지 추가
            if (rng.Next(100) < 30 && length >= 2)
            {
                int subDir = (dir + 1 + rng.Next(2)) % 4; // 원래 방향에서 꺾임
                set(cx + (subDir == 0 ? 1 : subDir == 1 ? -1 : 0),
                    cy + 1,
                    cz + (subDir == 2 ? 1 : subDir == 3 ? -1 : 0),
                    BlockType.DeadWood);
            }
        }

        /// <summary>방향 인덱스 → XZ 오프셋.</summary>
        private static void GetDirOffset(int dir, out int dx, out int dz)
        {
            dx = 0; dz = 0;
            switch (dir)
            {
                case 0: dx = 1;  break; // +X
                case 1: dx = -1; break; // -X
                case 2: dz = 1;  break; // +Z
                case 3: dz = -1; break; // -Z
            }
        }
    }
}
