// ── DropTable.cs ──
// 블록 파괴 시 드롭 아이템을 결정.
// 도구 티어에 따라 다른 드롭.

using System.Collections.Generic;
using Arcpunk.Voxel;

namespace Arcpunk.Inventory
{
    public struct DropEntry
    {
        public ItemType Item;
        public int MinCount;
        public int MaxCount;
        public ToolTier MinToolTier;  // 이 티어 이상이어야 드롭
    }

    public static class DropTable
    {
        private static Dictionary<BlockType, List<DropEntry>> _table = new();

        static DropTable()
        {
            // ── 자연 블록 ──
            Add(BlockType.Dirt, ItemType.Dirt, 1, 1, ToolTier.Hand);

            Add(BlockType.Stone, ItemType.StoneChip, 2, 3, ToolTier.Stone);
            Add(BlockType.Stone, ItemType.Stone, 1, 1, ToolTier.Iron); // 철 이상이면 블록 드롭

            Add(BlockType.DeadWood, ItemType.Stick, 1, 2, ToolTier.Hand);
            Add(BlockType.DeadWood, ItemType.DeadWood, 1, 1, ToolTier.Copper);

            Add(BlockType.CopperOre, ItemType.CopperNugget, 2, 3, ToolTier.Copper);
            Add(BlockType.IronOre, ItemType.IronNugget, 2, 2, ToolTier.Iron);
            Add(BlockType.MushroomBlock, ItemType.Mushroom, 1, 2, ToolTier.Hand);

            // ── 건축 블록 ──
            Add(BlockType.StoneBrick, ItemType.StoneBrick, 1, 1, ToolTier.Stone);
            Add(BlockType.CopperPlate, ItemType.CopperPlate, 1, 1, ToolTier.Stone);

            // ── 전력 블록 (자기 자신 드롭) ──
            Add(BlockType.WoodRod, ItemType.WoodRod, 1, 1, ToolTier.Hand);
            Add(BlockType.CopperRod, ItemType.CopperRod, 1, 1, ToolTier.Stone);
            Add(BlockType.CopperWire, ItemType.CopperWire, 1, 1, ToolTier.Hand);
            Add(BlockType.CopperBattery, ItemType.CopperBattery, 1, 1, ToolTier.Stone);
            Add(BlockType.Light, ItemType.Light, 1, 1, ToolTier.Stone);
            Add(BlockType.Sentry, ItemType.Sentry, 1, 1, ToolTier.Stone);
            Add(BlockType.ElectricFurnace, ItemType.ElectricFurnace, 1, 1, ToolTier.Copper);
            Add(BlockType.Workbench, ItemType.Workbench, 1, 1, ToolTier.Hand);
        }

        private static void Add(BlockType block, ItemType item,
            int min, int max, ToolTier minTier)
        {
            if (!_table.ContainsKey(block))
                _table[block] = new List<DropEntry>();

            _table[block].Add(new DropEntry
            {
                Item = item,
                MinCount = min,
                MaxCount = max,
                MinToolTier = minTier,
            });
        }

        /// <summary>블록 파괴 시 드롭할 아이템 목록. 도구 티어에 따라 필터링.</summary>
        public static List<(ItemType type, int count)> GetDrops(
            BlockType block, ToolTier toolTier)
        {
            var result = new List<(ItemType, int)>();

            if (!_table.TryGetValue(block, out var entries))
                return result;

            // 도구 티어 조건을 만족하는 것 중 가장 높은 티어의 드롭을 선택
            DropEntry? bestDrop = null;

            foreach (var entry in entries)
            {
                if (toolTier >= entry.MinToolTier)
                {
                    // 더 높은 티어 요구 드롭을 우선 (철 곡괭이로 돌 캐면 Stone 드롭)
                    if (!bestDrop.HasValue || entry.MinToolTier > bestDrop.Value.MinToolTier)
                        bestDrop = entry;
                }
            }

            if (bestDrop.HasValue)
            {
                int count = UnityEngine.Random.Range(
                    bestDrop.Value.MinCount,
                    bestDrop.Value.MaxCount + 1);
                result.Add((bestDrop.Value.Item, count));
            }

            return result;
        }

        /// <summary>해당 도구로 이 블록을 파괴할 수 있는지.</summary>
        public static bool CanBreak(BlockType block, ToolTier toolTier)
        {
            if (!_table.TryGetValue(block, out var entries))
                return true; // 드롭 테이블 없는 블록은 항상 파괴 가능

            // 최소 티어 중 가장 낮은 것 기준
            ToolTier lowestRequired = ToolTier.Iron;
            foreach (var entry in entries)
            {
                if (entry.MinToolTier < lowestRequired)
                    lowestRequired = entry.MinToolTier;
            }

            return toolTier >= lowestRequired;
        }
    }
}
