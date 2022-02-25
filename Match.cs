using System;
using Lidgren.Network;
using System.Threading;

namespace WC.SARS
{
    class Match
    {
        
        private NetPeerConfiguration config;
        public NetServer server;
        public Player[] player_list;
        public Thread updateThread;
        private int matchSeed1, matchSeed2, matchSeed3; //these are supposed to be random
        private int slpTime, prevTime;
        private bool matchStarted, matchFull;
        public double timeUntilStart, gasAdvanceTimer;

        //these get to go at some point, or never. I'm quite lazy.
        public bool DEBUG_ENABLED;
        public bool ANOYING_DEBUG1;

        public Match(int port, string ip, bool db, bool annoying)
        {
            slpTime = 10;
            matchStarted = false;
            matchFull = false;
            player_list = new Player[64];
            timeUntilStart = 90.00;
            gasAdvanceTimer = -1;
            prevTime = DateTime.Now.Second;
            updateThread = new Thread(serverUpdateThread);

            DEBUG_ENABLED = db;
            ANOYING_DEBUG1 = annoying;
            config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.PingInterval = 22f;
            config.LocalAddress = System.Net.IPAddress.Parse(ip);
            config.Port = port;
            server = new NetServer(config); // todo make sure server actually starts before having to launch update thread?
            server.Start();
            updateThread.Start();
            NetIncomingMessage msg;
            while (true)
            {
                while ((msg = server.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.Data:
                            HandleMessage(msg);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            Logger.Header("~-- { Status Change} --~");
                            switch (msg.SenderConnection.Status)
                            {
                                case NetConnectionStatus.Connected:
                                    Logger.Success($"A new client has connected successfuly! Sender Address: {msg.SenderConnection}");
                                    NetOutgoingMessage acceptMsgg = server.CreateMessage();
                                    acceptMsgg.Write((byte)0);
                                    acceptMsgg.Write(true);
                                    server.SendMessage(acceptMsgg, msg.SenderConnection, NetDeliveryMethod.ReliableSequenced);
                                    break;
                                case NetConnectionStatus.Disconnected:
                                        Logger.Warn("Searching for player that disconnected.");
                                        //Logger.Warn($"{msg.SenderEndPoint} has disconnected...");
                                        short plr = getPlayerArrayIndex(msg.SenderConnection);
                                        if (plr != -1)
                                        {
                                            NetOutgoingMessage playerLeft = server.CreateMessage();
                                            playerLeft.Write((byte)46);
                                            playerLeft.Write(player_list[plr].myID);
                                            playerLeft.Write(false); //isAdminGhosting -- not quite sure how to determine this
                                            player_list[plr] = null;
                                            server.SendToAll(playerLeft, NetDeliveryMethod.ReliableOrdered);
                                            Logger.Success("Player Disconnected and dealt with successfully.");
                                        }
                                        else{ Logger.Failure("Well that is awfully strange. No one was found."); }
                                    break;
                                case NetConnectionStatus.Disconnecting:
                                    Logger.Warn($"Somebody is disconnecting...");
                                    break;
                            }
                            break;
                        case NetIncomingMessageType.ConnectionApproval:
                            Logger.Header("<< Incoming Connection >>");
                            //Console.WriteLine("Incoming Connection! Wowie!");
                            string clientKey = msg.ReadString();
                            if (clientKey == "flwoi51nawudkowmqqq") //look. if you're editing your game to even be able to connect, you already know this.
                            {
                                if (!matchStarted){
                                    Logger.Success("Connection Allowed.");
                                    msg.SenderConnection.Approve();
                                }
                                else{
                                    Logger.Failure("Connection Refused. Match in progress.");
                                    msg.SenderConnection.Deny($"The match has already begun, sorry!");
                                }

                            }
                            else { msg.SenderConnection.Deny($"Your client version key is incorrect.\n\nYour version key: {clientKey}"); Logger.Failure("Client Connected with wrong key..."); }
                            break;
                        case NetIncomingMessageType.DebugMessage:
                            Logger.DebugServer(msg.ReadString());
                            break;
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.ErrorMessage:
                            Logger.Failure("EPIC BLUNDER! " + msg.ReadString());
                            break;
                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                                //look I don't know why, but yes sending a message here does include the type ConnectionLatencyUpdated.
                                //Logger.Header($"ping from {msg.SenderConnection}");
                                NetOutgoingMessage pingBack = server.CreateMessage();
                                server.SendMessage(pingBack, msg.SenderConnection, NetDeliveryMethod.ReliableUnordered);
                            break;
                        default:
                            Logger.Failure("Unhandled type: " + msg.MessageType);
                            break;
                    }
                    server.Recycle(msg);
                }
                Thread.Sleep(slpTime);
            }
        }

        private void serverUpdateThread() //where most things get updated...
        {
            Logger.Success("Server update thread started.");

            //lobby
            while (!matchStarted)
            {
                if (player_list[player_list.Length - 1] != null && !matchFull)
                {
                    matchFull = true;
                    Logger.Basic("Match seems to be full!");
                }

                //check the count down timer
                if (!matchStarted && (player_list[0] != null)) { checkStartTime(); }
                //inform everyone of new time ^^


                //updating player info to all people in the match
                updateEveryoneOnPlayerPositions();
                updateEveryoneOnPlayerInfo();
                //updateServerTapeCheck(); --similar idea, about as stupid.

                //sleep for a sec 
                Thread.Sleep(slpTime); // ~1ms delay
            }


            //main game
            while (matchStarted)
            {
                //updating player info to all people in the match
                updateEveryoneOnPlayerPositions();
                updateEveryoneOnPlayerInfo();

                updateServerDrinkCheck();
                //updateServerTapeCheck(); --similar idea, about as stupid.
                updateEveryonePingList();
                test_SENDDUMMY();
                checkGasTime();

                //sleep for a sec 
                Thread.Sleep(slpTime); // ~1ms delay
            }
        }

        private void updateEveryoneLobbyStartTime()
        {
            NetOutgoingMessage sTimeMsg = server.CreateMessage();
            sTimeMsg.Write((byte)43);
            sTimeMsg.Write(timeUntilStart);
            server.SendToAll(sTimeMsg, NetDeliveryMethod.ReliableOrdered);
        }

