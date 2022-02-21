using Lidgren.Network;
/*
so the idea of this class is to be a client object that a server object can store in a list.
*/

namespace WC.SARS
{
    class Player
    {
        // Make Some Empty Variables! \\
        public short assignedID; // Server Assigns this
        public NetConnection sender; // Server Also Assigns this
        public string myName = "愛子";
        public short avatarID; // Character & Avatar
        public short umbrellaID; // Umbrella
        public short gravestoneID; // Gravestone
        public short deathExplosionID; // Death Explosion
        public short[] emoteIDs; // Emote List (Length of 6; short; array)
        public short hatID; // Hat
        public short glassesID; // Glasses
        public short beardID; // Beard
        public short clothesID; // Clothes
        public short meleeID; // Melee
        public byte gunSkinIndexByIDAmmount; // Gunskin1
        public short[] gunSkinKeys; // Gunskin2 IDK
        public byte[] gunSkinIndexes; // Gunskin3 IDK

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

        //ya
        //this constructor thingy hasn't been updated in a while... maybe work on it at some point :3
        public Player(short assignID, short charID, short parasollID, short graveID, short deathEffectID, short[] danceIDs, short hatID, short glassID, short brdID, short clothingID, short meleeID, byte gunbyID, short[] indexLists, byte[] values)
        {
            this.assignedID = assignID;
            this.avatarID = charID;
            this.umbrellaID = parasollID;
            this.gravestoneID = graveID;
            this.deathExplosionID = deathEffectID;
            this.emoteIDs = danceIDs;
            this.hatID = hatID;
            this.glassesID = glassID;
            this.beardID = brdID;
            this.clothesID = clothingID;
            this.meleeID = meleeID;

            //the first one I know what it might be by name, but last two not too sure take a better look.
            this.gunSkinIndexByIDAmmount = gunbyID;
            this.gunSkinKeys = indexLists;
            this.gunSkinIndexes = values;

        }
    }
}
