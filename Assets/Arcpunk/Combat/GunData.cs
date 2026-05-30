// ── GunData.cs ──
// 총기 타입 정의 + 스탯.
// PlayerGunController에서 현재 장착된 총기의 GunDef를 참조.

using System;
using UnityEngine;

namespace Arcpunk.Combat
{
    public enum GunType
    {
        None,
        Pistol,
        Rifle,
        Minigun,
    }

    public enum FireMode
    {
        SemiAuto,   // 클릭당 1발
        FullAuto,   // 홀드 시 연사
    }

    [Serializable]
    public struct GunDef
    {
        public GunType Type;
        public string DisplayName;
        public FireMode Mode;

        [Header("Combat")]
        public float Damage;
        public float Knockback;
        public float Range;
        public float SpreadAngle;       // 도 단위, 0=완벽 정확

        [Header("Fire Rate")]
        public float FireInterval;      // 발사 간격 (초)
        public float SpinUpTime;        // 미니건 전용: 발사 준비 시간

        [Header("Ammo")]
        public Inventory.ItemType AmmoType;
        public int AmmoPerShot;         // 발사당 소모량

        [Header("VFX")]
        public Color TracerColor;
        public float TracerWidth;
        public float MuzzleFlashIntensity;
    }

    public static class GunDatabase
    {
        public static readonly GunDef Pistol = new()
        {
            Type = GunType.Pistol,
            DisplayName = "Pistol",
            Mode = FireMode.SemiAuto,

            Damage = 15f,
            Knockback = 3f,
            Range = 50f,
            SpreadAngle = 1f,

            FireInterval = 0.33f,       // 초당 ~3발
            SpinUpTime = 0f,

            AmmoType = Inventory.ItemType.PistolAmmo,
            AmmoPerShot = 1,

            TracerColor = new Color(1f, 0.9f, 0.5f, 0.8f),
            TracerWidth = 0.02f,
            MuzzleFlashIntensity = 4f,
        };

        public static readonly GunDef Rifle = new()
        {
            Type = GunType.Rifle,
            DisplayName = "Rifle",
            Mode = FireMode.SemiAuto,

            Damage = 35f,
            Knockback = 6f,
            Range = 80f,
            SpreadAngle = 0.3f,

            FireInterval = 0.5f,        // 초당 2발
            SpinUpTime = 0f,

            AmmoType = Inventory.ItemType.RifleAmmo,
            AmmoPerShot = 1,

            TracerColor = new Color(1f, 0.8f, 0.3f, 0.9f),
            TracerWidth = 0.025f,
            MuzzleFlashIntensity = 6f,
        };

        public static readonly GunDef Minigun = new()
        {
            Type = GunType.Minigun,
            DisplayName = "Minigun",
            Mode = FireMode.FullAuto,

            Damage = 8f,
            Knockback = 1.5f,
            Range = 30f,
            SpreadAngle = 5f,

            FireInterval = 0.066f,      // 초당 ~15발
            SpinUpTime = 0.8f,          // 발사 전 준비 시간

            AmmoType = Inventory.ItemType.MinigunAmmo,
            AmmoPerShot = 1,

            TracerColor = new Color(1f, 0.6f, 0.2f, 0.7f),
            TracerWidth = 0.03f,
            MuzzleFlashIntensity = 8f,
        };

        /// <summary>ItemType → GunDef 매핑.</summary>
        public static bool TryGetGun(Inventory.ItemType itemType, out GunDef gun)
        {
            switch (itemType)
            {
                case Inventory.ItemType.Pistol:
                    gun = Pistol;
                    return true;
                case Inventory.ItemType.Rifle:
                    gun = Rifle;
                    return true;
                case Inventory.ItemType.Minigun:
                    gun = Minigun;
                    return true;
                default:
                    gun = default;
                    return false;
            }
        }
    }
}
