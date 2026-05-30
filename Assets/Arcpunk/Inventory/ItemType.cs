// ── ItemType.cs ── (v2 — 도구 머리 추가)
// 모든 아이템 타입 열거형 + 정적 속성 정의.

using UnityEngine;

namespace Arcpunk.Inventory
{
    public enum ItemType
    {
        None = 0,

        // ── 블록 아이템 (설치 가능) ── 100번대
        Stone = 100,
        Dirt = 101,
        DeadWood = 102,
        CopperOre = 103,
        IronOre = 104,
        MushroomBlock = 105,
        StoneBrick = 106,
        CopperPlate = 107,
        IronPlate = 108,
        WoodRod = 109,
        CopperRod = 110,
        CopperWire = 111,
        CopperBattery = 112,
        Light = 113,
        Sentry = 114,
        ElectricFurnace = 115,
        Workbench = 116,

        // ── 소재 아이템 (설치 불가) ── 200번대
        Stick = 200,
        CopperIngot = 201,
        IronIngot = 202,
        CopperNugget = 203,
        IronNugget = 204,
        StoneChip = 205,
        Mushroom = 206,
        PistolAmmo = 207,
        RifleAmmo = 208,
        MinigunAmmo = 209,


        // ── 도구 머리 (날빗기/주조 결과물) ── 250번대
        StonePickaxeHead = 250,
        StoneAxeHead = 251,
        StoneKnifeHead = 252,
        CopperPickaxeHead = 253,
        CopperAxeHead = 254,

        // ── 완성 도구 ── 300번대
        StonePickaxe = 300,
        StoneAxe = 301,
        StoneKnife = 302,
        CopperPickaxe = 303,
        CopperAxe = 304,
        IronPickaxe = 305,
        IronAxe = 306,

        // ── 무기 ── 400번대
        Pistol = 400,
        Rifle = 401,
        Minigun = 402,
    }

    public enum ToolType
    {
        None,
        Pickaxe,
        Axe,
        Knife,
    }

    public struct ItemDef
    {
        public string Name;
        public int MaxStack;
        public bool IsPlaceable;
        public Voxel.BlockType BlockType;
        public Voxel.ToolTier ToolTier;
        public ToolType ToolType;
        public int Durability;
        public float SpeedMultiplier;
        public float FoodValue;
        public int IconIndex;
    }

    public static class ItemDatabase
    {
        public static readonly ItemDef[] Defs = new ItemDef[512];

