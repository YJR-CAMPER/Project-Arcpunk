// ── ItemStack.cs ──
// 인벤토리 한 칸의 데이터. 아이템 타입 + 수량 + 내구도.

namespace Arcpunk.Inventory
{
    [System.Serializable]
    public class ItemStack
    {
        public ItemType Type;
        public int Count;
        public int Durability; // 도구만 해당

        public ItemStack()
        {
            Type = ItemType.None;
            Count = 0;
            Durability = 0;
        }

        public ItemStack(ItemType type, int count = 1)
        {
            Type = type;
            Count = count;
            var def = ItemDatabase.Get(type);
            Durability = def.Durability;
        }

        public bool IsEmpty => Type == ItemType.None || Count <= 0;
        public ItemDef Def => ItemDatabase.Get(Type);

        public bool CanMerge(ItemStack other)
        {
            if (other == null || other.IsEmpty) return false;
            if (IsEmpty) return true;
            return Type == other.Type && Count + other.Count <= Def.MaxStack;
        }

        /// <summary>다른 스택을 이 스택에 합친다. 넘치는 수량을 반환.</summary>
        public int MergeFrom(ItemStack source)
        {
            if (source == null || source.IsEmpty) return 0;

            if (IsEmpty)
            {
                Type = source.Type;
                Durability = source.Durability;
                int space = Def.MaxStack;
                int toAdd = UnityEngine.Mathf.Min(source.Count, space);
                Count = toAdd;
                return source.Count - toAdd;
            }

            if (Type != source.Type) return source.Count;

            int maxAdd = Def.MaxStack - Count;
            int added = UnityEngine.Mathf.Min(source.Count, maxAdd);
            Count += added;
            return source.Count - added;
        }

        /// <summary>이 스택에서 n개 빼기. 뺀 수량 반환.</summary>
        public int Remove(int n)
        {
            int removed = UnityEngine.Mathf.Min(n, Count);
            Count -= removed;
            if (Count <= 0) Clear();
            return removed;
        }

        public void Clear()
        {
            Type = ItemType.None;
            Count = 0;
            Durability = 0;
        }

        public ItemStack Clone()
        {
            return new ItemStack
            {
                Type = this.Type,
                Count = this.Count,
                Durability = this.Durability
            };
        }

        public override string ToString()
        {
            if (IsEmpty) return "[Empty]";
            return $"{Def.Name} x{Count}";
        }
    }
}
