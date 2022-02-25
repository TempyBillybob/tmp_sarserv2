using Lidgren.Network;
/*
so the idea of this class is to be a client object that a server object can store in a list.
*/

namespace WC.SARS
{
    class Player
    {
        // Make Some Empty Variables! \\
        public short myID; // Server Assigns this
        public NetConnection sender; // Server Also Assigns this
        public string myName = "愛子";
        public short charID; // Character & Avatar
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
        public float mouseAngle = 0f;
        public float position_X = 508.7f; 
        public float position_Y = 496.7f;
        public short currenteEmote = -1;
        public byte currWalkMode = 0;
        public byte activeSlot = 0;
        public short equip1 = -1;
        public short equip2 = -1;
        public byte equip1_rarity = 0;
        public byte equip2_rarity = 0;
        public byte curEquipIndex = 0;
        public short vehicleID = -1;
        // having to do with "health" stuff
        public byte hp = 100; //max 100
        public byte armorTier = 0; //do they have a level 1, 2, or 3?
        public byte armorTapes = 0; //how many slots of that armor is *actually* taped up? (tier 2; 1/2 armorTapes > saved from shotty)
        public byte drinkies = 200; //game wants byte, I give byte, just like with hp. however, hp is turned into a float... so why not make it one?
        public byte tapies = 0; //amount of duct tape the player currently has. drinkies and tapies was named by me. I got bored.
        public bool isHealing = false;
        public bool isTaping = false;

        //simple solution to checking whether a person deserves the colors they get
        public ulong steamID = 0;
        public bool isDev = false;
        public bool isMod = false;
        public bool isFounder = false;

        //Booleans
        public bool dancing = false;
        public bool drinking = false;
        public bool reloading = false;
        public bool alive = true;

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
        }
    }
}
