// ── BlockType.cs ──
// 모든 블록 타입 열거형 + 정적 속성 정의.
// 새 블록을 추가하려면 enum에 항목 추가 + BlockData.Init()에 정의 추가.

using UnityEngine;

namespace Arcpunk.Voxel
{
    public enum BlockType : ushort
    {
        Air = 0,

        // ── 자연 블록 ──
        Stone       = 1,
        Dirt        = 2,
        DeadWood    = 3,
        CopperOre   = 4,
        IronOre     = 5,
        MushroomBlock = 6,

        // ── 건축 블록 ──
        StoneBrick  = 20,
        CopperPlate = 21,
        IronPlate   = 22,

        // ── 전력 블록 ──
        WoodRod       = 40,
        CopperRod     = 41,
        CopperWire    = 42,
        CopperBattery = 43,
        Light         = 44,
        Sentry        = 45,
        ElectricFurnace = 46,

        // ── 기능 블록 ──
        Workbench   = 60,
    }

    public enum PowerRole : byte
    {
        None,
        Producer,   // 피뢰침
        Conductor,  // 배선
        Storage,    // 배터리
        Consumer,   // 조명, 센트리, 전기로
    }

    public enum ToolTier : byte
    {
        Hand,
        Stone,
        Copper,
        Iron,
    }

    /// <summary>
    /// 블록 타입별 정적 속성. 런타임에 변하지 않는 데이터.
    /// </summary>
    public struct BlockDef
    {
        public string Name;
        public bool IsSolid;
        public bool IsTransparent; // 공기, 배선 등 메싱에서 면을 그리지 않는 블록
        public float Hardness;     // 파괴 시간 (초). 0이면 파괴 불가

        // 텍스처 아틀라스 인덱스 (4×4 아틀라스 기준: 0~15)
        public int TexTop;
        public int TexSide;
        public int TexBottom;

        public ToolTier MinTool;

        // 전력 시스템
        public bool IsPowerBlock;
        public PowerRole PowerRole;
        public float PowerDraw;        // 소비 전력 (kW/tick)
        public float BatteryCapacity;  // 저장 용량 (kWh)
        public float BaseOutput;       // 피뢰침 기본 발전량

        // 빛 방출
        public bool EmitsLight;
        public int LightLevel;

        // X자 빌보드 (버섯, 피뢰침 등)
        public bool IsCross;
        public int TexCross;           // 크로스 아틀라스 인덱스
    }

    /// <summary>
    /// 블록 정의 조회용 정적 클래스. 인덱스 = (ushort)BlockType.
    /// </summary>
    public static class BlockData
    {
        public static readonly BlockDef[] Defs = new BlockDef[256];
        public static int AtlasSize = 4; // 4×4 텍스처 아틀라스
        public static int CrossAtlasCols = 2; // 크로스 아틀라스 2×1
        public static int CrossAtlasRows = 1;

        static BlockData()
        {
            Init();
        }

