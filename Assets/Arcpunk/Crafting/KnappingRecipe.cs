// ── KnappingRecipe.cs ── (v2 — 도구 머리 + 개선된 패턴)
// 날빗기 결과물은 도구 머리. 나뭇가지와 조합해야 완성 도구가 된다.
// 패턴은 실제 뗀석기 형태를 닮도록 디자인.

using Arcpunk.Inventory;

namespace Arcpunk.Crafting
{
    public class KnappingRecipe
    {
        public string Name;
        public ItemType ResultItem;
        public int ResultCount;
        public ItemType MaterialItem;
        public int MaterialCost;
        public bool[,] Pattern;

        public KnappingRecipe(string name, ItemType result, int resultCount,
            ItemType material, int cost, bool[,] pattern)
        {
            Name = name;
            ResultItem = result;
            ResultCount = resultCount;
            MaterialItem = material;
            MaterialCost = cost;
            Pattern = pattern;
        }
    }

    public static class KnappingRecipes
    {
        private static bool[,] P(string s0, string s1, string s2, string s3, string s4)
        {
            var p = new bool[5, 5];
            string[] rows = { s0, s1, s2, s3, s4 };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    p[y, x] = rows[y][x] == '#';
            return p;
        }

        // 곡괭이 머리: 넓고 납작, 가운데 홈(자루 끼울 곳)
        public static readonly KnappingRecipe StonePickaxeHead = new(
            "석재 곡괭이 머리",
            ItemType.StonePickaxeHead, 1,
            ItemType.StoneChip, 3,
            P(
                "####.",
                ".##..",
                "..#..",
                ".....",
                "....."
            )
        );

        // 도끼 머리: 쐐기형, 한쪽 넓고 반대쪽 좁음
        public static readonly KnappingRecipe StoneAxeHead = new(
            "석재 도끼 머리",
            ItemType.StoneAxeHead, 1,
            ItemType.StoneChip, 3,
            P(
                ".##..",
                "###..",
                "####.",
                ".##..",
                "..#.."
            )
        );

        // 칼날: 길고 가늘며 한쪽 끝이 뾰족
        public static readonly KnappingRecipe StoneKnifeHead = new(
            "석재 칼날",
            ItemType.StoneKnifeHead, 1,
            ItemType.StoneChip, 2,
            P(
                "..#..",
                ".##..",
                ".##..",
                "##...",
                "#...."
            )
        );

        // 석재벽: 네모반듯하게 다듬기
        public static readonly KnappingRecipe StoneBrickRecipe = new(
            "석재벽 (x4)",
            ItemType.StoneBrick, 4,
            ItemType.StoneChip, 2,
            P(
                "####.",
                "####.",
                "####.",
                "####.",
                "....."
            )
        );

        public static readonly KnappingRecipe[] All = {
            StonePickaxeHead,
            StoneAxeHead,
            StoneKnifeHead,
            StoneBrickRecipe,
        };
    }
}