        static ItemDatabase()
        {
            for (int i = 0; i < 512; i++)
                Defs[i] = new ItemDef { Name = "Unknown", MaxStack = 64, SpeedMultiplier = 1f };

            // ── 블록 아이템 ──
            Set(ItemType.Stone, "Stone", 64, true, Voxel.BlockType.Stone, 0);
            Set(ItemType.Dirt, "Dirt", 64, true, Voxel.BlockType.Dirt, 1);
            Set(ItemType.DeadWood, "Dead Wood", 64, true, Voxel.BlockType.DeadWood, 2);
            Set(ItemType.CopperOre, "Copper Ore", 64, true, Voxel.BlockType.CopperOre, 4);
            Set(ItemType.IronOre, "Iron Ore", 64, true, Voxel.BlockType.IronOre, 5);
            Set(ItemType.MushroomBlock, "Mushroom Block", 64, true, Voxel.BlockType.MushroomBlock, 6);
            Set(ItemType.StoneBrick, "Stone Brick", 64, true, Voxel.BlockType.StoneBrick, 7);
            Set(ItemType.CopperPlate, "Copper Plate", 64, true, Voxel.BlockType.CopperPlate, 8);
            Set(ItemType.IronPlate, "Iron Plate", 64, true, Voxel.BlockType.IronPlate, 8);
            Set(ItemType.WoodRod, "Wood Rod", 64, true, Voxel.BlockType.WoodRod, 2);
            Set(ItemType.CopperRod, "Copper Rod", 64, true, Voxel.BlockType.CopperRod, 10);
            Set(ItemType.CopperWire, "Copper Wire", 64, true, Voxel.BlockType.CopperWire, 11);
            Set(ItemType.CopperBattery, "Battery", 64, true, Voxel.BlockType.CopperBattery, 12);
            Set(ItemType.Light, "Light", 64, true, Voxel.BlockType.Light, 13);
            Set(ItemType.Sentry, "Sentry", 64, true, Voxel.BlockType.Sentry, 14);
            Set(ItemType.ElectricFurnace, "Electric Furnace", 64, true, Voxel.BlockType.ElectricFurnace, 15);
            Set(ItemType.Workbench, "Workbench", 64, true, Voxel.BlockType.Workbench, 9);

            // ── 소재 아이템 ──
            SetMaterial(ItemType.Stick, "Stick", 64, 2);
            SetMaterial(ItemType.CopperIngot, "Copper Ingot", 64, 10);
            SetMaterial(ItemType.IronIngot, "Iron Ingot", 64, 5);
            SetMaterial(ItemType.CopperNugget, "Copper Nugget", 64, 4);
            SetMaterial(ItemType.IronNugget, "Iron Nugget", 64, 5);
            SetMaterial(ItemType.StoneChip, "Stone Chip", 64, 0);
            SetMaterial(ItemType.Mushroom, "Mushroom", 64, 6, 20f);

            // ── 도구 머리 (날빗기 결과물) ──
            SetMaterial(ItemType.StonePickaxeHead, "Stone Pickaxe Head", 16, 0);
            SetMaterial(ItemType.StoneAxeHead, "Stone Axe Head", 16, 0);
            SetMaterial(ItemType.StoneKnifeHead, "Stone Knife Blade", 16, 0);
            SetMaterial(ItemType.CopperPickaxeHead, "Copper Pickaxe Head", 16, 10);
            SetMaterial(ItemType.CopperAxeHead, "Copper Axe Head", 16, 10);

            // ── 완성 도구 ──
            SetTool(ItemType.StonePickaxe, "Stone Pickaxe", Voxel.ToolTier.Stone, ToolType.Pickaxe, 131, 2.0f, 0);
            SetTool(ItemType.StoneAxe, "Stone Axe", Voxel.ToolTier.Stone, ToolType.Axe, 131, 2.0f, 0);
            SetTool(ItemType.StoneKnife, "Stone Knife", Voxel.ToolTier.Stone, ToolType.Knife, 90, 1.5f, 0);
            SetTool(ItemType.CopperPickaxe, "Copper Pickaxe", Voxel.ToolTier.Copper, ToolType.Pickaxe, 250, 3.0f, 10);
            SetTool(ItemType.CopperAxe, "Copper Axe", Voxel.ToolTier.Copper, ToolType.Axe, 250, 3.0f, 10);
            SetTool(ItemType.IronPickaxe, "Iron Pickaxe", Voxel.ToolTier.Iron, ToolType.Pickaxe, 500, 4.0f, 5);
            SetTool(ItemType.IronAxe, "Iron Axe", Voxel.ToolTier.Iron, ToolType.Axe, 500, 4.0f, 5);

            // ── 무기 ──
            SetTool(ItemType.Pistol, "Pistol", Voxel.ToolTier.Copper, ToolType.None, 500, 1f, 14);
            SetTool(ItemType.Rifle, "Rifle", Voxel.ToolTier.Iron, ToolType.None, 500, 1f, 14);
            SetTool(ItemType.Minigun, "Minigun", Voxel.ToolTier.Iron, ToolType.None, 800, 1f, 14);

            // ── 탄약 ──
            SetMaterial(ItemType.PistolAmmo, "Pistol Ammo", 64, 14);
            SetMaterial(ItemType.RifleAmmo, "Rifle Ammo", 64, 14);
            SetMaterial(ItemType.MinigunAmmo, "Minigun Ammo", 128, 14);
        }

        private static void Set(ItemType type, string name, int maxStack,
            bool placeable, Voxel.BlockType blockType, int icon)
        {
            Defs[(int)type] = new ItemDef
            {
                Name = name,
                MaxStack = maxStack,
                IsPlaceable = placeable,
                BlockType = blockType,
                IconIndex = icon,
                SpeedMultiplier = 1f,
            };
        }

        private static void SetMaterial(ItemType type, string name,
            int maxStack, int icon, float food = 0)
        {
            Defs[(int)type] = new ItemDef
            {
                Name = name,
                MaxStack = maxStack,
                IconIndex = icon,
                FoodValue = food,
                SpeedMultiplier = 1f,
            };
        }

        private static void SetTool(ItemType type, string name,
            Voxel.ToolTier tier, ToolType toolType,
            int durability, float speed, int icon)
        {
            Defs[(int)type] = new ItemDef
            {
                Name = name,
                MaxStack = 1,
                ToolTier = tier,
                ToolType = toolType,
                Durability = durability,
                SpeedMultiplier = speed,
                IconIndex = icon,
            };
        }

        public static ref ItemDef Get(ItemType type) => ref Defs[(int)type];

        /// <summary>400번대 무기인지 판별. HeldVoxelItem 타입 자동 결정에 사용.</summary>
        public static bool IsGun(ItemType type)
            => type == ItemType.Pistol
            || type == ItemType.Rifle
            || type == ItemType.Minigun;
    }
}