// ── Noise3D.cs ──
// 제대로 된 3D 퍼린 노이즈 구현.
// 해시 기반 그래디언트 방식으로 공간적 연속성 보장.
// 동굴, 3D 광맥, 바이옴 블렌딩 등에 사용.

using UnityEngine;

namespace Arcpunk.Voxel
{
    public static class Noise3D
    {
        // Ken Perlin의 개선된 퍼뮤테이션 테이블
        private static readonly int[] _perm = new int[512];
        private static readonly int[] _base = {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
            140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
            247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,
            57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
            74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,
            60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
            65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,
            200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
            52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,
            207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
            119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
            129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
            218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,
            81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
            184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,
            222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        static Noise3D()
        {
            for (int i = 0; i < 512; i++)
                _perm[i] = _base[i & 255];
        }

        /// <summary>
        /// 3D 퍼린 노이즈. 약 -1 ~ +1 범위.
        /// </summary>
        public static float Perlin(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x) & 255;
            int yi = Mathf.FloorToInt(y) & 255;
            int zi = Mathf.FloorToInt(z) & 255;

            float xf = x - Mathf.Floor(x);
            float yf = y - Mathf.Floor(y);
            float zf = z - Mathf.Floor(z);

            float u = Fade(xf);
            float v = Fade(yf);
            float w = Fade(zf);

            int aaa = _perm[_perm[_perm[xi]     + yi]     + zi];
            int aba = _perm[_perm[_perm[xi]     + yi + 1] + zi];
            int aab = _perm[_perm[_perm[xi]     + yi]     + zi + 1];
            int abb = _perm[_perm[_perm[xi]     + yi + 1] + zi + 1];
            int baa = _perm[_perm[_perm[xi + 1] + yi]     + zi];
            int bba = _perm[_perm[_perm[xi + 1] + yi + 1] + zi];
            int bab = _perm[_perm[_perm[xi + 1] + yi]     + zi + 1];
            int bbb = _perm[_perm[_perm[xi + 1] + yi + 1] + zi + 1];

            float x1 = Lerp(Grad(aaa, xf, yf, zf),     Grad(baa, xf-1, yf, zf),     u);
            float x2 = Lerp(Grad(aba, xf, yf-1, zf),   Grad(bba, xf-1, yf-1, zf),   u);
            float y1 = Lerp(x1, x2, v);

            float x3 = Lerp(Grad(aab, xf, yf, zf-1),   Grad(bab, xf-1, yf, zf-1),   u);
            float x4 = Lerp(Grad(abb, xf, yf-1, zf-1), Grad(bbb, xf-1, yf-1, zf-1), u);
            float y2 = Lerp(x3, x4, w);

            return Lerp(y1, y2, w);
        }

        /// <summary>0~1 범위로 정규화된 3D 퍼린 노이즈.</summary>
        public static float Sample(float x, float y, float z)
        {
            return (Perlin(x, y, z) + 1f) * 0.5f;
        }

        /// <summary>다중 옥타브 3D 노이즈.</summary>
        public static float SampleOctaves(float x, float y, float z, int octaves, float persistence = 0.5f)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += Perlin(x * frequency, y * frequency, z * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }

            return (total / maxValue + 1f) * 0.5f;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);

        private static float Grad(int hash, float x, float y, float z)
        {
            switch (hash & 0xF)
            {
                case 0x0: return  x + y;
                case 0x1: return -x + y;
                case 0x2: return  x - y;
                case 0x3: return -x - y;
                case 0x4: return  x + z;
                case 0x5: return -x + z;
                case 0x6: return  x - z;
                case 0x7: return -x - z;
                case 0x8: return  y + z;
                case 0x9: return -y + z;
                case 0xA: return  y - z;
                case 0xB: return -y - z;
                case 0xC: return  y + x;
                case 0xD: return -y + z;
                case 0xE: return  y - x;
                case 0xF: return -y - z;
                default:  return 0f;
            }
        }
    }
}
