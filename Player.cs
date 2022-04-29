using Lidgren.Network;

namespace WC.SARS
{
    class Player
    {
        // Make Some Empty Variables! \\
        public NetConnection sender;
        public short myID;
        public string myName = "愛子";
        public short charID; // Character / Avatar
        public short umbrellaID; // Umbrella
        public short gravestoneID; // Gravestone
        public short deathEffectID; // Death Explosion
        public short[] emoteIDs; // Emote List (Length of 6; short; array)
        public short hatID; // Hat
        public short glassesID; // Glasses
        public short beardID; // Beard
        public short clothesID; // Clothes
        public short meleeID; // Melee
        public byte gunSkinCount; // Gunskin1
        public short[] gunskinKey; // Gunskin2 IDK //key
        public byte[] gunskinValue; // Gunskin3 IDK //value

        //Updated Regularly...
        public float lastPingTime = 0f;
        public float mouseAngle = 0f;
        public float position_X = 508.7f; 
        public float position_Y = 496.7f;

        // Dance Stuff
        public short currenteEmote = -1;
        public float EmoteDuration;
        public bool isDancing = false;


        public byte WalkMode = 0;
        public byte ActiveSlot = 0;
        public LootItem[] MyLootItems = new LootItem[3];
        //public LootItem EquipSlot1;
        //public LootItem EquipSlot2;
        //public LootItem EquipSlot3;
        //public short equip1 = -1;
        //public short equip2 = -1;
        //public short equip3 = -1;
        //public byte equip1_rarity = 0;
        //public byte equip2_rarity = 0;
        public byte curEquipIndex = 0;
        public short vehicleID = -1;
        // having to do with "health" stuff
        public byte hp = 100; //max 100
        public byte armorTier = 0;
        public byte armorTapes = 0;
        public byte drinkies = 200;
        public byte tapies = 0;

        // Color Bools
        public bool isDev = false;
        public bool isMod = false;
        public bool isFounder = false;

        //Booleans
        public bool isReloading = false;
        public bool isDrinking = false;
        public bool isTaping = false;
        public bool isAlive = true;

        //constructor edited: 2/24/22
        public Player(short assignedID, short characterID, short parasollID, short gravestoneID, short deathExplosionID, short[] emotes, short hatID, short glassesID, short beardID, short clothingID, short meleeID, byte skinCount, short[] skinKeys, byte[] skinValues, string thisName, NetConnection senderAddress)
        {
            this.myName = thisName;
            this.myID = assignedID;
            this.charID = characterID;
            this.umbrellaID = parasollID;
            this.gravestoneID = gravestoneID;
            this.deathEffectID = deathExplosionID;
            this.emoteIDs = emotes;
            this.hatID = hatID;
            this.glassesID = glassesID;
            this.beardID = beardID;
            this.clothesID = clothingID;
            this.meleeID = meleeID;
            this.gunSkinCount = skinCount;
            this.gunskinKey = skinKeys;
            this.gunskinValue= skinValues;
            this.sender = senderAddress;
            MyLootItems = new LootItem[] {
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0),
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0),
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0)
            };
        }

        /* Death Notes:
         * Weapon: 
         * 0 -- Melee?
         * -1  -- nothing
         * -2 == Killfeed Hammer
         * -3 == Killfeed Explosiion lol
         * -4 -- nothing
         * 
         * 
         * Killing Player:
         * anything 0+ = player
         * -1 -- nothing
         * -2 == Gas
         * -3 == Banan Gods (/killed)
         * 
         */

    }
}
