using System;
using System.Collections.Generic;
using System.Text;

namespace WC.SARS
{
    /// <summary>
    /// From Loot.cs -- Armor Types
    /// </summary>
    public enum ArmorType
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
        /// Create a new Loot object with the specified parameters.
        /// </summary>
        public LootItem(int aLootID, LootType aLootType, WeaponType aWeaponType, string aLootName, byte aItemRarity, byte aGiveAmount)
        {
            LootID = aLootID;
            LootType = aLootType;
            WeaponType = aWeaponType;
            LootName = aLootName;
            ItemRarity = aItemRarity;
            GiveAmount = aGiveAmount;
        }
    }
}
