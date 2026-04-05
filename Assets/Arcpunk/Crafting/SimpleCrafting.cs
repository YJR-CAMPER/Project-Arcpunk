// ── SimpleCrafting.cs ──
// 간단한 2재료 조합 시스템.
// 도구 머리 + 나뭇가지 = 완성 도구.
// 인벤토리 UI에서 [조합] 버튼으로 접근하거나,
// 핫바에서 도구 머리를 들고 나뭇가지에 우클릭(향후).
//
// 프로토타입에서는 레시피 목록 UI로 구현.

using System.Collections.Generic;
using Arcpunk.Inventory;

namespace Arcpunk.Crafting
{
    public struct CombineRecipe
    {
        public string Name;
        public ItemType Result;
        public int ResultCount;
        public (ItemType item, int count)[] Ingredients;

        public CombineRecipe(string name, ItemType result, int resultCount,
            params (ItemType, int)[] ingredients)
        {
            Name = name;
            Result = result;
            ResultCount = resultCount;
            Ingredients = ingredients;
        }
    }

    public static class SimpleCrafting
    {
        public static readonly List<CombineRecipe> Recipes = new()
        {
            // ── 도구 머리 + 나뭇가지 = 완성 도구 ──
            new("석재 곡괭이", ItemType.StonePickaxe, 1,
                (ItemType.StonePickaxeHead, 1), (ItemType.Stick, 2)),

            new("석재 도끼", ItemType.StoneAxe, 1,
                (ItemType.StoneAxeHead, 1), (ItemType.Stick, 2)),

            new("석재 칼", ItemType.StoneKnife, 1,
                (ItemType.StoneKnifeHead, 1), (ItemType.Stick, 1)),

            // ── 구리 도구 (주조 머리 + 나뭇가지) ──
            new("구리 곡괭이", ItemType.CopperPickaxe, 1,
                (ItemType.CopperPickaxeHead, 1), (ItemType.Stick, 2)),

            new("구리 도끼", ItemType.CopperAxe, 1,
                (ItemType.CopperAxeHead, 1), (ItemType.Stick, 2)),

            // ── 기본 제작 ──
            new("작업대", ItemType.Workbench, 1,
                (ItemType.Stick, 4)),

            new("나무 피뢰침", ItemType.WoodRod, 1,
                (ItemType.Stick, 3)),

            new("구리 피뢰침", ItemType.CopperRod, 1,
                (ItemType.CopperIngot, 3)),

            new("구리 배선 (x6)", ItemType.CopperWire, 6,
                (ItemType.CopperIngot, 3)),

            new("구리 배터리", ItemType.CopperBattery, 1,
                (ItemType.CopperIngot, 6), (ItemType.StoneChip, 2)),

            new("조명", ItemType.Light, 1,
                (ItemType.CopperIngot, 2), (ItemType.CopperNugget, 1)),

            new("구리판 (x4)", ItemType.CopperPlate, 4,
                (ItemType.CopperIngot, 4)),

            new("석재벽 (x4)", ItemType.StoneBrick, 4,
                (ItemType.StoneChip, 4)),

            new("구리 주괴", ItemType.CopperIngot, 1,
                (ItemType.CopperNugget, 9)),

            new("철 주괴", ItemType.IronIngot, 1,
                (ItemType.IronNugget, 9)),
        };

        /// <summary>인벤토리에 재료가 충분한 레시피 목록.</summary>
        public static List<CombineRecipe> GetAvailableRecipes(PlayerInventory inv)
        {
            var result = new List<CombineRecipe>();
            foreach (var recipe in Recipes)
            {
                if (CanCraft(inv, recipe))
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>재료가 충분한지 체크.</summary>
        public static bool CanCraft(PlayerInventory inv, CombineRecipe recipe)
        {
            foreach (var (item, count) in recipe.Ingredients)
            {
                if (inv.CountItem(item) < count)
                    return false;
            }
            return true;
        }

        /// <summary>제작 실행. 재료 소모 + 결과물 추가.</summary>
        public static bool Craft(PlayerInventory inv, CombineRecipe recipe)
        {
            if (!CanCraft(inv, recipe)) return false;

            // 재료 소모
            foreach (var (item, count) in recipe.Ingredients)
                inv.RemoveItem(item, count);

            // 결과물 추가
            int leftover = inv.AddItem(recipe.Result, recipe.ResultCount);
            if (leftover > 0)
            {
                // 인벤토리 가득 → 바닥에 드롭
                var player = Player.PlayerController.Instance;
                if (player != null)
                    ItemEntity.Spawn(
                        player.transform.position + UnityEngine.Vector3.up,
                        recipe.Result, leftover);
            }

            UnityEngine.Debug.Log($"[Crafting] Made: {recipe.Name} x{recipe.ResultCount}");
            return true;
        }
    }
}
