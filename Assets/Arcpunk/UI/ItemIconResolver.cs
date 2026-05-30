// ── ItemIconResolver.cs ──
// Sprite[] 배열로 아이콘 관리.
// Sprite Editor로 자른 것, 개별 PNG, 뭐든 Sprite면 OK.

using UnityEngine;
using UnityEngine.UI;
using Arcpunk.Voxel;

namespace Arcpunk.Inventory
{
    public enum IconMode { IsometricBlock, GeminiSprite, None }
    public enum HeldMode { Block, VoxelSprite, None }

    public class ItemIconResolver : MonoBehaviour
    {
        public static ItemIconResolver Instance { get; private set; }

        [Header("아이템 아이콘 (Sprite 배열)")]
        [SerializeField] private Sprite[] _icons;
        //
        //  [0]  StoneChip          [10] CopperPickaxeHead
        //  [1]  Stick              [11] CopperAxeHead
        //  [2]  CopperIngot        [12] StonePickaxe
        //  [3]  IronIngot          [13] StoneAxe
        //  [4]  CopperNugget       [14] StoneKnife
        //  [5]  IronNugget         [15] CopperPickaxe
        //  [6]  Mushroom           [16] CopperAxe
        //  [7]  StonePickaxeHead   [17] IronPickaxe
        //  [8]  StoneAxeHead       [18] IronAxe
        //  [9]  StoneKnifeHead     [19] Pistol
        //                             [20] Rifle
        //                             [21] Minigun
        //                             [22] PistolAmmo
        //                             [23] RifleAmmo
        //                             [24] MinigunAmmo

        private void Awake() { Instance = this; }

        public static int GetIndex(ItemType type)
        {
            return type switch
            {
                ItemType.StoneChip => 0,
                ItemType.Stick => 1,
                ItemType.CopperIngot => 2,
                ItemType.IronIngot => 3,
                ItemType.CopperNugget => 4,
                ItemType.IronNugget => 5,
                ItemType.Mushroom => 6,
                ItemType.StonePickaxeHead => 7,
                ItemType.StoneAxeHead => 8,
                ItemType.StoneKnifeHead => 9,
                ItemType.CopperPickaxeHead => 10,
                ItemType.CopperAxeHead => 11,
                ItemType.StonePickaxe => 12,
                ItemType.StoneAxe => 13,
                ItemType.StoneKnife => 14,
                ItemType.CopperPickaxe => 15,
                ItemType.CopperAxe => 16,
                ItemType.IronPickaxe => 17,
                ItemType.IronAxe => 18,
                ItemType.Pistol => 19,
                ItemType.Rifle => 20,
                ItemType.Minigun => 21,
                ItemType.PistolAmmo => 22,
                ItemType.RifleAmmo => 23,
                ItemType.MinigunAmmo => 24,
                _ => -1,
            };
        }

        public static IconMode GetIconMode(ItemType type)
        {
            int id = (int)type;
            if (id >= 100 && id < 200) return IconMode.IsometricBlock;
            if (id >= 200) return IconMode.GeminiSprite;
            return IconMode.None;
        }

        public static HeldMode GetHeldMode(ItemType type)
        {
            int id = (int)type;
            if (id >= 100 && id < 200) return HeldMode.Block;
            if (id >= 200) return HeldMode.VoxelSprite;  // 소재 + 도구 머리 + 도구 + 무기 전부
            return HeldMode.None;
        }

        /// <summary>Sprite 반환. RawImage에도 사용 가능.</summary>
        public Sprite GetSprite(ItemType type)
        {
            int idx = GetIndex(type);
            if (idx < 0 || _icons == null || idx >= _icons.Length) return null;
            return _icons[idx];
        }

        /// <summary>RawImage에 Sprite를 세팅하는 헬퍼.</summary>
        public static void ApplyToRawImage(RawImage icon, Sprite sprite)
        {
            if (sprite == null) { icon.enabled = false; return; }

            icon.texture = sprite.texture;

            // Sprite의 textureRect → UV rect 변환
            var r = sprite.textureRect;
            float tw = sprite.texture.width;
            float th = sprite.texture.height;
            icon.uvRect = new Rect(r.x / tw, r.y / th, r.width / tw, r.height / th);
            icon.enabled = true;
        }

        /// <summary>손에 든 아이템 갱신.</summary>
        public void UpdateHeldItem(ItemType type)
        {
            var voxelHeld = HeldVoxelItem.Instance;
            if (voxelHeld == null) return;

            var heldMode = GetHeldMode(type);

            if (heldMode == HeldMode.VoxelSprite)
            {
                // Sprite에서 Texture2D 추출 (복셀 스프라이트용)
                var sprite = GetSprite(type);
                if (sprite != null)
                {
                    var tex = ExtractTexture(sprite);
                    var heldType = ItemDatabase.IsGun(type)
                        ? Voxel.HeldItemType.Gun
                        : Voxel.HeldItemType.Tool;
                    voxelHeld.ShowItem(tex, heldType);
                }
                else
                {
                    voxelHeld.Hide();
                }
            }
            else
            {
                voxelHeld.Hide();
            }
        }

        /// <summary>Sprite 영역을 새 Texture2D로 추출.</summary>
        private Texture2D ExtractTexture(Sprite sprite)
        {
            var r = sprite.textureRect;
            int x = Mathf.FloorToInt(r.x);
            int y = Mathf.FloorToInt(r.y);
            int w = Mathf.FloorToInt(r.width);
            int h = Mathf.FloorToInt(r.height);

            try
            {
                Color[] pixels = sprite.texture.GetPixels(x, y, w, h);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixels(pixels);
                tex.Apply();
                return tex;
            }
            catch
            {
                Debug.LogWarning($"[ItemIconResolver] Sprite texture not readable. " +
                    "Read/Write Enabled를 켜주세요: {sprite.texture.name}");
                return null;
            }
        }
    }
}