        private static void Init()
        {
            // 기본값: 모두 Air (IsSolid=false, IsTransparent=true)
            for (int i = 0; i < 256; i++)
            {
                Defs[i] = new BlockDef
                {
                    Name = "Air",
                    IsSolid = false,
                    IsTransparent = true,
                };
            }

            // ── 자연 블록 ──
            Set(BlockType.Stone, new BlockDef
            {
                Name = "Stone", IsSolid = true, Hardness = 3f,
                TexTop = 0, TexSide = 0, TexBottom = 0,
                MinTool = ToolTier.Stone,
            });

            Set(BlockType.Dirt, new BlockDef
            {
                Name = "Dirt", IsSolid = true, Hardness = 1f,
                TexTop = 1, TexSide = 1, TexBottom = 1,
                MinTool = ToolTier.Hand,
            });

            Set(BlockType.DeadWood, new BlockDef
            {
                Name = "Dead Wood", IsSolid = true, Hardness = 1.5f,
                TexTop = 3, TexSide = 2, TexBottom = 3,
                MinTool = ToolTier.Hand,
            });

            Set(BlockType.CopperOre, new BlockDef
            {
                Name = "Copper Ore", IsSolid = true, Hardness = 4f,
                TexTop = 4, TexSide = 4, TexBottom = 4,
                MinTool = ToolTier.Stone,
            });

            Set(BlockType.IronOre, new BlockDef
            {
                Name = "Iron Ore", IsSolid = true, Hardness = 5f,
                TexTop = 5, TexSide = 5, TexBottom = 5,
                MinTool = ToolTier.Copper,
            });

            Set(BlockType.MushroomBlock, new BlockDef
            {
                Name = "Mushroom", IsSolid = false, IsTransparent = true,
                Hardness = 0.5f,
                TexTop = 6, TexSide = 6, TexBottom = 6,
                MinTool = ToolTier.Hand,
                IsCross = true,
                TexCross = 1,  // 크로스 아틀라스: 버섯
            });

            // ── 건축 블록 ──
            Set(BlockType.StoneBrick, new BlockDef
            {
                Name = "Stone Brick", IsSolid = true, Hardness = 3.5f,
                TexTop = 7, TexSide = 7, TexBottom = 7,
                MinTool = ToolTier.Stone,
            });

            Set(BlockType.CopperPlate, new BlockDef
            {
                Name = "Copper Plate", IsSolid = true, Hardness = 2.5f,
                TexTop = 8, TexSide = 8, TexBottom = 8,
                MinTool = ToolTier.Stone,
            });

            // ── 전력 블록 ──
            Set(BlockType.WoodRod, new BlockDef
            {
                Name = "Wood Lightning Rod", IsSolid = false, IsTransparent = true,
                Hardness = 1.5f,
                TexTop = 3, TexSide = 2, TexBottom = 3,
                MinTool = ToolTier.Hand,
                IsPowerBlock = true, PowerRole = PowerRole.Producer,
                BaseOutput = 5f,
                IsCross = true,
                TexCross = 0,  // 크로스 아틀라스: 피뢰침
            });

            Set(BlockType.CopperRod, new BlockDef
            {
                Name = "Copper Lightning Rod", IsSolid = false, IsTransparent = true,
                Hardness = 2f,
                TexTop = 10, TexSide = 10, TexBottom = 10,
                MinTool = ToolTier.Stone,
                IsPowerBlock = true, PowerRole = PowerRole.Producer,
                BaseOutput = 10f,
                IsCross = true,
                TexCross = 0,  // 크로스 아틀라스: 피뢰침
            });

            Set(BlockType.CopperBattery, new BlockDef
            {
                Name = "Copper Battery", IsSolid = true, Hardness = 2f,
                TexTop = 12, TexSide = 12, TexBottom = 12,
                MinTool = ToolTier.Stone,
                IsPowerBlock = true, PowerRole = PowerRole.Storage,
                BatteryCapacity = 100f,
            });

            Set(BlockType.Light, new BlockDef
            {
                Name = "Electric Light", IsSolid = true, Hardness = 1f,
                TexTop = 13, TexSide = 13, TexBottom = 13,
                MinTool = ToolTier.Hand,
                IsPowerBlock = true, PowerRole = PowerRole.Consumer,
                PowerDraw = 2f,
                EmitsLight = true, LightLevel = 12,
            });

            Set(BlockType.Sentry, new BlockDef
            {
                Name = "Sentry", IsSolid = true, Hardness = 3f,
                TexTop = 14, TexSide = 14, TexBottom = 14,
                MinTool = ToolTier.Stone,
                IsPowerBlock = true, PowerRole = PowerRole.Consumer,
                PowerDraw = 5f,
            });

            Set(BlockType.ElectricFurnace, new BlockDef
            {
                Name = "Electric Furnace", IsSolid = true, Hardness = 3f,
                TexTop = 15, TexSide = 15, TexBottom = 15,
                MinTool = ToolTier.Stone,
                IsPowerBlock = true, PowerRole = PowerRole.Consumer,
                PowerDraw = 8f,
            });

            Set(BlockType.Workbench, new BlockDef
            {
                Name = "Workbench", IsSolid = true, Hardness = 2f,
                TexTop = 9, TexSide = 9, TexBottom = 9,
                MinTool = ToolTier.Hand,
            });
        }

        private static void Set(BlockType type, BlockDef def)
        {
            def.IsTransparent = !def.IsSolid; // 기본: 비고체 = 투명
            Defs[(ushort)type] = def;
        }

        public static ref BlockDef Get(BlockType type) => ref Defs[(ushort)type];

        public static bool IsSolid(BlockType type) => Defs[(ushort)type].IsSolid;
        public static bool IsTransparent(BlockType type) => Defs[(ushort)type].IsTransparent;
    }
}
