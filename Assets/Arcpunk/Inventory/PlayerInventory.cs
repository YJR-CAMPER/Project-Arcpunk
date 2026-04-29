// ── PlayerInventory.cs ──
// 플레이어 인벤토리. 핫바 9칸 + 메인 27칸 = 36칸.
// Player 오브젝트에 부착.

using System;
using UnityEngine;

namespace Arcpunk.Inventory
{
    public class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        public const int HOTBAR_SIZE = 9;
        public const int MAIN_SIZE = 27;
        public const int TOTAL_SIZE = HOTBAR_SIZE + MAIN_SIZE; // 36

        /// <summary>슬롯 배열. 0~8 = 핫바, 9~35 = 메인 인벤토리.</summary>
        public ItemStack[] Slots { get; private set; }

        /// <summary>현재 선택된 핫바 슬롯 (0~8).</summary>
        public int SelectedHotbar { get; private set; }

        /// <summary>슬롯 내용이 바뀔 때 발생. int = 슬롯 인덱스.</summary>
        public event Action<int> OnSlotChanged;

        /// <summary>핫바 선택이 바뀔 때 발생.</summary>
        public event Action<int> OnHotbarSelectionChanged;

        private void Awake()
        {
            Instance = this;
            Slots = new ItemStack[TOTAL_SIZE];
            for (int i = 0; i < TOTAL_SIZE; i++)
                Slots[i] = new ItemStack();
        }

        private void Update()
        {
            HandleHotbarInput();
        }

        private void HandleHotbarInput()
        {
            // 숫자키 1~9
            for (int i = 0; i < HOTBAR_SIZE; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectedHotbar = i;
                    OnHotbarSelectionChanged?.Invoke(i);
                }
            }

            // 마우스 휠
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                SelectedHotbar = (SelectedHotbar + (scroll > 0 ? -1 : 1) + HOTBAR_SIZE) % HOTBAR_SIZE;
                OnHotbarSelectionChanged?.Invoke(SelectedHotbar);
            }
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        /// <summary>현재 선택된 핫바 슬롯의 아이템.</summary>
        public ItemStack GetSelectedItem() => Slots[SelectedHotbar];

        /// <summary>아이템을 인벤토리에 추가. 핫바 우선, 그 다음 메인. 못 넣은 수량 반환.</summary>
        public int AddItem(ItemType type, int count = 1)
        {
            int remaining = count;

            // 1차: 같은 타입이 이미 있는 슬롯에 합치기 (핫바 먼저)
            for (int i = 0; i < TOTAL_SIZE && remaining > 0; i++)
            {
                if (Slots[i].Type == type && Slots[i].Count < Slots[i].Def.MaxStack)
                {
                    int space = Slots[i].Def.MaxStack - Slots[i].Count;
                    int toAdd = Mathf.Min(remaining, space);
                    Slots[i].Count += toAdd;
                    remaining -= toAdd;
                    OnSlotChanged?.Invoke(i);
                }
            }

            // 2차: 빈 슬롯에 새로 넣기
            for (int i = 0; i < TOTAL_SIZE && remaining > 0; i++)
            {
                if (Slots[i].IsEmpty)
                {
                    var def = ItemDatabase.Get(type);
                    int toAdd = Mathf.Min(remaining, def.MaxStack);
                    Slots[i] = new ItemStack(type, toAdd);
                    remaining -= toAdd;
                    OnSlotChanged?.Invoke(i);
                }
            }

            return remaining; // 0이면 전부 넣은 것
        }

        /// <summary>특정 아이템을 n개 제거. 실제 제거된 수량 반환.</summary>
        public int RemoveItem(ItemType type, int count = 1)
        {
            int toRemove = count;

            for (int i = 0; i < TOTAL_SIZE && toRemove > 0; i++)
            {
                if (Slots[i].Type == type)
                {
                    int removed = Slots[i].Remove(toRemove);
                    toRemove -= removed;
                    OnSlotChanged?.Invoke(i);
                }
            }

            return count - toRemove; // 실제 제거된 수량
        }

        /// <summary>특정 아이템 보유 수량.</summary>
        public int CountItem(ItemType type)
        {
            int total = 0;
            for (int i = 0; i < TOTAL_SIZE; i++)
                if (Slots[i].Type == type)
                    total += Slots[i].Count;
            return total;
        }

        /// <summary>선택된 핫바 아이템 1개 소모.</summary>
        public bool ConsumeSelected()
        {
            var slot = Slots[SelectedHotbar];
            if (slot.IsEmpty) return false;

            slot.Remove(1);
            OnSlotChanged?.Invoke(SelectedHotbar);
            return true;
        }

        /// <summary>두 슬롯 교환.</summary>
        public void SwapSlots(int a, int b)
        {
            if (a < 0 || a >= TOTAL_SIZE || b < 0 || b >= TOTAL_SIZE) return;
            (Slots[a], Slots[b]) = (Slots[b], Slots[a]);
            OnSlotChanged?.Invoke(a);
            OnSlotChanged?.Invoke(b);
        }

        /// <summary>외부에서 슬롯 변경 알림을 보낼 때 사용.</summary>
        public void NotifySlotChanged(int index)
        {
            OnSlotChanged?.Invoke(index);
        }

        // ═══════════════════════════════════════
        // 디버그
        // ═══════════════════════════════════════

        /// <summary>디버그용: 모든 블록 아이템 10개씩 추가.</summary>
        public void DebugFillInventory()
        {
            // 먼저 인벤토리 비우기
            for (int i = 0; i < TOTAL_SIZE; i++)
                Slots[i] = new ItemStack();

            // 핫바 (0~8): 자주 쓰는 도구/블록
            AddItem(ItemType.StonePickaxe, 1);
            AddItem(ItemType.StoneAxe, 1);
            AddItem(ItemType.StoneKnife, 1);
            AddItem(ItemType.Workbench, 4);
            AddItem(ItemType.StoneBrick, 64);
            AddItem(ItemType.CopperPlate, 32);
            AddItem(ItemType.WoodRod, 16);
            AddItem(ItemType.CopperRod, 16);
            AddItem(ItemType.Sentry, 8);

            // 메인 인벤토리 (9~35): 자원/부품
            AddItem(ItemType.StoneChip, 32);
            AddItem(ItemType.Stick, 32);
            AddItem(ItemType.CopperWire, 32);
            AddItem(ItemType.CopperBattery, 8);
            AddItem(ItemType.Light, 8);
            AddItem(ItemType.ElectricFurnace, 4);
            AddItem(ItemType.CopperIngot, 16);
            AddItem(ItemType.IronIngot, 16);
            AddItem(ItemType.Mushroom, 16);
            AddItem(ItemType.CopperOre, 32);
            AddItem(ItemType.IronOre, 32);

            // 전체 슬롯 갱신 알림
            for (int i = 0; i < TOTAL_SIZE; i++)
                OnSlotChanged?.Invoke(i);
        }
    }
}