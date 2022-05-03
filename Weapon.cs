using System;
using System.IO;
using SimpleJSON;

namespace WC.SARS
{
    internal class Weapon
    {
        public WeaponType WeaponType;
        public string Name;
        public short JSONIndex;
        public int ClipSize;
        public byte AmmoType;
        public byte AmmoSpawnAmount;
        public int Damage;
        public int DamageIncrease;
        public int ArmorDamage;
        public int ArmorDamageOverride;
        public bool PenetratesArmor;
        public byte RarityMaxVal;
        public byte RarityMinVal;
        public byte SpawnSizeOverworld;
        public int SpawnFrequency;

        public Weapon(JSONNode data, short index)
        {
            JSONIndex = index;
            if (data["inventoryID"]) Name = data["inventoryID"];
            if (data["weaponClass"])
            {
                string readValue = data["weaponClass"];
                if (readValue == "Melee")
                {
                    WeaponType = WeaponType.Melee;
                }
                else if (readValue == "Gun")
                {
                    WeaponType = WeaponType.Gun;
                }
                else if (readValue == "Grenade")
                {
                    WeaponType = WeaponType.Throwable;
                    if (data["grenadeInfo"]["worldSpawnAmount"])
                    {
                        SpawnSizeOverworld = (byte)data["grenadeInfo"]["worldSpawnAmount"].AsInt;
                    }
                    else
                    {
                        Logger.Failure("Something went wrong while processing grenade information");
                    }
                }
            }
            // eat my shorts
            if (data["minRarity"])
            {
                RarityMinVal = (byte)data["minRarity"].AsInt;
            }
            if (data["maxRarity"])
            {
                RarityMaxVal = (byte)data["maxRarity"].AsInt;
            }
            if (data["damageNormal"])
            {
                Damage = data["damageNormal"].AsInt;
            }
            if (data["breaksArmorAmount"])
            {
                ArmorDamage = data["breaksArmorAmount"].AsInt;
            }
            if (data["overrideBreaksVehicleAmount"]) // applies to sniper for sure and should apply to magnum as well but IT DOESN'T IN THIS FUCKING UPDATE
            {
                ArmorDamage = data["overrideBreaksVehicleAmount"].AsInt;
            }
            if (data["damageThroughArmor"]) // applies to dartgun for sure- likely bow/crossbow as well
            {
                PenetratesArmor = data["damageThroughArmor"].AsBool;
            }
            if (data["addedDamagePerRarity"])
            {
                DamageIncrease = data["addedDamagePerRarity"].AsInt;
                //Logger.Basic(DamageIncrease.ToString());
            }
            if (data["clipSize"])
            {
                ClipSize = data["clipSize"].AsInt;
                //Logger.Basic(ClipSize.ToString());
            }
            if (data["spawnRatioRelativeToOthers"])
            {
                SpawnFrequency = data["spawnRatioRelativeToOthers"].AsInt;
            }
            if (data["ammoID"])
            {
                AmmoType = (byte)data["ammoID"].AsInt;
            }
            if (data["ammoSpawnAmount"])
            {
                AmmoSpawnAmount = (byte)data["ammoSpawnAmount"].AsInt;
            }
        }

        static public Weapon[] GetAllWeaponsList()
        {
            string dir = Directory.GetCurrentDirectory() + @"\Data\WeaponData.json";
            Weapon[] m_WeaponListGame = new Weapon[0];
            if (!File.Exists(dir))
            {
                Logger.Failure("Could not find WeaponData.json");
            }
            try
            {
                string ReadData = File.ReadAllText(dir);
                JSONArray jArray = (JSONArray)JSON.Parse(ReadData);
                m_WeaponListGame = new Weapon[jArray.Count];
                //Logger.Success(jArray.Count.ToString());
                
                for (int i = 0; i < jArray.Count; i++)
                {
                    m_WeaponListGame[i] = new Weapon(jArray[i], (short)i);
                }
            }
            catch (Exception thisExcept)
            {
                Logger.Failure($"Error processing daat.\nException:: {thisExcept}");
            }
            return m_WeaponListGame;
        }
    }
}