        private void updateEveryoneOnPlayerPositions()
        {
            NetOutgoingMessage playerUpdate = server.CreateMessage();
            playerUpdate.Write((byte)11); // Header -- Basic Update Info

            //Find Length of Actual Players
            //Logger.Warn($"player_list length: {(byte)player_list.Length}");
            //playerUpdate.Write((byte)player_list.); // Ammount of times to loop (for amount of players, you know?

            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    playerUpdate.Write(i); // Ammount of times to loop (for amount of players, you know?
                    //Logger.Header($"list length: {i}");
                    //Logger.Warn($"sendEveryonePlayer i value: {i}");
                    break;
                }
                else { continue; }
            }

            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    playerUpdate.Write(player_list[i].myID); // may be able to simplfiy by just writing "I"
                    playerUpdate.Write(player_list[i].mouseAngle);
                    playerUpdate.Write(player_list[i].position_X); //REALLY need to fix this...
                    playerUpdate.Write(player_list[i].position_Y); //really need to fix this as well...
                }
                else { break; } //exits
            }
            server.SendToAll(playerUpdate, NetDeliveryMethod.ReliableSequenced);
        }
        private void updateEveryoneOnPlayerInfo()
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45); //b == 45
            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    msg.Write(i);
                    //unfortunately the amount of times the client is told to loop, is stated at the beginning.
                    //so, we have to find the length first, then come back and go through the list again...
                    //it isn't that it is slow, it just sucks ig.
                    break;
                }
            }

            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null) //just to make sure? TODO: make there be an actual reason to do this...
                {
                    msg.Write(player_list[i].myID);
                    msg.Write(player_list[i].hp);
                    msg.Write(player_list[i].armorTier);
                    msg.Write(player_list[i].armorTapes);
                    msg.Write(player_list[i].currWalkMode);
                    msg.Write(player_list[i].drinkies);
                    msg.Write(player_list[i].tapies);
                }
                else { break; }//this *may* cause some problems later
                /*this can be stated elsewhere, but the way this is dealt with currently WILL cause problems.
                what do we do when someone disconnects? do we just null their entry in the list?
                well that's fine if they were the last person, but what if they are 3/6? now players 4,5, and 6 are
                skipped over entirely now. so the way to keep it working the way it is now if to then reorder the
                whole entire list. which kind of stinks if you ask me, but that's the result of pushing things off.*/
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        private void test_SENDDUMMY()
        {
            NetOutgoingMessage dummy = server.CreateMessage();
            dummy.Write((byte)97);
            dummy.Write("a dummy makes money off a dummy");
            server.SendToAll(dummy, NetDeliveryMethod.ReliableUnordered);
        }

        private void updateEveryonePingList()
        {
            NetOutgoingMessage pings = server.CreateMessage();
            byte i = 0;
            for (i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    pings.Write(i); //problematic -- what if player_list[3 && 5] are normal, but player_list[4] == null? [5] is skipped...
                    break;
                }
            }
            for (byte j = 0; j < i; j++)
            {
                pings.Write(player_list[j].myID);
                pings.Write((short)420);//ping in ms
            }
            server.SendToAll(pings, NetDeliveryMethod.ReliableUnordered);
        }

        private void updateServerDrinkCheck()
        {
            //other than the attrocious constant checking, this also only heal the player by 5 each time, when in actuality
            //the player may be able to receive health juice in any amount and the game usually lets the player heal until
            //they are truly out of drinkies. this... this doesn't right now... so yeah...
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    if (player_list[i].isHealing)
                    {
                        if (player_list[i].hp != 100)
                        {
                            for (int j = 0; j < player_list[i].drinkies; j++)
                            {
                                if ((player_list[i].drinkies - 5) >= 0)
                                {
                                    player_list[i].drinkies -= 5;
                                    player_list[i].hp += 5;
                                    if (player_list[i].hp > 100)
                                    {
                                        player_list[i].hp = 100;
                                    }
                                }
                                else
                                {
                                    player_list[i].isHealing = false;
                                    NetOutgoingMessage tmp_drinkFinish = server.CreateMessage();
                                    tmp_drinkFinish.Write((byte)49);
                                    tmp_drinkFinish.Write(player_list[i].myID);
                                    server.SendToAll(tmp_drinkFinish, NetDeliveryMethod.ReliableSequenced);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            player_list[i].isHealing = false;
                            NetOutgoingMessage tmp_drinkFinish = server.CreateMessage();
                            tmp_drinkFinish.Write((byte)49);
                            tmp_drinkFinish.Write(player_list[i].myID);
                            server.SendToAll(tmp_drinkFinish, NetDeliveryMethod.ReliableSequenced);
                            break;
                        }
                    }
                }
                else { break; }
            }
        }
        private void checkStartTime()
        {
            if (timeUntilStart  > 0)
            {
                if (prevTime != DateTime.Now.Second)
                {
                    if ((timeUntilStart  % 2) < 1)
                    {
                        Logger.Basic($"seconds until start: {timeUntilStart }");
                        if (timeUntilStart != 0) { updateEveryoneLobbyStartTime(); }
                    }
                    timeUntilStart  -= 1;
                    prevTime = DateTime.Now.Second;
                }
            }
            else if (timeUntilStart  == 0)
            {
                //waits an extra second
                if (prevTime != DateTime.Now.Second)
                {
                    sendStartGame();
                    matchStarted = true;
                }
            }
        }
        private void checkGasTime()
        {
            //basically a copy and paste from checkStartTimer(). Any inaccuracies/inefficency there also appear here.
            if (gasAdvanceTimer > 0)
            {
                if (prevTime != DateTime.Now.Second)
                {
                    if ((gasAdvanceTimer % 2) < 1)
                    {
                        Logger.Basic($"time until send gas message: {gasAdvanceTimer}");
                    }
                    gasAdvanceTimer -= 1;
                    prevTime = DateTime.Now.Second;
                }
            }
            else if (gasAdvanceTimer == 0)
            {
                //this is so it waits an extra second
                if (prevTime != DateTime.Now.Second)
                {
                    gasAdvanceTimer = -1;
                    NetOutgoingMessage gasMoveMsg = server.CreateMessage();
                    gasMoveMsg.Write((byte)34);
                    gasMoveMsg.Write(5f); //move time -- higher values allow the gas to move slower, lower values mean fast gas
                    server.SendToAll(gasMoveMsg, NetDeliveryMethod.ReliableOrdered);
                }
            }
        }
        
        //TODO : Make this better :3
        private void sendStartGame()
        {
            if (DEBUG_ENABLED) { Logger.Warn("Sending game begin to all clients!"); }
            NetOutgoingMessage startMsg = server.CreateMessage();
            startMsg.Write((byte)6); //Header
            startMsg.Write(20f); //x1
            startMsg.Write(30f); //y1case 
            startMsg.Write(40f); //x2
            startMsg.Write(50f); //y2
            startMsg.Write((byte)1); //b4 -- one loop
            startMsg.Write((short)30); //readInt16
            startMsg.Write((short)600); // readInt16 -- percentage
            startMsg.Write((byte)1);//b5 -- one loop 
            startMsg.Write((short)120); //snowtime... something tells me reading ints is purposefully complicated...
            startMsg.Write((short)600);

            //Send message out
            server.SendToAll(startMsg, NetDeliveryMethod.ReliableUnordered);
        }


        // End of Update Thread

        // Start of HandleMessage
        

        private void HandleMessage(NetIncomingMessage msg)
        {
            //Stopwatch watch = new Stopwatch();
            //watch.Start();
            byte b = msg.ReadByte();
            if (DEBUG_ENABLED) {
                if (b != 14)
                {
                    Logger.Header($"Byte : {b}");
                } }

            switch (b)
            {
                // Request Authentication
                case 1:
                    Logger.Header($"Authentication Requestion\nSender: {msg.SenderEndPoint}\n");
                    sendAuthToPlayer(msg.SenderConnection);
                    break;

                case 3: // still has work to be done
                    Logger.Header($"Sender {msg.SenderEndPoint}'s Ready Received. Now, let's read their player-character info.");
                    serverHandlePlayerConnection(msg);
                    /*for (short i = 0; i < player_list.Length; i++)
                    {
                        if (player_list[i] == null)
                        {
                            //pID = i;
                            ulong steamID = msg.ReadUInt64(); //steamID64- this is from ME! :D
                            string readName = msg.ReadString(); //player's name from steam; this is also from me! :D
                            short charID = msg.ReadInt16(); // Character/Avatar ID
                            short umbrellaID = msg.ReadInt16(); // Umbrella ID
                            short gravestoneID = msg.ReadInt16(); // Gravestone ID
                            short deathExplosionID = msg.ReadInt16(); // Death Explosion ID
                            short[] emoteIDs = { msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), }; // Emote ID
                            short hatID = msg.ReadInt16(); // Hat ID
                            short glassesID = msg.ReadInt16(); // Glasses ID
                            short beardID = msg.ReadInt16(); // Beard ID
                            short clothesID = msg.ReadInt16(); // Clothes ID
                            short meleeID = msg.ReadInt16(); // MeleeWeaponID
                            byte skinIndexID = msg.ReadByte(); // GunSkinByGunID
                            short[] skinShorts = new short[skinIndexID];
                            byte[] skinValues = new byte[skinIndexID];
                            for (byte l = 0; l < skinIndexID; l++)
                            {
                                skinShorts[l] = msg.ReadInt16();
                                skinValues[l] = msg.ReadByte();
                            }
                            //short skinShort = msg.ReadInt16(); // indexInJSONFileList
                            //byte skinKey = msg.ReadByte(); // keyValuePair.Value

                            player_list[i] = new Player(i, charID, umbrellaID, gravestoneID, deathExplosionID, emoteIDs, hatID, glassesID, beardID, clothesID, meleeID, skinIndexID, skinShorts, skinValues);
                            player_list[i].sender = msg.SenderConnection;
                            player_list[i].myName = readName;
                            switch (steamID) // TODO: read from external file
                            {
                                case 76561198384352240:
                                    player_list[i].isMod = true;
                                    break;
                                case 76561198218282413:
                                    player_list[i].isMod = true;
                                    break;
                                case 76561198162222086:
                                    player_list[i].isMod = true;
                                    break;
                                case 76561198323046172:
                                    player_list[i].isMod = true;
                                    break;
                                default:
                                    player_list[i].isFounder = true;
                                    break;
                            }
                            sendClientMatchInfo2Connect(i, msg.SenderConnection);
                            break;
                        }
                    }

                    //no longer needed, but is useful.
                    if (DEBUG_ENABLED)
                    {
                        for (int i = 0; i < player_list.Length; i++)
                        {
                            if (player_list[i] != null)
                            {
                                Logger.Basic($"Player ID For Match: {player_list[i].myID}");
                                Logger.Basic($"Avatar/Character ID: {player_list[i].avatarID}");
                                Logger.Basic($"Umbrella ID: {player_list[i].umbrellaID}");
                                Logger.Basic($"Gravestone ID: {player_list[i].gravestoneID}");
                                Logger.Basic($"Death Explosion ID: {player_list[i].deathExplosionID}");
                                Logger.Basic($"Emote ID: {player_list[i].emoteIDs}");
                                Logger.Basic($"Hat ID: {player_list[i].hatID}");
                                Logger.Basic($"Glasses ID: {player_list[i].glassesID}");
                                Logger.Basic($"Beard ID: {player_list[i].beardID}");
                                Logger.Basic($"Clothes ID: {player_list[i].clothesID}");
                                Logger.Basic($"Melee Weapon ID: {player_list[i].meleeID}");
                                Logger.Basic($"Gun-Skin-by-Index-ID: {player_list[i].gunSkinIndexByIDAmmount}");
                                //Logger.Basic($"Unsure 1: {player_list[i].UNKNOWN_BYTE}");
                                //Logger.Basic($"Unsure 2: {player_list[i].UNKNOWN_DATA}");
                            }
                            else { break; }
                        }
                    }*/
                    break;

                case 5:
                    Logger.Header($"<< sending {msg.SenderEndPoint} player characters... >>");
                    sendPlayerCharacters();
                    break;
                case 7:
                    short plr = getPlayerID(msg.SenderConnection);
                    NetOutgoingMessage sendEject = server.CreateMessage();
                    sendEject.Write((byte)8);
                    sendEject.Write(player_list[plr].myID);
                    sendEject.Write(player_list[plr].position_X);
                    sendEject.Write(player_list[plr].position_Y);
                    sendEject.Write(true);
                    server.SendToAll(sendEject, NetDeliveryMethod.ReliableSequenced);
                    break;

                case 14:
                    float mAngle = msg.ReadFloat(); //before I changed this to float it was int16 idrk if that matters...
                    float actX = msg.ReadFloat();
                    float actY = msg.ReadFloat();
                    byte currentwalkMode = msg.ReadByte();

                    for (short i = 0; i < player_list.Length; i++)
                    {
                        if ((player_list[i] != null))
                        {
                            if (player_list[i].sender == msg.SenderConnection)
                            {
                                player_list[i].position_X = actX;
                                player_list[i].position_Y = actY;
                                player_list[i].mouseAngle = mAngle;
                                player_list[i].currWalkMode = currentwalkMode;
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    //annoying debug section
                    if (ANOYING_DEBUG1)
                    {
                        Logger.Warn($"Mouse Angle as a Short: {mAngle}");
                        Logger.Warn($"playerX: {actX}");
                        Logger.Warn($"playerY: {actY}");
                        Logger.Basic($"player WalkMode: {currentwalkMode}");
                    } 
                    break;
                case 16:

                    short weaponID = msg.ReadInt16(); //short -- WeaponId
                    byte slotIndex = msg.ReadByte();//byte -- slotIndex
                    float aimAngle = msg.ReadFloat();//float (myver) -- aimAngle
                    float spawnPoint_X = msg.ReadFloat();//float -- spawnPoint.X
                    float spawnPoint_Y = msg.ReadFloat();//float -- spawnPoint.Y
                    bool shotPointValid = msg.ReadBoolean();//bool -- shotPointValid
                    bool didHitADestruct = msg.ReadBoolean();//bool -- didHitDestructible
                    short destructCollisionPoint_X = 0; //short -- destructCollisionPoint.X
                    short destructCollisionPoint_Y = 0; //short -- destruct.CollisionPoint.y
                    if (didHitADestruct)
                    {
                        destructCollisionPoint_X = msg.ReadInt16();
                        destructCollisionPoint_Y = msg.ReadInt16(); // I think this is meant for the server but whatevs
                    }
                    short attackID = msg.ReadInt16();//short -- attackID
                    byte sendProjectileAnglesArrayLength = msg.ReadByte();//byte -- projectileAngles.Length


                    NetOutgoingMessage plrShot = server.CreateMessage();
                    plrShot.Write((byte)17);
                    plrShot.Write(getPlayerID(msg.SenderConnection)); //playerID of shot
                    plrShot.Write((ushort)1); //playerPing figure this out later I don't give a darn right now!
                    plrShot.Write(weaponID); //weaponID from shot
                    plrShot.Write(slotIndex); //slotIndex
                    plrShot.Write(attackID); //attackID
                    plrShot.Write(aimAngle); //angle
                    plrShot.Write(spawnPoint_X);
                    plrShot.Write(spawnPoint_Y);
                    plrShot.Write(shotPointValid);
                    plrShot.Write(sendProjectileAnglesArrayLength);
                    if (sendProjectileAnglesArrayLength > 0)
                    {
                        for (int i = 0; i < sendProjectileAnglesArrayLength; i++)
                        {
                            plrShot.Write(msg.ReadFloat());
                            plrShot.Write(msg.ReadInt16());
                            plrShot.Write(msg.ReadBoolean());
                        }
                    }
                    server.SendToAll(plrShot, NetDeliveryMethod.ReliableSequenced);

                    /* using this as a base for later...
                    short weaponID = msg.ReadInt16(); //short -- WeaponId
                    byte slotIndex = msg.ReadByte();//byte -- slotIndex
                    float aimAngle = msg.ReadFloat();//float (myver) -- aimAngle
                    float spawnPoint_X = msg.ReadFloat();//float -- spawnPoint.X
                    float spawnPoint_Y = msg.ReadFloat();//float -- spawnPoint.Y
                    bool shotPointValid = msg.ReadBoolean();//bool -- shotPointValid
                    bool didHitADestruct = msg.ReadBoolean();//bool -- didHitDestructible
                    short destructCollisionPoint_X = 0; //short -- destructCollisionPoint.X
                    short destructCollisionPoint_Y = 0; //short -- destruct.CollisionPoint.y
                    if (didHitADestruct)
                    {
                        destructCollisionPoint_X = msg.ReadInt16();
                        destructCollisionPoint_Y = msg.ReadInt16();
                    }
                    short attackID = msg.ReadInt16();//short -- attackID
                    byte sendProjectileAnglesArrayLength = msg.ReadByte();//byte -- projectileAngles.Length
                    float projectileInstAngle = 0;
                    short projectileID = 0;
                    bool didHit = false;
                    if (sendProjectileAnglesArrayLength > 0)
                    {
                        for (byte i = 0; i < sendProjectileAnglesArrayLength; i++)
                        {
                            projectileInstAngle = msg.ReadFloat();
                            Logger.Basic($"Projectile ID {i}.Angle: {projectileInstAngle}");
                            projectileID = msg.ReadInt16();
                            Logger.Basic($"Projectile ID {i}.ReadID: {projectileID}");
                            didHit = msg.ReadBoolean();
                            Logger.Basic($"Projectile ID {i}.didHit: {didHit}");
                        }
                    }

                    if (DEBUG_ENABLED)
                    {
                        Logger.Basic($"WeaponID: {weaponID}\nSlotIndex: {slotIndex}\naimAngle: {aimAngle}\nSpawn X: {spawnPoint_X}\nSpawn Y: {spawnPoint_Y}\nValid? {shotPointValid}" +
                            $"\nhitDestruct: {didHitADestruct}\nDestruct X: {destructCollisionPoint_X}\nDestruct Y: {destructCollisionPoint_Y}\nAttack ID: {attackID}\nArrayLength: {sendProjectileAnglesArrayLength}\nProjectile Inst Angle: {projectileInstAngle}\nProjectile ID: {projectileID}\nDid Hit? {didHit}");
                    }*/
                    break;
                case 18:
                    serverSendPlayerShoteded(msg);
                    break;
                case 21:
                    //Writes 21 > Write(int:lootID) > Write(byte:slotIndex) [EDIT 2/3/22: what? lol]
                    Player currPlayer = player_list[getPlayerArrayIndex(msg.SenderConnection)];
                    short item = (short)msg.ReadInt32();
                    byte index = msg.ReadByte();
                    if (DEBUG_ENABLED) { Logger.Basic($"Loot ID: {item}\nSlotIndex: {index}"); }
                    switch (index)
                    {
                        case 0:
                            currPlayer.equip1 = item;
                            currPlayer.equip1_rarity = 0;
                            break;
                        case 1:
                            currPlayer.equip2 = item;
                            currPlayer.equip2_rarity = 0;
                            break;
                        default:
                            Logger.Failure($"Well something went wrong with the index... index: {index}");
                            //pssst nothing went wrong (probs), throwables just aren't dealt with
                            break;
                    }
                    NetOutgoingMessage testMessage = server.CreateMessage();
                    testMessage.Write((byte)22);
                    testMessage.Write(currPlayer.myID); //player
                    testMessage.Write((int)item); // the item
                                                  //testMessage.Write(player_list[i].equip1);
                    testMessage.Write(index);
                    testMessage.Write((byte)4); //Forced Rarity -- seems only applicable in the shooting gallery
                    server.SendToAll(testMessage, NetDeliveryMethod.ReliableUnordered);
                    break;


                case 25:
                    serverHandleChatMessage(msg);
                    break;

                //clientSentSelectedSlot
                case 27:
                    serverSendSlotUpdate(msg.SenderConnection, msg.ReadByte());
                    break;

                case 29: //Received Reloading
                    NetOutgoingMessage sendReloadMsg = server.CreateMessage();
                    sendReloadMsg.Write((byte)30);
                    sendReloadMsg.Write(getPlayerID(msg.SenderConnection)); //sent ID
                    sendReloadMsg.Write(msg.ReadInt16()); //weapon ID
                    sendReloadMsg.Write(msg.ReadByte()); //slot ID
                    server.SendToAll(sendReloadMsg, NetDeliveryMethod.ReliableOrdered);
                    break;
                case 92: //Received DONE reloading
                    NetOutgoingMessage doneReloading = server.CreateMessage();
                    doneReloading.Write((byte)93);
                    doneReloading.Write(getPlayerID(msg.SenderConnection)); //playerID
                    server.SendToAll(doneReloading, NetDeliveryMethod.ReliableOrdered); //yes it's that simple
                    break;
                //figure it out.
                case 36:
                    serverSendBeganGrenadeThrow(msg);
                    break;
                case 38:
                    serverSendGrenadeThrowing(msg);
                    break;
                case 40:
                    serverSendGrenadeFinished(msg);
                    break;

                case 55: //Entering a hamball
                    short vehPlr = getPlayerArrayIndex(msg.SenderConnection);
                    short enteredVehicleID = msg.ReadInt16();
                    NetOutgoingMessage enterVehicle = server.CreateMessage();
                    enterVehicle.Write((byte)56);
                    enterVehicle.Write(player_list[vehPlr].myID); //sent ID
                    enterVehicle.Write(enteredVehicleID); //vehicle ID
                    enterVehicle.Write(player_list[vehPlr].position_X); //X
                    enterVehicle.Write(player_list[vehPlr].position_Y); //Y
                    player_list[vehPlr].vehicleID = enteredVehicleID;
                    server.SendToAll(enterVehicle, NetDeliveryMethod.ReliableOrdered);
                    break;

                //clientSendExitHamsterball
                case 57:
                    short vehPlrEx = getPlayerArrayIndex(msg.SenderConnection);
                    NetOutgoingMessage exitVehicle = server.CreateMessage();
                    exitVehicle.Write((byte)58);
                    exitVehicle.Write(player_list[vehPlrEx].myID); //sent ID
                    exitVehicle.Write(msg.ReadInt16()); //vehicle ID
                    exitVehicle.Write(player_list[vehPlrEx].position_X); //X
                    exitVehicle.Write(player_list[vehPlrEx].position_Y); //Y
                    player_list[vehPlrEx].vehicleID = -1;
                    server.SendToAll(exitVehicle, NetDeliveryMethod.ReliableOrdered); //yes it's that simple
                    break;
                //someone started healing...
                case 47:
                    serverSendPlayerStartedHealing(msg.SenderConnection, msg.ReadFloat(), msg.ReadFloat());
                    break;
                case 51:
                    serverSendCoconutEaten(msg);
                    break;
                case 53:
                    serverSendCutGrass(msg);
                    break;
                //clientSendVehicleHitPlayer
                case 60:
                    serverSendVehicleHitPlayer(msg);
                    break;

                //clientSendVehicleHitWall
                case 62:
                    serverSendPlayerHamsterballBounce(msg);
                    break;

                case 64:
                    short vehShotWepID = msg.ReadInt16();//WeaponID, ID of the weapon that shot the vehicle
                    short targetedVehicleID = msg.ReadInt16(); //targetVehicleID, vehicle that was shot at
                    short optionalProjectileID = msg.ReadInt16();
                    if (DEBUG_ENABLED)
                    {
                        Logger.Header($"Someone has shot a hamsterball...");
                        Logger.Basic($"Weapon ID: {vehShotWepID}\nTargeted Vehicle ID: {targetedVehicleID}\nProjectile ID: {optionalProjectileID}");
                    }
                    NetOutgoingMessage ballHit = server.CreateMessage();
                    ballHit.Write( (byte)65 );
                    ballHit.Write(getPlayerID(msg.SenderConnection));
                    ballHit.Write(targetedVehicleID);
                    ballHit.Write( (byte)0 );
                    ballHit.Write(optionalProjectileID);
                    server.SendToAll(ballHit, NetDeliveryMethod.ReliableUnordered);
                    break;

                //clientSendPlayerEmote
                case 66:

                    //Send Back a response
                    short ePlayerID = getPlayerID(msg.SenderConnection);
                    short ePlayerIndex = getPlayerArrayIndex(msg.SenderConnection);
                    NetOutgoingMessage emoteMsg = server.CreateMessage();
                    emoteMsg.Write((byte)67); //Header
                    emoteMsg.Write(ePlayerID); //obviously ID
                    emoteMsg.Write(msg.ReadInt16()); //Read emote id to then send to everyone!
                    server.SendToAll(emoteMsg, NetDeliveryMethod.ReliableUnordered);

                    //update player info rq
                    player_list[ePlayerIndex].position_X = msg.ReadFloat();
                    player_list[ePlayerIndex].position_Y = msg.ReadFloat();
                    break;

                case 72: // CLIENT_DESTORY_DOODAD
                    //short descXthing = msg.ReadInt16(); //x
                    //short descYthing = msg.ReadInt16(); //y
                    
                    //descBroke.Write(msg.ReadInt16()); //xSpot
                    //descBroke.Write(msg.ReadInt16()); //ySpot -- next read will be optionalProjectileID


                    NetOutgoingMessage descBroke = server.CreateMessage();
                    descBroke.Write((byte)73); //SERVER_DESTROY_DOODAD
                    descBroke.Write((short)31257); //at the beginning it searching for a short; HOWEVER, it serves no true purpose
                    // the only thing that the game does with this value is read and discard it. kind of wacky!
                    descBroke.Write(msg.ReadInt16()); //x 
                    descBroke.Write(msg.ReadInt16()); //y


                    descBroke.Write((short)1); // how many collision points to change, this may fluctuate
                    descBroke.Write((short)5); //was descX
                    descBroke.Write((short)6); //was descY
                    descBroke.Write((byte)0);
                    descBroke.Write((byte)1);
                    descBroke.Write((short)4);

                    server.SendToAll(descBroke, NetDeliveryMethod.ReliableUnordered);

                    /* goes to: GameServerSentDoodadDestroyed
                     * GameServerSentDoodadDestroyed(desctructX, destructY,
                     * L[IntPoints]:Points to Change, CollisionType : collisionTOChangeTO,
                     * List[short] : damaged player IDs)
                     * 
                     */

                    break;

                case 74: //attack windUP -- seems to have something to do with the minigun... not noticed elsewhere.
                    Logger.testmsg("\nplease note this occurred!!! attack windup. well that's rare...\n-- please note this\n");
                    serverSendAttackWindUp(msg);
                    break;

                case 75: //attack windDOWN
                        Logger.testmsg("\nplease note this occurred!!! attack winddown. well that's even rarer...\n-- please note this\n");
                        serverSendAttackWindDown(getPlayerID(msg.SenderConnection), msg.ReadInt16());
                    break;
                case 87:
                        serverSendDepployedTrap(msg);
                    break;
                case 90: //reload weapon
                    NetOutgoingMessage plrCanceled = server.CreateMessage();
                    plrCanceled.Write((byte)91);
                    plrCanceled.Write(getPlayerID(msg.SenderConnection));
                    server.SendToAll(plrCanceled, NetDeliveryMethod.ReliableSequenced);
                    break;

                case 97: //dummy message << if you got this, the client believes it is lagging
                    NetOutgoingMessage dummyMsg = server.CreateMessage();
                    dummyMsg.Write((byte)97);
                    server.SendMessage(dummyMsg, msg.SenderConnection, NetDeliveryMethod.UnreliableSequenced);
                    break;
                //clientSendDucttaping
                case 98:
                    serverSendPlayerStartedTaping(msg.SenderConnection, msg.ReadFloat(), msg.ReadFloat());
                    break;

                default:
                    Logger.missingHandle(b.ToString());
                    break;

            }

            //watch.Stop();
            //Logger.Header($"time taken: {watch.Elapsed}");
            //this stopwatch is very much not needed.
        }
        //message 1 -> send 2
        private void sendAuthToPlayer(NetConnection client)
        {
            NetOutgoingMessage acceptMsg = server.CreateMessage();
            acceptMsg.Write((byte)2);
            acceptMsg.Write(true);
            server.SendMessage(acceptMsg, client, NetDeliveryMethod.ReliableOrdered);
            Logger.Success($"Server sent {client.RemoteEndPoint} their accept message!");
        }

        private void serverHandlePlayerConnection(NetIncomingMessage msg)
        {
            //Read the player's character info and stuff
            ulong steamID = msg.ReadUInt64(); //not in base
            string steamName = msg.ReadString();//not in base -- must be added in
            short charID = msg.ReadInt16();
            short umbrellaID = msg.ReadInt16();
            short graveID = msg.ReadInt16();
            short deathEffectID = msg.ReadInt16();
            short[] emoteIDs =
            {
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(), };
            short hatID = msg.ReadInt16();
            short glassesID = msg.ReadInt16();
            short beardID = msg.ReadInt16();
            short clothesID = msg.ReadInt16();
            short meleeID = msg.ReadInt16();
            byte gunSkinCount = msg.ReadByte();
            short[] gunskinGunID = new short[gunSkinCount];
            byte[] gunSkinIndex = new byte[gunSkinCount];
            for (int l = 0; l < gunSkinCount; l++)
            {
                gunskinGunID[l] = msg.ReadInt16();
                gunSkinIndex[l] = msg.ReadByte();
            }

            //find an empty slot
            sortPlayersListNull(); //need to find an empty i
            //TODO :: make sure this finds the REAL non-existent value
            for (short i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    player_list[i] = new Player(i, charID, umbrellaID, graveID, deathEffectID, emoteIDs, hatID, glassesID, beardID, clothesID, meleeID, gunSkinCount, gunskinGunID, gunSkinIndex, steamName, msg.SenderConnection);
                    sendClientMatchInfo2Connect(i, msg.SenderConnection);
                    break;
                }
            }
        }

        //message 3 > << message 4 >> --still needs working ------ make seed random
        private void sendClientMatchInfo2Connect(short sendingID, NetConnection receiver)
        {
            // send a message back to the connecting player... \\
            NetOutgoingMessage mMsg = server.CreateMessage();
            mMsg.Write((byte)4); //Header (doesn't matter)
            mMsg.Write(sendingID); // Assigned Player ID

            // TODO : Make Seed Randomized...
            mMsg.Write(351301); //seed 1 -- int32
            mMsg.Write(5328522); //seed 2 -- int32 
            mMsg.Write(9037281); //seed 3 -- int32 
            // TODO : MAKE SEED RANDOMIZED

            mMsg.Write(timeUntilStart); //time at which game will start [double] 
            mMsg.Write("yerhAGJ"); // match UUID -- string -- *should* be random but is fine for now
            mMsg.Write("solo"); // game mode [solo, duo, squad]
            mMsg.Write((float)0); //x -- No clue? -- maybe has to do with flightpath
            mMsg.Write((float)0); //y
            mMsg.Write((float)8000); //x2
            mMsg.Write((float)8000); //y2 -- No clue -- flight start and end points ig

            mMsg.Write((byte)0); // amount of times to loop through thing ig but I skipped out. something with gallery targets
            mMsg.Write((byte)0); // that gallery target's score or whatever but I don't give a darn

            server.SendMessage(mMsg, receiver, NetDeliveryMethod.ReliableOrdered);
            //msg.SenderConnection.Disconnect("Currently Testing Stuff! Please come back later!");
        }

        //message 5 > send10
        private void sendPlayerCharacters()
        {
            NetOutgoingMessage sendPlayerPosition = server.CreateMessage();
            sendPlayerPosition.Write((byte)10);
            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    sendPlayerPosition.Write(i); // Ammount of times to loop (for amount of players, you know?
                    break;
                }
            }

            // loop through the list of all of the players in the match \\
            for (short i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    Logger.Header($"Sending << player_list[{i}] >>");
                    //For Loop Start // this byte may be a list of all players. I'm not sure though!
                    sendPlayerPosition.Write(i); //num4 / myAssignedPlayerID? [SHORT]
                    sendPlayerPosition.Write(player_list[i].charID); //charIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].umbrellaID); //umbrellaIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].gravestoneID); //gravestoneIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].deathEffectID); //explosionIndex [SHORT]
                    for (int j = 0; j < player_list[i].emoteIDs.Length; j++)
                    {
                        Logger.Warn("Loop Ammount: " + j);
                        sendPlayerPosition.Write(player_list[i].emoteIDs[j]); //emoteIndex [SHORT]
                    }
                    sendPlayerPosition.Write(player_list[i].hatID); //hatIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].glassesID); //glassesIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].beardID); //beardIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].clothesID); //clothesIndex [SHORT]
                    sendPlayerPosition.Write(player_list[i].meleeID); //meleeIndex [SHORT]

                    //Really Confusing Loop
                    sendPlayerPosition.Write(player_list[i].gunSkinCount);
                    for (byte l = 0; l < player_list[i].gunSkinCount; l++)
                    {
                        sendPlayerPosition.Write(player_list[i].gunskinKey[l]); //Unknown Key
                        sendPlayerPosition.Write(player_list[i].gunskinValue[l]); //Unknown Value
                    }

                    //Positioni?
                    sendPlayerPosition.Write(player_list[i].position_X);
                    sendPlayerPosition.Write(player_list[i].position_Y);

                    //sendPlayerPosition.Write((float)508.7); //x2
                    //sendPlayerPosition.Write((float)496.7); //y2
                    sendPlayerPosition.Write(player_list[i].myName); //playername

                    sendPlayerPosition.Write(player_list[i].currenteEmote); //num 6 - int16 -- I think this is the emote currently in use. so... defualt should be none/ -1
                    sendPlayerPosition.Write(player_list[i].equip1); //equip -- int16
                    sendPlayerPosition.Write(player_list[i].equip2); //equip2 - int16
                    sendPlayerPosition.Write(player_list[i].equip1_rarity); // equip rarty byte
                    sendPlayerPosition.Write(player_list[i].equip2_rarity); // equip rarity 2 -- byte
                    sendPlayerPosition.Write(player_list[i].curEquipIndex); // current equip index -- byte
                                                                           //sendPlayerPosition.Write((short)12); //num8 -- something with emotes?
                    /* 0 -- Default; 4-- Clap; 10 -- Russian; 11- Laugh; 
                     */
                    sendPlayerPosition.Write(player_list[i].isDev); //isDev
                    sendPlayerPosition.Write(player_list[i].isMod); //isMod
                    sendPlayerPosition.Write(player_list[i].isFounder); //isFounder
                    sendPlayerPosition.Write((short)450); //accLvl -- short
                    sendPlayerPosition.Write((byte)1); //b6 -- not too sure, but normal byte
                    sendPlayerPosition.Write((short)25); //list of something gets added...

                }
                else { Logger.Success($"breakout! count: {i}"); break; }// break out of loop
            }
            Logger.Success("Going to be sending new player all other player positions.");
            server.SendToAll(sendPlayerPosition, NetDeliveryMethod.ReliableSequenced); // CHANGED FROM BOTTOM TO THIS IDK WHAT IT DOES
                                                                                       //server.SendMessage(sendPlayerPosition, msg.SenderConnection, NetDeliveryMethod.UnreliableSequenced);
        }


        //18 > 19
        private void serverSendPlayerShoteded(NetIncomingMessage message)
        {
            /*
            netOutgoingMessage.Write(targetPlayerID);
            netOutgoingMessage.Write(weaponID);
            netOutgoingMessage.Write(optionalProjectileID);
            netOutgoingMessage.Write(hitPosition.x);
            netOutgoingMessage.Write(hitPosition.y);*/

            short hitPlayerID = message.ReadInt16();
            short wepID = message.ReadInt16();
            short projID = message.ReadInt16();
            float hitX = message.ReadFloat();
            float hitY = message.ReadFloat();

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)19);
            msg.Write(getPlayerID(message.SenderConnection));
            msg.Write(hitPlayerID);
            msg.Write(projID);
            msg.Write((byte)0);
            msg.Write((short)-1);

            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

            /*
             * short fromPlayerID = msg.ReadInt16();
             * short toPlayerID = msg.ReadInt16();
             * short optionalProjectileID = msg.ReadInt16();
             * byte dinkedArmorInitialAmount = msg.ReadByte();
             * short num17 = msg.ReadInt16();
             * byte vehicleHP = 0;
                if (num17 >= 0)
                    {
                        vehicleHP = msg.ReadByte();
                        }
					if (GameServerManager.responderGame != null)
					{
					GameServerManager.responderGame.GameServerSentServerAttackHit(fromPlayerID, toPlayerID, optionalProjectileID, dinkedArmorInitialAmount, num17, vehicleHP);
				return;
			}*/
        }

        //25 > 26/94/106 -- is A HUGE mess
        private void serverHandleChatMessage(NetIncomingMessage message)
        {
            //this can either be a command, or an actual chat message. let's find out if it was a command
            if (message.PeekString().StartsWith("/"))
            {

                string[] command = message.PeekString().Split(" ", 8);
                string responseMsg = "command executed... no info given...";
                short id, id2, amount;
                float cPosX, cPosY;
                Logger.Warn($"Player {player_list[getPlayerArrayIndex(message.SenderConnection)].myID} ({player_list[getPlayerArrayIndex(message.SenderConnection)].myName}) used {command[0]}");
                switch (command[0])
                {
                    case "/help":
                        Logger.Success("user has used help command");
                        if (command.Length >= 2)
                        {
                            switch (command[1])
                            {
                                case "help":
                                    responseMsg = "\nThis command will give information about other commands!\nUsage: /help {page}";
                                    break;
                                case "heal":
                                    responseMsg = "\nHeals a certain player's health by the inputed value.\nExample: /heal 0 50";
                                    break;
                                case "teleport":
                                    responseMsg = "\nTeleports the user to provided cordinates.\nExample: /teleport 200 500";
                                    break;
                                case "tp":
                                    responseMsg = "\nTeleports given player_1 TO given player_2.\nExample: /teleport 2 0";
                                    break;
                                case "moveplayer":
                                    responseMsg = "\nTeleports provided player to given cordinates.\nExample: /teleport 0 5025 1020";
                                    break;
                                /*case "2":
                                    responseMsg = "Command Page 2 -- List of usabble Commands\n/help {page/command}\n/heal {ID} {AMOUNT}";
                                    break;
                                case "3":

                                    break;*/
            default:
                                    responseMsg = $"Invalid help entry '{command[1]}'.\nPlease see '/help' for a list of usable commands.";
                                    break;
                            }
                        }
                        else
                        {
                            responseMsg = "\n(1) List of usable commads:" +
                                "\n/help {page}" +
                                "\n/heal {ID} {AMOUNT}" +
                                "\n/sethp {ID} {AMOUNT}" +
                                "\n/teleport {positionX} {positionY}" +
                                "\n/tp {playerID1} {playerID2}" +
                                "\n/moveplayer {playerID} {X} {Y}" +
                                "\nType '/help [command]' for more information";
                        }
                        
                        break;
                    case "/heal":
                        Logger.Success("user has heal command");
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                if (amount - player_list[id].hp <= 0)
                                {
                                    player_list[id].hp += (byte)amount;
                                    if (player_list[id].hp > 100) { player_list[id].hp = 100; }
                                    responseMsg = $"Healed player {id} ({player_list[id].myName} by {amount})";
                                }
                                else
                                {
                                    responseMsg = "Wrong player ID or provided health value is too high.";
                                }
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /heal {ID} {AMOUNT}"; }
                        break;
                    case "/sethp":
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                if (amount > 100) { amount = 100; }
                                player_list[id].hp = (byte)amount;
                                responseMsg = $"Set player {id} ({player_list[id].myName})'s health to {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /sethp {ID} {AMOUNT}"; }
                        break;
                    case "/teleport":
                        if (command.Length > 3)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                cPosX = float.Parse(command[2]);
                                cPosY = float.Parse(command[3]);

                                NetOutgoingMessage forcetoPos = server.CreateMessage();
                                forcetoPos.Write((byte)8); forcetoPos.Write(id);
                                forcetoPos.Write(cPosX); forcetoPos.Write(cPosY); forcetoPos.Write(false);
                                server.SendToAll(forcetoPos, NetDeliveryMethod.ReliableOrdered);

                                player_list[id].position_X = cPosX;
                                player_list[id].position_Y = cPosY;
                                responseMsg = $"Moved player {id} ({player_list[id].myName}) to ({cPosX}, {cPosY}). ";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /teleport {ID} {positionX} {positionY}"; }
                        break;
                    case "/tp":
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                id2 = short.Parse(command[1]);


                                NetOutgoingMessage forcetoPos = server.CreateMessage();
                                forcetoPos.Write((byte)8); forcetoPos.Write(id);
                                forcetoPos.Write(player_list[id2].position_X); forcetoPos.Write(player_list[id2].position_Y); forcetoPos.Write(false);
                                server.SendToAll(forcetoPos, NetDeliveryMethod.ReliableOrdered);

                                player_list[id].position_X = player_list[id2].position_X;
                                player_list[id].position_Y = player_list[id2].position_Y;
                                responseMsg = $"Moved player {id} ({player_list[id].myName}) to player {id2} ({player_list[id2].myName}). ";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /tp {playerID1} {playerID2}"; }
                        break;
                    case "/time":
                        if (!matchStarted)
                        {
                            if (command.Length == 2)
                            {
                                double newTime;
                                if (double.TryParse(command[1], out newTime))
                                {
                                    responseMsg = $"New time which the game will start: {newTime}";
                                    timeUntilStart = newTime;
                                    NetOutgoingMessage sTimeMsg2 = server.CreateMessage();
                                    sTimeMsg2.Write((byte)43);
                                    sTimeMsg2.Write(timeUntilStart);
                                    server.SendToAll(sTimeMsg2, NetDeliveryMethod.ReliableOrdered);
                                }
                                else
                                {
                                    responseMsg = $"Inputed value '{command[1]}' is not a valid time.\nValid input example: /time 20";
                                }
                            }
                            else
                            {
                                responseMsg = $"The game *should* begin in {timeUntilStart} seconds.";
                                NetOutgoingMessage sTimeMsg = server.CreateMessage();
                                sTimeMsg.Write((byte)43);
                                sTimeMsg.Write(timeUntilStart);
                                server.SendToAll(sTimeMsg, NetDeliveryMethod.ReliableOrdered);
                            }
                        }
                        else
                        {
                            responseMsg = "You cannot change the start time. The match has already started.";
                        }
                        break;
                    case "/makecircle":
                        if (command.Length == 8)
                        {
                            try
                            {
                                float gx1, gy1, gx2, gy2, gr1, gr2, gtime;
                                gx1 = float.Parse(command[1]);
                                gy1 = float.Parse(command[2]);
                                gr1 = float.Parse(command[3]);
                                gx2 = float.Parse(command[4]);
                                gy2 = float.Parse(command[5]);
                                gr2 = float.Parse(command[6]);
                                gtime = float.Parse(command[7]);

                                NetOutgoingMessage gCircCmdMsg = server.CreateMessage();
                                gCircCmdMsg.Write( (byte)33 );
                                gCircCmdMsg.Write(gx1); gCircCmdMsg.Write(gy1);
                                gCircCmdMsg.Write(gx2); gCircCmdMsg.Write(gy2);
                                gCircCmdMsg.Write(gr1); gCircCmdMsg.Write(gr2);
                                gCircCmdMsg.Write(gtime);

                                server.SendToAll(gCircCmdMsg, NetDeliveryMethod.ReliableOrdered);
                                gasAdvanceTimer = (double)gtime;
                                responseMsg = $"Started Gas Warning:\nCirlce Major:\nCenter: ({gx1}, {gy1})\nRadius: {gr1}\nCirlce Minor:\nCenter: ({gx2}, {gy2})\nRadius: {gr2}\n\nTime Until Incoming: ~{gtime} seconds";

                            }
                            catch
                            {
                                responseMsg = "All fields for this command are integer values. One or more argument was not an integer. Please try again. (Valid Ex: 1, 0.25; Invalid: 1/2, one)";
                            }
                        }
                        else
                        {
                            responseMsg = "Invalid arguments. Command Usage: /makecricle {C1 Position X} {C1 Position Y} {C1 Radius} {C2 Position X} {C2 Position Y} {C2 Radius} {DELAY}";
                        }
                        break;
                    case "/list":
                        NetOutgoingMessage plrlistMsg = server.CreateMessage();
                        plrlistMsg.Write((byte)97);
                        plrlistMsg.Write("heeey idk what this really does tbh...");
                        server.SendToAll(plrlistMsg, NetDeliveryMethod.ReliableOrdered);
                        responseMsg = "command executed successfully. anything happen?";
                        break;
                    case "/divemode":
                        if (command.Length > 1)
                        {
                            bool isDive;
                            if (bool.TryParse(command[1], out isDive))
                            {
                                NetOutgoingMessage cParaMsg = server.CreateMessage();
                                cParaMsg.Write((byte)109);
                                cParaMsg.Write((short)0);
                                cParaMsg.Write(isDive);
                                server.SendToAll(cParaMsg, NetDeliveryMethod.ReliableOrdered);
                                responseMsg = $"Parachute Mode Changed. Dive: {isDive}";
                            }
                            else
                            {
                                responseMsg = $"Provided value '{command[1]}' is not a true/false value. Please try again.";
                            }
                        }
                        else
                        {
                            responseMsg = $"Insufficient amount of arguments provided. This command takes 1. Given: {command.Length-1}.";
                        }
                        break;
                    case "/startshow":
                        if (command.Length > 1)
                        {
                            byte showNum;
                            if (byte.TryParse(command[1], out showNum))
                            {
                                NetOutgoingMessage cParaMsg = server.CreateMessage();
                                cParaMsg.Write((byte)104);
                                cParaMsg.Write(showNum);
                                server.SendToAll(cParaMsg, NetDeliveryMethod.ReliableOrdered);
                                responseMsg = $"Played AviaryShow #{showNum}";
                            }
                            else
                            {
                                responseMsg = $"Provided value '{command[1]}' is not valid. Please try again. (Valid values include: 0, 1, and 2)";
                            }
                        }
                        else
                        {
                            responseMsg = $"Insufficient amount of arguments provided. This command takes 1. Given: {command.Length - 1}.";
                        }
                        break;
                    case "/forceland":
                        if (command.Length > 1)
                        {
                            short forceID;
                            if(short.TryParse(command[1], out forceID))
                            {
                                if (!(forceID < 0) && !(forceID > 64))
                                {
                                    for(int fl = 0; fl < player_list.Length; fl++)
                                    {
                                        if (player_list[fl] != null && player_list[fl]?.myID == forceID)
                                        {
                                            NetOutgoingMessage sendEject = server.CreateMessage();
                                            sendEject.Write((byte)8);
                                            sendEject.Write(player_list[fl].myID);
                                            sendEject.Write(player_list[fl].position_X);
                                            sendEject.Write(player_list[fl].position_Y);
                                            sendEject.Write(true);
                                            server.SendToAll(sendEject, NetDeliveryMethod.ReliableSequenced);
                                            responseMsg = "Command executed successfully?";
                                            break;
                                        }
                                        responseMsg = $"Player ID {forceID} not found.";
                                    }
                                }
                                else
                                {
                                    responseMsg = $"Provided argument, '{forceID}' not valid. 0-64.";
                                }
                            }
                        }
                        else
                        {
                            responseMsg = $"Insufficient amount of arguments provided. This command takes 1. Given: {command.Length - 1}.";
                        }

                        break;

                    default:
                        Logger.Failure("Invalid command used.");
                        responseMsg = "Invalid command provided. Please see '/help' for a list of commands.";
                        break;
                }
                //now send response to player...
                NetOutgoingMessage allchatmsg = server.CreateMessage();
                allchatmsg.Write((byte)94);
                allchatmsg.Write(getPlayerID(message.SenderConnection)); //ID of player who sent msg
                allchatmsg.Write(responseMsg);
                server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
            } 
             else
            {
                //Regular message.
                NetOutgoingMessage allchatmsg = server.CreateMessage();
                allchatmsg.Write((byte)26);
                allchatmsg.Write(getPlayerID(message.SenderConnection)); //ID of player who sent msg
                allchatmsg.Write(message.ReadString());
                allchatmsg.Write(false);
                server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
            }
        }

        //got 27 > send 28
        private void serverSendSlotUpdate(NetConnection snd, byte sentSlot)
        {
            Player plr = player_list[getPlayerArrayIndex(snd)];
            plr.activeSlot = sentSlot;

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)28);
            msg.Write(plr.myID);
            msg.Write(sentSlot);
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //got 36 > send 37
        private void serverSendBeganGrenadeThrow(NetIncomingMessage message)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)37);
            msg.Write(getPlayerID(message.SenderConnection));
            msg.Write(message.ReadInt16());
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        //got 38 > send 39
        private void serverSendGrenadeThrowing(NetIncomingMessage message)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)39);
            for (byte i = 0; i < 3; i++)
            {
                msg.Write(message.ReadFloat()); //x
                msg.Write(message.ReadFloat()); //y
            }
            short grenadeID = message.ReadInt16();
            msg.Write(grenadeID);
            msg.Write(grenadeID);//likely needs to be unique. not sure how. maybe just make the server have its own counter
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        //r40 >
        private void serverSendGrenadeFinished(NetIncomingMessage message){
            float x = message.ReadFloat();
            float y = message.ReadFloat();
            float nadeHeight = message.ReadFloat(); ;
            short nadeID = message.ReadInt16();

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write( (byte)41 ); msg.Write(getPlayerID(message.SenderConnection));
            msg.Write(nadeID);
            msg.Write(x); msg.Write(y);
            msg.Write(nadeHeight);
            msg.Write((byte)1);
            msg.Write((short)0);// this should be a list of all players that are within the blast radius

            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        //send 51
        private void serverSendCoconutEaten(NetIncomingMessage message)
        {
            Player client = player_list[getPlayerArrayIndex(message.SenderConnection)];
            if (client.hp < 200)
            {
                client.hp += 5;
                if (client.hp > 200) { client.hp = 200; }
            }
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)52);
            msg.Write(getPlayerID(message.SenderConnection));
            msg.Write(message.ReadInt16());
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //r(53) >> s(54)
        private void serverSendCutGrass(NetIncomingMessage message)
        {
            byte bladesCut = message.ReadByte();
            NetOutgoingMessage grassMsg = server.CreateMessage();
            grassMsg.Write((byte)54);
            grassMsg.Write(getPlayerID(message.SenderConnection));
            grassMsg.Write(bladesCut);
            for (byte i = 0; i < bladesCut; i++)
            {
                grassMsg.Write(message.ReadInt16()); //x
                grassMsg.Write(message.ReadInt16()); //y
            }

            server.SendToAll(grassMsg, NetDeliveryMethod.ReliableOrdered);
        }

        //send 61
        private void serverSendVehicleHitPlayer(NetIncomingMessage message)
        {
            Logger.Header("--  Vehicle Hit Player  --");
            Logger.Basic($"Target Player ID: {message.ReadInt16()}\nSpeed: {message.ReadFloat()}");
            //It isn't that the bottom code doesn't work, it is just that the "correct" speed value needs to be found.

            /*
            //client SENDS this
            NetOutgoingMessage netOutgoingMessage = GameServerManager.netClient.CreateMessage();
            netOutgoingMessage.Write(60);
            netOutgoingMessage.Write(targetPlayerID);
            netOutgoingMessage.Write(speed);*/
            Player plrA = player_list[getPlayerArrayIndex(message.SenderConnection)];
            NetOutgoingMessage vehicleHit = server.CreateMessage();
            vehicleHit.Write((byte)61); //Message #61
            vehicleHit.Write(plrA.myID); //player who hit
            vehicleHit.Write(message.ReadInt16()); //player who GOT hit
            //TODO: redo player list so that can actually figure out how to find whether or not palyer died
            if (message.ReadFloat() > 50f){
                vehicleHit.Write(true); }
            else{ vehicleHit.Write(false); }
            vehicleHit.Write(plrA.vehicleID);
            vehicleHit.Write((byte)0); //idk
            vehicleHit.Write((byte)2);

            server.SendToAll(vehicleHit, NetDeliveryMethod.ReliableOrdered);

            
            /*
            //client gets this
            short fromPlayerID2 = msg.ReadInt16();
            short toPlayerID2 = msg.ReadInt16();
            bool didKillPlayer = msg.ReadBoolean();
            short fromVehicleIndex = msg.ReadInt16();
            short num33 = msg.ReadInt16();
            byte optionalTargetVehicleHP = 0;
            if (num33 >= 0)
            {
                optionalTargetVehicleHP = msg.ReadByte();
            }
            if (GameServerManager.responderGame != null)
            {
                GameServerManager.responderGame.GameServerSentVehicleHitPlayer(fromPlayerID2, toPlayerID2, fromVehicleIndex, num33, optionalTargetVehicleHP, didKillPlayer);
                return;
            }*/
        }

        //send 63
        private void serverSendPlayerHamsterballBounce(NetIncomingMessage message)
        {
            Player plr = player_list[getPlayerArrayIndex(message.SenderConnection)];
            NetOutgoingMessage smsg = server.CreateMessage();
            smsg.Write((byte)63);
            smsg.Write(plr.myID);
            smsg.Write(plr.vehicleID);
            server.SendToAll(smsg, NetDeliveryMethod.ReliableUnordered);
        }
        //send 75 -- something with minigun?
        private void serverSendAttackWindUp(NetIncomingMessage message)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)75);
            msg.Write(getPlayerID(message.SenderConnection)); //playerID
            msg.Write(message.ReadInt16()); //weaponID
            msg.Write(message.ReadByte()); //slotIndex
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //send 77 -- something wtih minigun?
        private void serverSendAttackWindDown(short plrID, short weaponID)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)77);
            msg.Write(plrID);
            msg.Write(weaponID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //client[47] > server[48] -- pretty much a copy of sendingTape and stuff... info inside btw...
        private void serverSendPlayerStartedHealing(NetConnection sender, float posX, float posY)
        {
            Player plr = player_list[getPlayerArrayIndex(sender)];
            plr.position_X = posX;
            plr.position_Y = posY;
            plr.isHealing = true;

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)48);
            msg.Write(plr.myID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
            /* so this whole thing only tells the person/everyone that the person who sent this whole message has
             * started healing. their game won't update their hp, juice count, tape, how much got taped, etc.
             * so, that all has to be done on a separate function for the server. sooo figure that out later
             * when it is time to properly tackle healing and stuff. should not be too difficult*/
        }

        //r[87] > s[111]
        private void serverSendDepployedTrap(NetIncomingMessage message)
        {
            /* client > server
             * float x
             * float y
             * short ID
             */
            //float tX, tY; --idk
            //short plrID; -- idkidk

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)111);
            msg.Write(getPlayerID(message.SenderConnection));
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

            /* server > client
             * short playerID
             * short trapID
             */
        }

        //client[98] > server[99] -- started taping
        private void serverSendPlayerStartedTaping(NetConnection sender, float posX, float posY)
        {
            Player plr = player_list[getPlayerArrayIndex(sender)];
            plr.position_X = posX;
            plr.position_Y = posY;
            plr.isTaping = true;

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)99);
            msg.Write(plr.myID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        
        private void sortPlayersListNull()
        {
            Player[] temp_plrlst = new Player[player_list.Length]; //yeah I mean I think the game caps it at 64 but you know it's fine
            byte newIndex = 0;
            for (byte i = 0; i < player_list.Length; i++)
            {
                if(player_list[i] != null)
                {
                    temp_plrlst[newIndex] = player_list[i];
                    newIndex++;
                }
            }
            player_list = temp_plrlst;
        }
        private void sortPlayersListIDs()
        {
            Player[] temp_plrlst = new Player[player_list.Length];;
            for (int i = 0; i < player_list.Length; i++)
            {
                for(int j = i+1; j < player_list.Length; j++)
                {
                    if (player_list[i]?.myID < player_list[j]?.myID)
                    {
                        temp_plrlst[i] = player_list[i];
                        player_list[i] = player_list[j];
                        player_list[j] = temp_plrlst[i];
                    }
                }
            }
            player_list = temp_plrlst;
        }

        //Helper Functions to get playerID
        private short getPlayerID(NetConnection thisSender)
        {
            short id = -1;
            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    if (player_list[i].sender == thisSender)
                    {
                        id = player_list[i].myID;
                        break;
                    }
                }
            }
            return id;
        }//Helper Function to get playerID
        private short getPlayerArrayIndex(NetConnection thisSender)
        {
            short id = -1;
            for (id = 0;  id < player_list.Length; id++)
            {
                if (player_list[id] != null)
                {
                    if (player_list[id].sender == thisSender)
                    {
                        //Logger.Header($"Theoretical returned ID should be: {id}");
                        //Logger.Header($"Returned ID will be: {id}");
                        return id;
                    }
                }
            }
            Logger.Failure("NO PLAYER WAS FOUND WITH THE GIVEN SENDER ADDRESS");
            return -1;
            //Logger.Header($"Returned ID will be: {id}");
            //return id;
        }
    }

}
