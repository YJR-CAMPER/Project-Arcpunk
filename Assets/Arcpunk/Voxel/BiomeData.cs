// ── BiomeData.cs ──
// 바이옴 타입 열거형 + 바이옴별 지형 파라미터.
// Land of Thunder 세계관에 맞는 4개 바이옴.

using UnityEngine;

namespace Arcpunk.Voxel
{
    public enum BiomeType : byte
    {
        ScorchedPlains,    // 기본. 평탄, 탄 흙, 죽은 나무 듬성듬성
        AshenHighlands,    // 산지. 높고 험준, 돌 노출, 구리 풍부
        DeadForest,        // 죽은 숲. 낮고 울퉁, 나무·버섯 밀집
        StormCrater,       // 특수. 움푹 파인 크레이터
    }

    /// <summary>바이옴별 지형 생성 파라미터.</summary>
    public struct BiomeParams
    {
        public float BaseHeight;      // 기본 지표면 높이
        public float HeightScale;     // 고저차 진폭
        public float NoiseFreq;       // 주 노이즈 주파수 (작을수록 넓은 언덕)
        public int Octaves;           // 디테일 레이어 수
        public float Persistence;     // 옥타브 감쇠율
        public BlockType Surface;     // 지표면 블록
        public BlockType Subsurface;  // 지표면 아래 블록 (흙 레이어)
        public float TreeDensity;     // 나무 밀도 (노이즈 임계값, 낮을수록 많음)
        public float MushroomDensity; // 버섯 밀도
        public float CopperBonus;     // 구리 광석 보너스 (임계값 감소)
    }

    public static class BiomeData
    {
        // ── 바이옴 파라미터 정의 ──

        public static readonly BiomeParams ScorchedPlains = new()
        {
            BaseHeight = 18f,
            HeightScale = 8f,
            NoiseFreq = 0.02f,
            Octaves = 2,
            Persistence = 0.5f,
            Surface = BlockType.Dirt,
            Subsurface = BlockType.Dirt,
            TreeDensity = 0.90f,      // 나무 드문
            MushroomDensity = 0.88f,
            CopperBonus = 0f,
        };

        public static readonly BiomeParams AshenHighlands = new()
        {
            BaseHeight = 28f,
            HeightScale = 22f,
            NoiseFreq = 0.015f,
            Octaves = 4,
            Persistence = 0.45f,
            Surface = BlockType.Stone,     // 돌 노출
            Subsurface = BlockType.Stone,
            TreeDensity = 0.96f,           // 나무 거의 없음
            MushroomDensity = 0.95f,
            CopperBonus = 0.05f,           // 구리 더 풍부
        };

        public static readonly BiomeParams DeadForest = new()
        {
            BaseHeight = 15f,
            HeightScale = 5f,
            NoiseFreq = 0.03f,
            Octaves = 2,
            Persistence = 0.5f,
            Surface = BlockType.Dirt,
            Subsurface = BlockType.Dirt,
            TreeDensity = 0.78f,           // 나무 밀집
            MushroomDensity = 0.75f,       // 버섯 밀집
            CopperBonus = 0f,
        };

        public static readonly BiomeParams StormCrater = new()
        {
            BaseHeight = 10f,
            HeightScale = 4f,
            NoiseFreq = 0.025f,
            Octaves = 2,
            Persistence = 0.5f,
            Surface = BlockType.Stone,
            Subsurface = BlockType.Dirt,
            TreeDensity = 0.98f,           // 나무 거의 없음
            MushroomDensity = 0.93f,
            CopperBonus = 0.03f,
        };

        /// <summary>바이옴 타입으로 파라미터 조회.</summary>
        public static BiomeParams Get(BiomeType type)
        {
            return type switch
            {
                BiomeType.ScorchedPlains => ScorchedPlains,
                BiomeType.AshenHighlands => AshenHighlands,
                BiomeType.DeadForest => DeadForest,
                BiomeType.StormCrater => StormCrater,
                _ => ScorchedPlains,
            };
        }

