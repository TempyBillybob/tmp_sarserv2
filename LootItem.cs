using System;
using System.Collections.Generic;
using System.Text;

namespace WC.SARS
{
    /// <summary>
    /// From Loot.cs -- Armor Types
    /// </summary>
    public enum ArmorType // I don't think this is ever used... for good reason I think though
    {
        None,
        Tier1,
        Tier2,
        Tier3
    }
    public enum LootType
    {
        Weapon,
        Juices,
        Armor,
        Ammo,
        Attatchment, //Throwable ?
        Tape,
        Collectable
    }
    public enum WeaponType
    {
        Melee,
        Gun,
        Throwable,
        NotWeapon
    }
    internal class LootItem
    {
        /// <summary>
        /// What type of loot this Loot object is.
        /// </summary>
        public LootType LootType;
        /// <summary>
        /// What type of weapon this loot is.
        /// </summary>
        public WeaponType WeaponType;
        /// <summary>
        /// Name of the current loot item.
        /// </summary>
        public string LootName;
        /// <summary>
        /// ID of this loot item.
        /// </summary>
        // Is the LootID really needed? It is already stored in a dictionary somewhere
        public int LootID;
        /// <summary>
        /// Item rarity of the loot item. Most LootTypes use this value.
        /// </summary>
        public byte ItemRarity;
        /// <summary>
        /// The amount which the item should give.
        /// </summary>
        public byte GiveAmount;
        /// <summary>
        /// This Weapon LootItem's index in an all WeaponsList array.
        /// </summary>
        public int IndexInList;
        /// <summary>
        /// This Weapon LootItem's index in an all WeaponsList array.
        /// </summary>
        public int GunAmmo;
        /// <summary>
        /// Create a new Loot object with the specified parameters. This is the basic version
        /// </summary>
        public LootItem(int aLootID, LootType aLootType, WeaponType aWeaponType, string aLootName, byte aItemRarity, byte aGiveAmount)
        {
            // You know, I don't think this works for throwables.
            LootID = aLootID;
            LootType = aLootType;
            WeaponType = aWeaponType;
            LootName = aLootName;
            ItemRarity = aItemRarity;
            GiveAmount = aGiveAmount;
            if (aLootType == LootType.Weapon)
            {
                IndexInList = GiveAmount;
            }
        }
        /// <summary>
        /// Create a new Gun LootObject. Requires its Rarity, IndexInList, and ClipSize
        /// </summary>
        public LootItem(int aLootID, LootType aLootType, WeaponType aWeaponType, string aLootName, byte aItemRarity, byte aWeaponIndex, int aAmmoAmount)
        {
            // You know, I don't think this works for throwables.
            LootID = aLootID;
            LootType = aLootType;
            WeaponType = aWeaponType;
            LootName = aLootName;
            ItemRarity = aItemRarity;
            IndexInList = aWeaponIndex;
            GunAmmo = aAmmoAmount;
            GiveAmount = (byte)GunAmmo; // Throwables use GiveAmmount
        }
    }
}