        /// <summary>
        /// XZ 위치에서 바이옴 결정 + 경계 블렌딩된 파라미터 반환.
        /// temperature/moisture 두 축의 2D 노이즈로 바이옴 영역을 나눔.
        /// </summary>
        public static BiomeParams SampleBlended(int wx, int wz, float seedOffset)
        {
            // 바이옴 결정용 저주파 노이즈 (넓은 영역)
            float temp = Mathf.PerlinNoise(
                (wx + seedOffset + 2000f) * 0.004f,
                (wz + seedOffset + 2000f) * 0.004f);
            float moist = Mathf.PerlinNoise(
                (wx + seedOffset + 3000f) * 0.005f,
                (wz + seedOffset + 3000f) * 0.005f);

            // 기본 바이옴 결정
            BiomeType primary = DetermineType(temp, moist);
            BiomeParams p = Get(primary);

            // 경계 블렌딩: 인접 바이옴과의 거리에 따라 파라미터 보간
            // 노이즈 값이 경계(임계값) 근처일 때 부드럽게 전환
            float blendRange = 0.08f; // 블렌딩 범위

            // temperature 축 경계 (0.55 기준)
            float tempDist = Mathf.Abs(temp - 0.55f);
            if (tempDist < blendRange)
            {
                BiomeType altTemp = DetermineType(temp > 0.55f ? temp - 0.15f : temp + 0.15f, moist);
                if (altTemp != primary)
                {
                    float t = 1f - (tempDist / blendRange);
                    t = t * t * (3f - 2f * t); // smoothstep
                    p = LerpParams(p, Get(altTemp), t * 0.5f);
                }
            }

            // moisture 축 경계 (0.45, 0.65 기준)
            float moistDist35 = Mathf.Abs(moist - 0.35f);
            float moistDist65 = Mathf.Abs(moist - 0.65f);
            float moistDist = Mathf.Min(moistDist35, moistDist65);
            if (moistDist < blendRange)
            {
                BiomeType altMoist = DetermineType(temp, moist > 0.5f ? moist - 0.15f : moist + 0.15f);
                if (altMoist != primary)
                {
                    float t = 1f - (moistDist / blendRange);
                    t = t * t * (3f - 2f * t);
                    p = LerpParams(p, Get(altMoist), t * 0.5f);
                }
            }

            return p;
        }

        /// <summary>바이옴 타입만 빠르게 결정 (구조물 배치 등에 사용).</summary>
        public static BiomeType SampleType(int wx, int wz, float seedOffset)
        {
            float temp = Mathf.PerlinNoise(
                (wx + seedOffset + 2000f) * 0.004f,
                (wz + seedOffset + 2000f) * 0.004f);
            float moist = Mathf.PerlinNoise(
                (wx + seedOffset + 3000f) * 0.005f,
                (wz + seedOffset + 3000f) * 0.005f);
            return DetermineType(temp, moist);
        }

        private static BiomeType DetermineType(float temp, float moist)
        {
            //  temp ↑ (뜨거움)
            //  ┌──────────────┬──────────────┐
            //  │ Storm Crater │ Ashen        │
            //  │ (건조+뜨거움)│ Highlands    │
            //  │              │ (습윤+뜨거움)│
            //  ├──────────────┼──────────────┤
            //  │ Scorched     │ Dead Forest  │
            //  │ Plains       │ (습윤+서늘)  │
            //  │ (건조+서늘)  │              │
            //  └──────────────┴──────────────┘
            //              moist → (습윤)

            if (temp > 0.55f)
            {
                if (moist > 0.5f) return BiomeType.AshenHighlands;
                else              return BiomeType.StormCrater;
            }
            else
            {
                if (moist > 0.5f) return BiomeType.DeadForest;
                else              return BiomeType.ScorchedPlains;
            }
        }

        /// <summary>두 바이옴 파라미터를 선형 보간.</summary>
        private static BiomeParams LerpParams(BiomeParams a, BiomeParams b, float t)
        {
            return new BiomeParams
            {
                BaseHeight = Mathf.Lerp(a.BaseHeight, b.BaseHeight, t),
                HeightScale = Mathf.Lerp(a.HeightScale, b.HeightScale, t),
                NoiseFreq = Mathf.Lerp(a.NoiseFreq, b.NoiseFreq, t),
                Octaves = Mathf.RoundToInt(Mathf.Lerp(a.Octaves, b.Octaves, t)),
                Persistence = Mathf.Lerp(a.Persistence, b.Persistence, t),
                // 블록 타입은 보간 불가 — 주 바이옴 우선
                Surface = t < 0.5f ? a.Surface : b.Surface,
                Subsurface = t < 0.5f ? a.Subsurface : b.Subsurface,
                TreeDensity = Mathf.Lerp(a.TreeDensity, b.TreeDensity, t),
                MushroomDensity = Mathf.Lerp(a.MushroomDensity, b.MushroomDensity, t),
                CopperBonus = Mathf.Lerp(a.CopperBonus, b.CopperBonus, t),
            };
        }
    }
}
