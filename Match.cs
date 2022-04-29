using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Lidgren.Network;
using SimpleJSON;

namespace WC.SARS
{
    class Match
    {
        Random randomizer = new Random();
        private NetPeerConfiguration config;
        public NetServer server;
        public Player[] player_list;
        private Weapon[] s_WeaponsList = Weapon.GetAllWeaponsList();
        private List<short> availableIDs;
        private Thread updateThread;

        private Dictionary<NetConnection, string> IncomingConnectionsList;
        private Dictionary<int, LootItem> ItemList;
        //private Dictionary<int, VAL> CoconutList;
        //private Dictionary<int, Vehicle>;

        private JSONArray s_PlayerDataJSON;
        private int s_TotalLootCounter;
        private int matchSeed1, matchSeed2, matchSeed3; //these are supposed to be random
        private int slpTime, prevTime, prevTimeA, matchTime;
        private bool matchStarted, matchFull;
        private bool isSorting, isSorted;
        public double timeUntilStart, gasAdvanceTimer, gasAdvanceLength;

        //mmmmmmmmmmmmmmmmmmmmmmmmmmmmm
        public bool DEBUG_ENABLED;
        public bool ANOYING_DEBUG1;
        private bool doWinCheck = false;
        private bool shouldUpdateAliveCount = true;

        public Match(int port, string ip, bool db, bool annoying)
        {
            s_TotalLootCounter = 0;
            slpTime = 10;
            matchStarted = false;
            matchFull = false;
            isSorting = false;
            isSorted = true;
            player_list = new Player[64];
            availableIDs = new List<short>(64);
            IncomingConnectionsList = new Dictionary<NetConnection, string>();
            for (short i = 0; i < 64; i++) { availableIDs.Add(i); }

            timeUntilStart = 90.00;
            gasAdvanceTimer = -1;
            prevTime = DateTime.Now.Second;
            updateThread = new Thread(serverUpdateThread);
            DEBUG_ENABLED = db;
            ANOYING_DEBUG1 = annoying;

            // Initializing JSON stuff
            if (File.Exists(Directory.GetCurrentDirectory() + @"\playerdata.json"))
            {
                JSONNode PlayerDataJSON = JSON.Parse(File.ReadAllText(Directory.GetCurrentDirectory() + @"\playerdata.json"));
                s_PlayerDataJSON = (JSONArray)PlayerDataJSON["PlayerData"];
            }
            else
            {
                Logger.Failure("No such file 'playerdata.json'");
                Environment.Exit(2);
            }
            // NetServer Initialization and starting
            config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.PingInterval = 22f;
            config.LocalAddress = System.Net.IPAddress.Parse(ip);
            config.Port = port;
            server = new NetServer(config); // todo make sure server actually starts before having to launch update thread? || believe that is done
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
                                    server.SendMessage(acceptMsgg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                    isSorted = false;
                                    break;
                                case NetConnectionStatus.Disconnected:
                                        Logger.Warn("Searching for player that disconnected.");
                                        Logger.Failure(msg.ReadString());
                                        //Logger.Warn($"{msg.SenderEndPoint} has disconnected...");
                                        short plr = getPlayerArrayIndex(msg.SenderConnection);
                                        if (plr != -1)
                                        {
                                            NetOutgoingMessage playerLeft = server.CreateMessage();
                                            playerLeft.Write((byte)46);
                                            playerLeft.Write(player_list[plr].myID);
                                            playerLeft.Write(false); //isAdminGhosting -- not quite sure how to determine this
                                            server.SendToAll(playerLeft, NetDeliveryMethod.ReliableOrdered);
                                            availableIDs.Insert(0, player_list[plr].myID);
                                            player_list[plr] = null;
                                            Logger.Success("Player Disconnected and dealt with successfully.");
                                            isSorted = false;
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
                            string clientKey = msg.ReadString();
                            if (clientKey == "flwoi51nawudkowmqqq")
                            {
                                if (!matchStarted)
                                {
                                    Logger.Success("Connection Allowed.");
                                    msg.SenderConnection.Approve();
                                }
                                else
                                {
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
                                Logger.Header("--> ConnectionLatencyUpdated:");
                                try
                                {
                                    float pingTime = msg.ReadFloat();
                                    Logger.Basic($"  -> Ping time: {pingTime}");
                                    Logger.Basic($"  -> Remote Time Offset: {msg.SenderConnection.RemoteTimeOffset}");
                                    Logger.Basic($"  -> Average Round Time: {msg.SenderConnection.AverageRoundtripTime}");
                                try
                                    {
                                        player_list[getPlayerArrayIndex(msg.SenderConnection)].lastPingTime = pingTime; //not actually ping whoopsies
                                }
                                    catch
                                    {
                                        Logger.Failure($"  -> Connection {msg.SenderEndPoint} does not exist. Cannot write their last ping time. (likely not yet fully connected)");
                                    }
                                }
                                catch
                                {
                                    Logger.Failure("ConnectionLatencyUpdated -- Error:: No float to read");
                                }
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
        //lots of important things go on in here
        #region server update thread
        private void serverUpdateThread() //where most things get updated...
        {
            Logger.Success("Server update thread started.");
            GenerateItemLootList(351301); //should be the matchSeed1 or something
            if (!doWinCheck)
            {
                Logger.Warn("\nWARNING -- doWinCheck is set to FALSE. The server will NOT check for a winner without intervention.");
                Logger.Warn("Use '/togglewin' to reactivate the check.\n");
            }

            //lobby
            while (!matchStarted)
            {
                if (!isSorted) { sortPlayersListNull(); }
                if (player_list[player_list.Length - 1] != null && !matchFull)
                {
                    matchFull = true;
                    Logger.Basic("Match seems to be full!");
                }
                send_dummy();

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
                if (!isSorted) { sortPlayersListNull(); }

                send_dummy();

                //check for win
                if (shouldUpdateAliveCount && doWinCheck) { update_checkAliveorWin(); }

                //updating player info to all people in the match
                updateEveryoneOnPlayerPositions();
                updateEveryoneOnPlayerInfo();

                updateServerDrinkCheck();
                //updateServerTapeCheck(); --similar idea, about as stupid.
                updateEveryonePingList();

                advanceTimeAndEventCheck();
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

            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    playerUpdate.Write(i);
                    break;
                }
            }

            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    playerUpdate.Write(player_list[i].myID);
                    playerUpdate.Write(player_list[i].mouseAngle);
                    playerUpdate.Write(player_list[i].position_X);
                    playerUpdate.Write(player_list[i].position_Y);
                }
                else { break; } //exits
            }
            server.SendToAll(playerUpdate, NetDeliveryMethod.ReliableSequenced);
        }
        private void updateEveryoneOnPlayerInfo()
        {
            /* The "correct" name for this is something like "Player HP/Armor/Move-Mode Change".
             * So, this should really only happen when one of those things are updated.
             * However, movemode should constantly be changing (super jump rolling for faster movement).
             * I think having it constantly check for this stuff is ok for right now.
             */

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45);
            for (int list_length = 0; list_length < player_list.Length; list_length++)
            {
                if (player_list[list_length] == null)
                {
                    msg.Write((byte)list_length);
                    for (int i = 0; i < list_length; i++)
                    {
                        if (player_list[i] != null)
                        {
                            msg.Write(player_list[i].myID);
                            msg.Write(player_list[i].hp);
                            msg.Write(player_list[i].armorTier);
                            msg.Write(player_list[i].armorTapes);
                            msg.Write(player_list[i].WalkMode);
                            msg.Write(player_list[i].drinkies);
                            msg.Write(player_list[i].tapies);
                        }
                        else { break; }
                    }
                    server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
                    break;
                }
            }
        }
        private void send_dummy()
        {
            NetOutgoingMessage dummy = server.CreateMessage();
            dummy.Write((byte)97);
            //dummy.Write("a dummy makes money off a dummy");
            server.SendToAll(dummy, NetDeliveryMethod.ReliableOrdered);
        }

        private void updateEveryonePingList()
        {
            NetOutgoingMessage pings = server.CreateMessage();
            pings.Write((byte)112);
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    pings.Write((byte)i);
                    for (int j = 0; j < i; j++)
                    {
                        pings.Write(player_list[j].myID);
                        pings.Write( (short)(player_list[j].lastPingTime*1000f)); //this appears to be correct.
                    }
                    break;
                }
            }
            server.SendToAll(pings, NetDeliveryMethod.ReliableOrdered);
        }

        private void updateServerDrinkCheck()
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    if (player_list[i].isDrinking)
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
                                    player_list[i].isDrinking = false;
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
                            player_list[i].isDrinking = false;
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

        private void update_checkAliveorWin()
        {
            shouldUpdateAliveCount = false;
            List<short> aIDs = new List<short>(player_list.Length);
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].isAlive)
                {
                    aIDs.Add(player_list[i].myID);
                }
            }
            if (aIDs.Count == 1)
            {
                NetOutgoingMessage congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon = server.CreateMessage();
                congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon.Write((byte)9);
                congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon.Write(aIDs[0]);
                server.SendToAll(congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon, NetDeliveryMethod.ReliableUnordered);
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
        //can be simplified with eventCheck
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
                if (prevTime != DateTime.Now.Second)
                {
                    gasAdvanceTimer = -1;
                    NetOutgoingMessage gasMoveMsg = server.CreateMessage();
                    gasMoveMsg.Write((byte)34);
                    //TODO: gasAdvanceLength should just be a float not a double silly billy!
                    gasMoveMsg.Write((float)gasAdvanceLength); //move time -- higher values allow the gas to move slower, lower values mean fast gas
                    server.SendToAll(gasMoveMsg, NetDeliveryMethod.ReliableOrdered);
                }
            }
        }
        //can be simplified with gasCheck
        private void advanceTimeAndEventCheck()
        {
            //basically a copy and paste from checkStartTimer(). Any inaccuracies/inefficency there also appear here.
            if (prevTimeA != DateTime.Now.Second)
            {
                matchTime += 1;
                prevTimeA = DateTime.Now.Second;

                switch (matchTime)
                {
                    case 60:
                        createNewSafezone(620, 720, 620, 720, 6000, 3000, 60);
                        gasAdvanceLength = 72;
                        break;
                    case 212:

                        break;
                    default:
                        Logger.DebugServer("Nothing to do...");
                        break;
                }
            }
        }

        private void createNewSafezone(float x1, float y1, float x2, float y2, float r1, float r2, float time)
        {
            NetOutgoingMessage gasMsg = server.CreateMessage();
            gasMsg.Write((byte)33);
            gasMsg.Write(x1); //starting circle centerX
            gasMsg.Write(y1); //starting circle centerY
            gasMsg.Write(x2); //end circle centerX
            gasMsg.Write(y2); //end circle centerY
            gasMsg.Write(r1); //radius of circle1
            gasMsg.Write(r2); //radius of circle2
            gasMsg.Write(time); //time until it shall advance
            server.SendToAll(gasMsg, NetDeliveryMethod.ReliableOrdered);
            gasAdvanceTimer = time;
        }

        private void checkForWinnerWinnerChickenDinner()
        {
            Logger.Failure("You shouldn't have done that.");
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
            server.SendToAll(startMsg, NetDeliveryMethod.ReliableOrdered);
        }
        #endregion

        // only reason this exists is just so the part of the server code where it checks for connections and stuff isn't clutted too much
        // until it is pretty close to being fine-enough. it caused a big mess.
        private void HandleMessage(NetIncomingMessage msg)
        {
            byte b = msg.ReadByte();
            //if (b != 14) {
                //Logger.DebugServer(b.ToString());
            //}

            switch (b)
            {
                // Request Authentication
                case 1:
                    Logger.Header($"Authentication Requestion\nSender: {msg.SenderEndPoint}\n");
                    sendAuthenticationResponseToClient(msg);
                    break;

                case 3: // still has work to be done
                    Logger.Header($"Sender {msg.SenderEndPoint}'s Ready Received. Now reading character data.");
                    serverHandlePlayerConnection(msg);
                    break;

                case 5:
                    Logger.Header($"<< sending {msg.SenderEndPoint} player characters... >>");
                    sendPlayerCharacters();
                    break;
                case 7:
                    Player ejectedPlayer = player_list[getPlayerArrayIndex(msg.SenderConnection)];
                    NetOutgoingMessage sendEject = server.CreateMessage();
                    sendEject.Write((byte)8);
                    sendEject.Write(ejectedPlayer.myID);
                    sendEject.Write(ejectedPlayer.position_X);
                    sendEject.Write(ejectedPlayer.position_Y);
                    sendEject.Write(true); //isParachute
                    server.SendToAll(sendEject, NetDeliveryMethod.ReliableSequenced);
                    Logger.Warn($"Player ID {ejectedPlayer.myID} ({ejectedPlayer.myName}) has ejected!\nX, Y: ({ejectedPlayer.position_X}, {ejectedPlayer.position_Y})");
                    break;

                case 14:
                    //this isn't accurate entirely. everything aside from mAngle is normal I think?
                    float mAngle = msg.ReadFloat();
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
                                player_list[i].WalkMode = currentwalkMode;
                                break;
                            }
                        }
                        else { break;}
                    }
                    if (ANOYING_DEBUG1)
                    {
                        Logger.Warn($"Mouse Angle: {mAngle}");
                        Logger.Warn($"playerX: {actX}");
                        Logger.Warn($"playerY: {actY}");
                        Logger.Basic($"player WalkMode: {currentwalkMode}");
                    }
                    break;
                case 16: //no clue how true to the actual this is
                    short weaponID = msg.ReadInt16(); //short -- WeaponId
                    byte slotIndex = msg.ReadByte();//byte -- slotIndex
                    float aimAngle = (msg.ReadFloat() / 57.295776f); //no clue if dividing actually gets to the correct angle or not (found in game)
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

                    int indexID = getPlayerArrayIndex(msg.SenderConnection);
                    NetOutgoingMessage plrShot = server.CreateMessage();
                    plrShot.Write((byte)17);
                    plrShot.Write(player_list[indexID].myID); //ID of the player who made the shot
                    plrShot.Write((ushort)(player_list[indexID].lastPingTime * 1000f)); //before it was stated "I don't give a darn!" because ping would always be set to 1. now it's just confuzing as to whether this truly works
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
                            plrShot.Write(msg.ReadFloat() / 57.295776f);
                            plrShot.Write(msg.ReadInt16());
                            plrShot.Write(msg.ReadBoolean());
                        }
                    }
                    server.SendToAll(plrShot, NetDeliveryMethod.ReliableSequenced);
                    break;
                case 18:
                    serverSendPlayerShoteded(msg);
                    break;
                case 21: //TODO -- cleanup
                    Logger.Header("Player Looted Item");
                    try
                    {
                        Player m_CurrentPlayer = player_list[getPlayerArrayIndex(msg.SenderConnection)];
                        NetOutgoingMessage __ExtraLoot = null; // Extra loot message to send after telling everyone about loot and junk
                        short m_LootID = (short)msg.ReadInt32();
                        byte m_PlayerSlot = msg.ReadByte();
                        LootItem m_LootToGive = ItemList[m_LootID];

                        switch (m_LootToGive.LootType)
                        {
                            case LootType.Weapon: // Stupidity
                                Logger.Basic($" -> Player found a weapon.\n{m_LootToGive.LootName}");
                                if (m_PlayerSlot == 1 || m_PlayerSlot == 0) // Weapon 1 or 2 | Not Melee
                                {
                                    m_CurrentPlayer.MyLootItems[m_PlayerSlot] = m_LootToGive;
                                    Logger.Failure("  -> WARNING NOT YET FULLY HANDLED");
                                    break;
                                }
                                if (m_PlayerSlot != 3) // This is the throwable slot. Slot_2 is melee. 
                                {
                                    Logger.Failure("  -> Player has found a weapon. However, none of the slot it claims to be accessing are valid here.");
                                    break;
                                }

                                // Throwable / Slot_3 | PlayerSlot 4 | so... m_PlayerSlot-1 = 2 >> right array index
                                if (m_CurrentPlayer.MyLootItems[2].WeaponType != WeaponType.NotWeapon) // Player has throwable here already
                                {
                                    // uuuh how do we figure this out ?????
                                    Logger.DebugServer($"Throwable LootName Test: Plr: {m_CurrentPlayer.MyLootItems[2].LootName}\nThis new loot: {m_LootToGive.LootName}");
                                    if (m_CurrentPlayer.MyLootItems[2].LootName == m_LootToGive.LootName) // Has this throwable-type already
                                    {
                                        m_CurrentPlayer.MyLootItems[2].GunAmmo += m_LootToGive.GunAmmo;
                                        Logger.Basic($"{m_CurrentPlayer.MyLootItems[2].LootName} - Amount: {m_CurrentPlayer.MyLootItems[2].GunAmmo}");
                                        break;
                                    }
                                    // Else = Player has a throwable, BUT it is a different type so we need to re-spawn the old one
                                    s_TotalLootCounter++;
                                    NetOutgoingMessage newLoot = server.CreateMessage();
                                    newLoot.Write((byte)20);
                                    newLoot.Write(s_TotalLootCounter);
                                    newLoot.Write((byte)LootType.Weapon);
                                    Logger.Warn(m_CurrentPlayer.MyLootItems[2].IndexInList.ToString());
                                    Logger.Warn(s_WeaponsList[m_CurrentPlayer.MyLootItems[2].IndexInList].JSONIndex.ToString());
                                    Logger.Warn(m_CurrentPlayer.MyLootItems[2].GunAmmo.ToString());
                                    newLoot.Write((short)m_CurrentPlayer.MyLootItems[2].IndexInList);
                                    newLoot.Write(m_CurrentPlayer.position_X);
                                    newLoot.Write(m_CurrentPlayer.position_Y);
                                    newLoot.Write(m_CurrentPlayer.position_X);
                                    newLoot.Write(m_CurrentPlayer.position_Y);
                                    newLoot.Write(m_CurrentPlayer.MyLootItems[2].GunAmmo);
                                    newLoot.Write(m_CurrentPlayer.MyLootItems[2].GunAmmo.ToString());
                                    server.SendToAll(newLoot, NetDeliveryMethod.ReliableOrdered);
                                    ItemList.Add(s_TotalLootCounter, new LootItem(s_TotalLootCounter, LootType.Weapon, WeaponType.Throwable, m_CurrentPlayer.MyLootItems[2].LootName, 0, (byte)m_CurrentPlayer.MyLootItems[2].IndexInList, m_CurrentPlayer.MyLootItems[2].GunAmmo));

                                    //clip ammount = ammount to spawn

                                    m_CurrentPlayer.MyLootItems[2] = m_LootToGive;
                                    break;
                                }
                                // Player doesn't have a throwable
                                m_CurrentPlayer.MyLootItems[2] = m_LootToGive;
                                    // throwables = slot 3
                                   /* switch (m_PlayerSlot)
                                {
                                    case 0:
                                        Logger.Warn(" -> Recieved slot = 0");
                                        break;
                                    case 1:
                                        Logger.Warn(" -> Recieved slot = 1");
                                        break;
                                    case 2:
                                        Logger.Warn(" -> Recieved slot = 2");
                                        break;
                                    default:
                                        Logger.Warn($" -> Yeah unhandled slot. {m_PlayerSlot}");
                                        break;
                                }*/
                                break;
                            case LootType.Juices:
                                Logger.Basic($" -> Player found some drinkies. +{m_LootToGive.GiveAmount}");

                                //Logger.DebugServer($"difference: {(int)m_CurrentPlayer.drinkies + m_LootToGive.GiveAmount}");
                                // TODO -- Find out if this number can overroll. because they're bytes and junk and like... they get turned into ints but like... can they roll over before being added?
                                if (m_CurrentPlayer.drinkies + m_LootToGive.GiveAmount > 200)
                                {
                                    m_LootToGive.GiveAmount -= (byte)(200 - m_CurrentPlayer.drinkies);
                                    m_CurrentPlayer.drinkies += (byte)(200 - m_CurrentPlayer.drinkies);
                                    __ExtraLoot = MakeNewDrinkLootItem(m_LootToGive.GiveAmount, new float[] { m_CurrentPlayer.position_X, m_CurrentPlayer.position_Y, m_CurrentPlayer.position_X, m_CurrentPlayer.position_Y });
                                    /*NetOutgoingMessage newLoot = server.CreateMessage();
                                    s_TotalLootCounter++;
                                    newLoot.Write((byte)20);
                                    newLoot.Write(s_TotalLootCounter);
                                    newLoot.Write((byte)LootType.Juices);
                                    newLoot.Write((short)m_LootToGive.GiveAmount);
                                    newLoot.Write(m_CurrentPlayer.position_X);
                                    newLoot.Write(m_CurrentPlayer.position_Y);
                                    newLoot.Write(m_CurrentPlayer.position_X);
                                    newLoot.Write(m_CurrentPlayer.position_Y);
                                    newLoot.Write((byte)0);
                                    server.SendToAll(newLoot, NetDeliveryMethod.ReliableOrdered);
                                    ItemList.Add(s_TotalLootCounter, new LootItem(s_TotalLootCounter, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{m_LootToGive.GiveAmount}", 0, m_LootToGive.GiveAmount));
                                    //Logger.DebugServer($"New Give: {m_LootToGive.GiveAmount}\nDrinkies: {m_CurrentPlayer.drinkies}");
                                    */
                                    break;
                                }
                                m_CurrentPlayer.drinkies += m_LootToGive.GiveAmount;
                                break;
                            case LootType.Tape:
                                Logger.Basic(" -> Player found some tape.");
                                if (m_CurrentPlayer.tapies < 5)
                                {
                                    m_CurrentPlayer.tapies += m_LootToGive.GiveAmount; // Yes, generated tapes give one, but what about looted tapes?
                                }
                                break;
                            case LootType.Armor:
                                Logger.Basic($" -> Player got some armor. Tier{m_LootToGive.ItemRarity}:{m_LootToGive.GiveAmount}");
                                if (m_CurrentPlayer.armorTier != 0)
                                {
                                    Logger.DebugServer(" -> Already have armor- spawn old one and give new");
                                    //go spawn the old one
                                    NetOutgoingMessage newLoot = server.CreateMessage();
                                    s_TotalLootCounter++;
                                    newLoot.Write((byte)20);
                                    newLoot.Write(s_TotalLootCounter);
                                    newLoot.Write((byte)LootType.Armor);
                                    newLoot.Write((short)m_CurrentPlayer.armorTapes); // Armor's Tape Amount
                                    newLoot.Write(m_CurrentPlayer.position_X);
                                    newLoot.Write(m_CurrentPlayer.position_Y);
                                    newLoot.Write(m_CurrentPlayer.position_X);
                                    newLoot.Write(m_CurrentPlayer.position_Y);
                                    newLoot.Write(m_CurrentPlayer.armorTier); // The actual tier of armor the game will display and stuff
                                    server.SendToAll(newLoot, NetDeliveryMethod.ReliableOrdered);
                                    ItemList.Add(s_TotalLootCounter, new LootItem(s_TotalLootCounter, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{m_CurrentPlayer.armorTier}", m_CurrentPlayer.armorTier, m_CurrentPlayer.armorTapes));
                                    
                                    m_CurrentPlayer.armorTier = m_LootToGive.ItemRarity;
                                    m_CurrentPlayer.armorTapes = m_LootToGive.GiveAmount;
                                    break;
                                }
                                Logger.DebugServer(" -> Player does not already have armor. Giving them armor");
                                m_CurrentPlayer.armorTier = m_LootToGive.ItemRarity;
                                m_CurrentPlayer.armorTapes = m_LootToGive.GiveAmount;
                                break;

                            // TODO - make server track how much ammo the player has. even though we won't use it
                            case LootType.Ammo:
                                Logger.Basic($" -> Ammo Loot: AmmoType:Ammount -- {m_LootToGive.ItemRarity}:{m_LootToGive.GiveAmount}");
                                break;
                        }
                        NetOutgoingMessage testMessage = server.CreateMessage();
                        testMessage.Write((byte)22); // Header / Packet ID
                        testMessage.Write(m_CurrentPlayer.myID); // Player ID
                        testMessage.Write((int)m_LootID); // Loot Item ID
                        testMessage.Write(m_PlayerSlot); // Player Slot to update
                        if (!matchStarted) // Write Forced Rarity
                        {
                            testMessage.Write((byte)4); // Only matters in a lobby
                        }
                        testMessage.Write((byte)0);
                        server.SendToAll(testMessage, NetDeliveryMethod.ReliableSequenced);
                        if (__ExtraLoot != null)
                        {
                            server.SendToAll(__ExtraLoot, NetDeliveryMethod.ReliableSequenced);
                        }
                    } catch (Exception Except)
                    {
                        Logger.Failure(Except.ToString());
                    }


                    //for testing -- remove when done
                    /*try{
                        LootItem FoundLoot = ItemList[item];
                        Logger.Header($"Client LootID = {item}.\nLooked up loot: {FoundLoot.LootID}");
                        Logger.Basic($" -> Loot Item Loot Type: {FoundLoot.LootType}\n");
                        switch (FoundLoot.LootType)
                        {
                            case LootType.Weapon:
                                Logger.Header($"Found a weapon loot item.\nName:{FoundLoot.LootName}");
                                Logger.Basic($" -> TrueType: {FoundLoot.LootType}");
                                Logger.Basic($" -> WeaponType: {FoundLoot.WeaponType}");
                                Logger.Basic($" -> Weapon Rarity: {FoundLoot.ItemRarity}");
                                Logger.Basic($" -> Loot Give Amount: {FoundLoot.GiveAmount}");
                                break;
                            case LootType.Juices:
                                Logger.Header($"Found a Juice Loot Item\nName:{FoundLoot.LootName}");
                                Logger.Basic($" -> TrueType: {FoundLoot.LootType}");
                                Logger.Basic($" -> WeaponType: {FoundLoot.WeaponType}");
                                Logger.Basic($" -> Loot Give Amount: {FoundLoot.GiveAmount}");
                                break;
                            case LootType.Armor:
                                Logger.Header($"Found a Juice Loot Item\nName:{FoundLoot.LootName}");
                                Logger.Basic($" -> TrueType: {FoundLoot.LootType}");
                                Logger.Basic($" -> WeaponType: {FoundLoot.WeaponType}");
                                Logger.Basic($" -> Armor Tier: {FoundLoot.ItemRarity}");
                                Logger.Basic($" -> Loot Give Amount: {FoundLoot.GiveAmount}");
                                break;
                            case LootType.Tape:
                                Logger.Header($"Found a Tape Loot Item\nName:{FoundLoot.LootName}");
                                Logger.Basic($" -> TrueType: {FoundLoot.LootType}");
                                Logger.Basic($" -> WeaponType: {FoundLoot.WeaponType}");
                                Logger.Basic($" -> Loot Give Amount: {FoundLoot.GiveAmount}");
                                break;
                            case LootType.Ammo:
                                Logger.Header($"Found an Ammo Loot Item\nName:{FoundLoot.LootName}");
                                Logger.Basic($" -> TrueType: {FoundLoot.LootType}");
                                Logger.Basic($" -> WeaponType: {FoundLoot.WeaponType}");
                                Logger.Basic($" -> AmmoType: {FoundLoot.ItemRarity}");
                                Logger.Basic($" -> Base-Ammo Give Amount: {FoundLoot.GiveAmount}");
                                break;
                        }
                    }
                    catch (Exception ThisException)
                    {
                        Logger.Failure("ClientSentLootItem -- Error while trying to lookup server's version of client's claimed loot.");
                        Logger.Failure($"Exception:: {ThisException}\n");
                    }*/
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
                //client - Send Spectating Player ?
                case 44:
                    /* msg.ReadFloat() // cam X
                     * msg.ReadFloat() // cam Y
                     * msg.ReadInt16() // player id (who they're watching)
                     */
                    //
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

                    //Tell everyone who emoted
                    Player emotingPlayer = player_list[getPlayerArrayIndex(msg.SenderConnection)]; //only need to get player once.
                    //short ePlayerID = getPlayerID(msg.SenderConnection); -- old
                    //int ePlayerIndex = getPlayerArrayIndex(msg.SenderConnection); -- old
                    NetOutgoingMessage emoteMsg = server.CreateMessage();
                    emoteMsg.Write((byte)67); //Header
                    emoteMsg.Write(emotingPlayer.myID);
                    //emoteMsg.Write(player_list[ePlayerIndex].myID); //obviously ID -- old
                    emoteMsg.Write(msg.ReadInt16()); //Read emote id to then send to everyone!
                    server.SendToAll(emoteMsg, NetDeliveryMethod.ReliableUnordered);

                    //update player info rq
                    emotingPlayer.position_X = msg.ReadFloat();
                    emotingPlayer.position_Y = msg.ReadFloat();
                    //player_list[ePlayerIndex].position_X = msg.ReadFloat(); -- old
                    //player_list[ePlayerIndex].position_Y = msg.ReadFloat(); -- old
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

                    server.SendToAll(descBroke, NetDeliveryMethod.ReliableOrdered);

                    /* goes to: GameServerSentDoodadDestroyed
                     * GameServerSentDoodadDestroyed(desctructX, destructY,
                     * L[IntPoints]:Points to Change, CollisionType : collisionTOChangeTO,
                     * List[short] : damaged player IDs)
                     * 
                     */

                    break;

                case 74: //attack windup. appears to only be used when starting to fire the minigun. no where else.
                    Logger.testmsg("\nAttack Windup used.\nNote if you see this message/find out when.");
                    serverSendAttackWindUp(msg);
                    break;

                case 75: //attack wind down -- clearly is used when someone revs-down a minigun. unknown if used elsewhere.
                    Logger.testmsg("\nAttack Wind Downed used.\nNote if you see this message/find out when.");
                    serverSendAttackWindDown(getPlayerID(msg.SenderConnection), msg.ReadInt16());
                    break;
                case 87:
                        serverSendDepployedTrap(msg);
                    break;
                case 90: //reload weapon
                    NetOutgoingMessage plrCancelReloadMsg = server.CreateMessage();
                    plrCancelReloadMsg.Write((byte)91);
                    plrCancelReloadMsg.Write(getPlayerID(msg.SenderConnection));
                    server.SendToAll(plrCancelReloadMsg, NetDeliveryMethod.ReliableSequenced);
                    break;

                case 97: //client periodically sends it. if it never gets anything back like ever, then it starts thinking it is lagging.
                    NetOutgoingMessage dummyMsg = server.CreateMessage();
                    dummyMsg.Write((byte)97);
                    server.SendMessage(dummyMsg, msg.SenderConnection, NetDeliveryMethod.Unreliable);
                    break;
                //clientSendDucttaping
                case 98:
                    serverSendPlayerStartedTaping(msg.SenderConnection, msg.ReadFloat(), msg.ReadFloat());
                    break;

                default:
                    Logger.missingHandle($"Message appears to be missing handle. ID: {b}");
                    break;

            }
        }
        //message 1 -> send 2
        /// <summary>
        /// Sends Authentication Response to a connected Client
        /// </summary>
        private void sendAuthenticationResponseToClient(NetIncomingMessage msg)
        {
            //TODO -- Actually try approving the connection.
            // string  --   PlayFabID
            // bool    --   FillYayorNay
            // string  --   Auth Ticket
            // byte    --   Party Count
            // string --    PartyMember[s] PlayFabID
            string _PlayFabID = msg.ReadString();
            /* bool _FillsChoice = msg.ReadBoolean();
            string _AuthenticationTicket = msg.ReadString();
            byte _PartyCount = msg.ReadByte();
            string[] _PartyIDs; */
            Logger.DebugServer("This user's PlayFabID: " + _PlayFabID);
            if (!IncomingConnectionsList.ContainsKey(msg.SenderConnection))
            {
                IncomingConnectionsList.Add(msg.SenderConnection, _PlayFabID);
            }
            NetOutgoingMessage acceptMsg = server.CreateMessage();
            acceptMsg.Write((byte)2);
            acceptMsg.Write(true); //todo - maaaybe actually try authenticating the player?
            server.SendMessage(acceptMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
            Logger.Success($"Server sent {msg.SenderConnection.RemoteEndPoint} their accept message!");
        }

        private void serverHandlePlayerConnection(NetIncomingMessage msg)
        {
            //Read the player's character info and stuff
            ulong steamID = msg.ReadUInt64();     //not in base
            string steamName = msg.ReadString();  //not in base -- must be added in
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
            if (!isSorted && !isSorting) { sortPlayersListNull(); } //make sure to sort if needed
            //TODO: I think there is a better way of finding the ID that is available and stuff but not sure how
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                { 
                    player_list[i] = new Player(availableIDs[0], charID, umbrellaID, graveID, deathEffectID, emoteIDs, hatID, glassesID, beardID, clothesID, meleeID, gunSkinCount, gunskinGunID, gunSkinIndex, steamName, msg.SenderConnection);
                    sendClientMatchInfo2Connect(availableIDs[0], msg.SenderConnection);
                    availableIDs.RemoveAt(0);
                    //find if person who connected is mod, admin, or whatever!
                    try
                    {
                        string _ThisPlayFabID = IncomingConnectionsList[msg.SenderConnection];
                        JSONObject _PlayerJSONData;
                        for (int p = 0; p < s_PlayerDataJSON.Count; p++)
                        {
                            if (s_PlayerDataJSON[p] != null && s_PlayerDataJSON[p]["PlayerID"] == _ThisPlayFabID)
                            {
                                _PlayerJSONData = (JSONObject)s_PlayerDataJSON[p];
                                Logger.Basic(_PlayerJSONData.ToString());
                                // not only does it check if the key exists, but also tests for bool soooo
                                if (_PlayerJSONData["Admin"])
                                {
                                    player_list[i].isDev = true;
                                }
                                if (_PlayerJSONData["Moderator"])
                                {
                                    player_list[i].isMod = true;
                                }
                                if (_PlayerJSONData["Founder"])
                                {
                                    player_list[i].isFounder = true;
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception exceptlol)
                    {
                        Logger.Failure("absolute blunder doing... IDK!\n" + exceptlol);
                    }
                    break;
                }
            }
        }

        private void sendClientMatchInfo2Connect(short sendingID, NetConnection receiver)
        {
            // send a message back to the connecting player... \\
            NetOutgoingMessage mMsg = server.CreateMessage();
            mMsg.Write((byte)4);
            mMsg.Write(sendingID); // Assigned Player ID

            // todo: *must* make match seeds random. could just use system.Random() but didn't wanna bother yet
            mMsg.Write(351301);  // int32 -- seed 1 (GameServer: Sent LootGen Seed)
            mMsg.Write(5328522); // int32 -- seed 2 (GameServer: Sent CoconutGen Seed)
            mMsg.Write(9037281); // int32 -- seed 2 (GameServer: Sent VehicleGen Seed)

            mMsg.Write(timeUntilStart); // double -- clientTimeAtWhichGameWillStart
            mMsg.Write("yerhAGJ");      // string -- Match UUID ||  [ MatchUUID ]
            mMsg.Write("solo");         // string -- gamemode ||  [ gameMode ]

            //todo: *must find a way to "randomize" flight path (I say "random" because it seems as though paths aren't entirely random...)
            mMsg.Write((float)0);       // float -- x1 - flightStartPoint
            mMsg.Write((float)0);       // float -- y1 
            mMsg.Write((float)8000);    // float -- x2 -- flightEndPoint
            mMsg.Write((float)8000);    // float -- y2

            /*this last part is the server telling the game info about where the gallery targets are and stuff
             * if nothing is included (like it is here) the targets just don't appear. which is fine by me.
             * whether or not the targets are working is not really game-changing so not dealing with that headache right now. */
            mMsg.Write((byte)0); // amount of times to loop through thing ig but I skipped out. something with gallery targets
            mMsg.Write((byte)0); // that gallery target's score or whatever but I don't give a darn

            server.SendMessage(mMsg, receiver, NetDeliveryMethod.ReliableOrdered);
            //msg.SenderConnection.Disconnect("Currently Testing Stuff! Please come back later!");
        }

        //message 5 > send10
        /// <summary>
        /// Sends a message to everyone in the lobby with data about everyone in the lobby. Likely only called once someone connects so everyone knows of the new player.
        /// </summary>
        private void sendPlayerCharacters()
        {
            Logger.Header($"Beginning to send player characters to everyone once again.");
            if (!isSorted) // make sure list is sorted still... but it probably is so... yeah
            {
                sortPlayersListNull();
            }
            NetOutgoingMessage sendPlayerPosition = server.CreateMessage();
            sendPlayerPosition.Write((byte)10);
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    sendPlayerPosition.Write((byte)i); // Ammount of times to loop (for amount of players, you know?
                    break;
                }
            }
            // Theoretically, I think it would be faster to just use the previously gotten list-length-to-null, and loop that many times then just stop
            // as opposed to just doing it over. However, in the previous tests the results said otherwise so... okii!
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null) 
                {
                    sendPlayerPosition.Write((short)i);                                 // MyAssignedID          |   Short
                    sendPlayerPosition.Write(player_list[i].charID);                    // CharacterID           |   Short
                    sendPlayerPosition.Write(player_list[i].umbrellaID);                // UmbrellaID            |   Short
                    sendPlayerPosition.Write(player_list[i].gravestoneID);              // GravestoneID          |   Short
                    sendPlayerPosition.Write(player_list[i].deathEffectID);             // ExplosionID           |   Short
                    for (int j = 0; j < player_list[i].emoteIDs.Length; j++)            // EmoteIDs              |   Short[6]
                    {
                        sendPlayerPosition.Write(player_list[i].emoteIDs[j]);
                    }
                    sendPlayerPosition.Write(player_list[i].hatID);                     // HatID                 |   Short
                    sendPlayerPosition.Write(player_list[i].glassesID);                 // GlassesID             |   Short
                    sendPlayerPosition.Write(player_list[i].beardID);                   // BeardID               |   Short
                    sendPlayerPosition.Write(player_list[i].clothesID);                 // ClothesID             |   Short
                    sendPlayerPosition.Write(player_list[i].meleeID);                   // MeleeID               |   Short
                    //Gun skins
                    sendPlayerPosition.Write(player_list[i].gunSkinCount);              // Amount of GunSkins    |   Byte
                    for (byte l = 0; l < player_list[i].gunSkinCount; l++) 
                    {
                        sendPlayerPosition.Write(player_list[i].gunskinKey[l]);         // GunSkin GunID         |   Short in Short[]
                        sendPlayerPosition.Write(player_list[i].gunskinValue[l]);       // GunSkin SkinID        |   Byte in Byte[]
                    }

                    //Positioni?
                    sendPlayerPosition.Write(player_list[i].position_X);                // PositionX             |   Float
                    sendPlayerPosition.Write(player_list[i].position_Y);                // PositionY             |   Float
                    sendPlayerPosition.Write(player_list[i].myName);                    // PlayerName            |   String

                    sendPlayerPosition.Write(player_list[i].currenteEmote);             // CurrentEmoteID        |   Short
                    sendPlayerPosition.Write((short)player_list[i].MyLootItems[0].LootID);     // Equip 1 ID            |   Short
                    sendPlayerPosition.Write((short)player_list[i].MyLootItems[1].LootID);     // Equip 2 ID            |   Short
                    sendPlayerPosition.Write(player_list[i].MyLootItems[0].ItemRarity); // Equip 1 Rarity        |   Byte
                    sendPlayerPosition.Write(player_list[i].MyLootItems[1].ItemRarity); // Equip 2 Rarity        |   Byte
                    sendPlayerPosition.Write(player_list[i].ActiveSlot);                // Current Equip Index   |   Byte
                    sendPlayerPosition.Write(player_list[i].isDev);                     // Is User Developer     |   Bool
                    sendPlayerPosition.Write(player_list[i].isMod);                     // Is User Moderator     |   Bool
                    sendPlayerPosition.Write(player_list[i].isFounder);                 // Is User Founder       |   Bool
                    sendPlayerPosition.Write((short)450);                               // Player Level          |   Short
                    sendPlayerPosition.Write((byte)0);                                  // Amount of Teammates   |   Byte
                    //sendPlayerPosition.Write((short)25);                              // Teammate ID           |   Short

                }
                else { break; }
            }
            Logger.Success("Going to be sending new player all other player positions.");
            server.SendToAll(sendPlayerPosition, NetDeliveryMethod.ReliableSequenced);
        }


        //18 > 19
        private void serverSendPlayerShoteded(NetIncomingMessage message)
        {
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

            // remove when done
            // testing stuff

            Player Attacker = player_list[getPlayerArrayIndex(message.SenderConnection)];
            Logger.Warn($"Attacker LootID: {Attacker.MyLootItems[Attacker.curEquipIndex].LootID}\nAttacker Name: {Attacker.MyLootItems[Attacker.curEquipIndex].LootName}\nAttacking Weapon ID: {wepID}");

            //

            /* there needs to be more code and junk to find out how much a person should actually be damaged for.
             * like client for the most part just sends weaponIDs / vehicleIDs, expecting the server to know which is which
             * however, this server does not know which is which. figure that out later I guess.
             */
            try
            {
                Player shotPlayer = null;
                for (int i = 0; i < player_list.Length; i++)
                {
                    if (player_list != null && player_list[i].myID == hitPlayerID)
                    {
                        shotPlayer = player_list[i];
                        Logger.Success($"Located player successfully. ID: {shotPlayer.myID} ({shotPlayer.myName})");
                        //calculate the distance between the player that was shot, and where it actually hit.
                        // (SPOILER) this is not a good way of figuring out whether this is a valid shot/to damage
                        double num = Math.Sqrt(Math.Pow((hitX - shotPlayer.position_X), 2) + Math.Pow((hitY - shotPlayer.position_Y), 2));
                        Logger.Warn($"Calculated distance: {num}");
                        if (num <= 20)
                        {
                            Logger.Warn($"Shot Player HP: {shotPlayer.hp}");
                            shotPlayer.hp -= 10;

                            //todo: fix -- edited this 3/17/22
                            Logger.Warn($"Shot Player NEW HP: {shotPlayer.hp}");
                            if (shotPlayer.hp == 0)
                            {
                                NetOutgoingMessage kill = server.CreateMessage();
                                kill.Write((byte)15);               // Message Type 15

                                kill.Write(shotPlayer.myID);        //Dying player ID
                                kill.Write(shotPlayer.position_X);  //Dying Player X
                                kill.Write(shotPlayer.position_Y);  //Dying Player Y

                                kill.Write((short)-3);              //Killer's Player ID
                                kill.Write((short)-1);              //Killer's Weapon ID

                                server.SendToAll(kill, NetDeliveryMethod.ReliableSequenced);
                                shotPlayer.isAlive = false;
                                shouldUpdateAliveCount = true;
                            }
                        }
                        break;
                    }
                }
                Logger.Failure("Did not find... the player that was hit??");
            }
            catch (Exception exc)
            {
                Logger.Failure(exc.ToString());
            }
        }

        /// <summary>
        /// Handles a chat message the server has received.
        /// </summary>
        /// <param name="message"></param>
        private void serverHandleChatMessage(NetIncomingMessage message)
        {
            Logger.Header("Chat message. Wonderful!");
            //this is terrible. we are aware. have fun.
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
                                id2 = short.Parse(command[2]);


                                NetOutgoingMessage forcetoPos = server.CreateMessage();
                                forcetoPos.Write((byte)8); forcetoPos.Write(id);
                                forcetoPos.Write(player_list[id2].position_X);
                                forcetoPos.Write(player_list[id2].position_Y);
                                forcetoPos.Write(false);
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
                                responseMsg = $"The game will begin in {timeUntilStart} seconds.";
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
                                gCircCmdMsg.Write((byte)33);
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
                                cParaMsg.Write(getPlayerID(message.SenderConnection));
                                //cParaMsg.Write((short)0);
                                cParaMsg.Write(isDive);
                                server.SendToAll(cParaMsg, NetDeliveryMethod.ReliableOrdered);
                                responseMsg += $"Parachute Mode Changed. Dive: {isDive}";
                            }
                            else
                            {
                                responseMsg = $"Provided value '{command[1]}' is not a true/false value. Please try again.";
                            }
                        }
                        else
                        {
                            responseMsg = $"Insufficient amount of arguments provided. This command takes 1. Given: {command.Length - 1}.";
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
                            if (short.TryParse(command[1], out forceID))
                            {
                                if (!(forceID < 0) && !(forceID > 64))
                                {
                                    for (int fl = 0; fl < player_list.Length; fl++)
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
                    case "/sendjunk":
                        Logger.Success("user used... sending JUNK?!!");
                        NetOutgoingMessage LOL = server.CreateMessage();
                        LOL.Write((byte)97);
                        LOL.Write("Hello, this is a string!");
                        LOL.Write(245f);
                        LOL.Write("There was a float somewhere there (2)...");
                        LOL.Write((short)5);
                        LOL.Write("There was a short somewhere there (5)...");
                        LOL.Write(true);
                        LOL.Write("There was a bool somewhere there (true)...");
                        LOL.Write("But that's all for now. Have fun with this RELIABLE ORDERED message!");
                        server.SendToAll(LOL, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = "All good. Have fun lol";
                        break;
                    case "/pray":
                        NetOutgoingMessage banan = server.CreateMessage();
                        banan.Write((byte)110);
                        banan.Write(getPlayerID(message.SenderConnection)); //player
                        banan.Write(0f);//interval
                        banan.Write((byte)255);
                        int count = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            for (int j = 0; j < 32; j++)
                            {
                                count++;
                                if (count != 256)
                                {
                                    banan.Write((float)(3683f + j)); //x 
                                    banan.Write((float)(3549f - i)); //y
                                }
                            }
                        }
                        Console.WriteLine(count);
                        banan.Write((byte)1);
                        banan.Write(getPlayerID(message.SenderConnection));

                        server.SendToAll(banan, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = "Praise Banan.";
                        break;
                    case "/kill":
                        //just wanted to test. this is very very broken just like the rest of the """"commands"""".
                        if (command.Length > 1)
                        {
                            if (tryFindPlayerIDbyName(command[1], out short sPlayerID) || short.TryParse(command[1], out sPlayerID))
                            {
                                if (tryFindArrayIndexByID(sPlayerID, out int index))
                                {
                                    Player killPlayer = player_list[index]; //is this worth it?
                                    NetOutgoingMessage kill = server.CreateMessage();
                                    kill.Write((byte)15);               // Message Type 15

                                    kill.Write(killPlayer.myID);        //Dying player ID
                                    kill.Write(killPlayer.position_X);  //Dying Player X
                                    kill.Write(killPlayer.position_Y);  //Dying Player Y

                                    kill.Write((short)-3);              //Killer's Player ID
                                    kill.Write((short)-1);              //Killer's Weapon ID

                                    server.SendToAll(kill, NetDeliveryMethod.ReliableSequenced);
                                    responseMsg = $"Killed Player {sPlayerID} ({killPlayer.myName}).";
                                    Logger.Basic($"Killed player {sPlayerID} ({killPlayer.myName}).");
                                }
                                else
                                { responseMsg = $"Could not locate player '{command[1]}'."; }
                            }
                            else { responseMsg = $"Could not locate player '{command[1]}'."; }
                        }
                        else { responseMsg = "Not enough arguments provided."; }
                        break;

                    case "/removeweapons":
                        NetOutgoingMessage rmMsg = server.CreateMessage();
                        rmMsg.Write((byte)125);
                        rmMsg.Write(getPlayerID(message.SenderConnection));
                        server.SendToAll(rmMsg, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = $"Weapons removed for {player_list[getPlayerArrayIndex(message.SenderConnection)].myName}";
                        break;
                    case "/ghost":
                        //TODO : remove or make better command -- testing only
                        NetOutgoingMessage ghostMsg = server.CreateMessage();
                        ghostMsg.Write((byte)105);
                        server.SendMessage(ghostMsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
                        break;
                    case "/rain":
                        if (command.Length > 1)
                        {
                            if (float.TryParse(command[1], out float duration))
                            {
                                NetOutgoingMessage rainTime = server.CreateMessage();
                                rainTime.Write((byte)35);
                                rainTime.Write(duration);
                                server.SendToAll(rainTime, NetDeliveryMethod.ReliableSequenced);
                                responseMsg = $"It's raining it's pouring the old man is snoring! (rain duration: ~{duration} seconds)";
                            }
                            else
                            {
                                responseMsg = $"Invalid duration '{command[1]}'";
                            }
                        }
                        else
                        {
                            responseMsg = "Please enter a duration amount.";
                        }
                        break;
                    case "/togglewin":
                        doWinCheck = !doWinCheck;
                        responseMsg = $"server var, doWinCheck = {doWinCheck}";
                        break;
                    case "/drink":
                        Logger.Success("drink command");
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                player_list[id].drinkies = (byte)amount;
                                responseMsg = $"done {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /drink {ID} {AMOUNT}"; }
                        break;
                    case "/tapes":
                        Logger.Success("tape command");
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                player_list[id].armorTapes = (byte)amount;
                                responseMsg = $"done {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /tape {ID} {AMOUNT}"; }
                        break;
                    case "/pos":
                        Logger.Success("position command");
                        try
                        {
                            Player ___this = player_list[getPlayerArrayIndex(message.SenderConnection)];
                            responseMsg = $"Your position: ({___this.position_X}, {___this.position_Y})";
                        }
                        catch
                        {
                            responseMsg = "Error processing command.";
                        }
                        break;
                    case "/spawndrink":
                        Logger.Success("spawndrink command");
                        if (command.Length > 3)
                        {
                            try
                            {
                                amount = short.Parse(command[1]);
                                cPosX = float.Parse(command[2]);
                                cPosY = float.Parse(command[3]);
                                server.SendToAll(MakeNewDrinkLootItem(amount, new float[] { cPosX, cPosY, cPosX, cPosY }), NetDeliveryMethod.ReliableSequenced);
                                responseMsg = $"Created Drink item with {amount}Oz of Juice @ ({cPosX}, {cPosY})";
                            }
                            catch
                            {
                                responseMsg = "Error processing command.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /spawndrink {amount}, {X}, {Y}"; }
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
                //server.SendMessage(allchatmsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
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
            plr.ActiveSlot = sentSlot;

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
            Player _plr = player_list[getPlayerArrayIndex(message.SenderConnection)];
            if ((_plr.MyLootItems[2].GunAmmo - 1) >= 0)
            {
                _plr.MyLootItems[2].GunAmmo -= 1;
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
                if (_plr.MyLootItems[2].GunAmmo == 0)
                {
                    _plr.MyLootItems[2] = new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0);
                }
                return;
            }
            Logger.Failure($"[[serverSendGrenadeThrowing]] - Amount of grenades-1 = <0 | {_plr.MyLootItems[2].GiveAmount-1}");
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
            plr.isDrinking = true;

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
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)111);
            msg.Write(getPlayerID(message.SenderConnection));
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        //client[98] > server[99] -- started taping
        /// <summary>
        /// Sends to everyone in the match a player who has started to tape along with their position.
        /// </summary>
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

        /// <summary>
        /// Creates a NetOutgoingMessage which tells the client about a new Drink LootItem
        /// </summary>
        private NetOutgoingMessage MakeNewDrinkLootItem(short aDrinkAmount, float[] aPositions)
        {
            s_TotalLootCounter++; // Increase LootCounter by 1
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(s_TotalLootCounter);      // LootID       |  Int
            msg.Write((byte)LootType.Juices);   // LootType     |  Byte
            msg.Write(aDrinkAmount);            // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write((byte)0);
            ItemList.Add(s_TotalLootCounter, new LootItem(s_TotalLootCounter, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{aDrinkAmount}", 0, (byte)aDrinkAmount));
            return msg;
        }
        /// <summary>
        /// Creates a new Armor Loot Item generation message to be sent out, and adds the new item to the loot list. This message MUST be sent.
        /// </summary>
        private NetOutgoingMessage MakeNewArmorLootItem(byte armorTicks, byte armorTier, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            s_TotalLootCounter++;               // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(s_TotalLootCounter);      // LootID       |  Int
            msg.Write((byte)LootType.Armor);    // LootType     |  Byte
            msg.Write((short)armorTicks);       // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write(armorTier);               // Rarity       |  Byte
            ItemList.Add(s_TotalLootCounter, new LootItem(s_TotalLootCounter, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{armorTier}", armorTier, armorTicks));
            return msg;
        }

        /// <summary>
        /// Fills the Server's LootItem-List. Only use this once.
        /// </summary>
        private void GenerateItemLootList(int seed)
        {
            Logger.Warn("Attempting to Generate ItemList");
            MersenneTwister MerTwist = new MersenneTwister((uint)seed);
            ItemList = new Dictionary<int, LootItem>();
            int LootID = 0;
            s_TotalLootCounter = 0;
            bool YesMakeBetter;
            uint MinGenValue;
            uint num;
            List<short> WeaponsToChooseByIndex = new List<short>();
            //Logger.DebugServer($"This Weapon List Length: {MyWeaponsList.Length}"); -- can remove

            //for each weapon in the game/dataset, add each into a frequency list of all weapons by its-Frequency-amount-of-times
            // does that make sense?
            for (int i = 0; i < s_WeaponsList.Length; i++)
            {
                for (int j = 0; j < s_WeaponsList[i].SpawnFrequency; j++)
                {
                    //Logger.Basic($"My Frequency:Index -- {MyWeaponsList[i].SpawnFrequency}:{MyWeaponsList[i].JSONIndex}"); --remove but looks cool
                    WeaponsToChooseByIndex.Add(s_WeaponsList[i].JSONIndex);
                }
            }
            // Generate Loot \\
            //i < ( [Ammount of Regular Loot Spawns] + [Amount of Special Loot Spawns] + [Amount of 'no-bot' Loot Spawns]
            // found stuff: Regular: 1447; Better Odds: 390; Bot Spawn: 0
            for (int i = 0; i < 1837; i++)
            {
                //LootID++; -- > LootID++ after completing a loop. sorta.
                LootID = s_TotalLootCounter;
                s_TotalLootCounter++;
                MinGenValue = 0U;
                YesMakeBetter = false;
                //if (i >= 1447) YesMakeBetter = true;
                //if (YesMakeBetter) MinGenValue = 20U;
                if (i >= 1447)
                {
                    YesMakeBetter = true;
                    MinGenValue = 20U;
                }
                num = MerTwist.NextUInt(MinGenValue, 100U);
                //Logger.DebugServer($"This generated number: {num}");
                if (num > 33.0)
                {
                    //Logger.Basic(" -> Greater than 33.0");

                    if (num <= 47.0) // Create Health Juice LootItem
                    {
                        //Logger.Basic(" --> Less than or equal to 47.0 | Juices");
                        byte JuiceAmount = 40;
                        // We get here in 1 loop. MinGenValue is always set each time we check for a new num. YesMakeBetter is also set
                        // up there as well. So, if we want to make another 0-100 & [NewMinimum]-100; MinGenValue is already 0, and 
                        // YesMakeBetter will already be set to true/false so we can check that and set our minimum if we need to
                        if (YesMakeBetter) MinGenValue = 15U;
                        num = MerTwist.NextUInt(MinGenValue, 100U);

                        if (num <= 55.0)
                        {
                            JuiceAmount = 10;
                        }
                        else if (num <= 89.0)
                        {
                            JuiceAmount = 20;
                        }
                        ItemList.Add(LootID, new LootItem(LootID, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{JuiceAmount}", 0, JuiceAmount));
                    }
                    else if (num <= 59.0) // LootType.Armor
                    {
                        // Logger.Basic(" --> Less than or equal to 59.0 | Armor");
                        if (YesMakeBetter) MinGenValue = 24U;
                        num = MerTwist.NextUInt(MinGenValue, 100U);
                        //Logger.Basic($" - -- > Armor Generate | Min: {MinGenValue}");
                        byte GenTier = 3;
                        if (num <= 65.0)
                        {
                            GenTier = 1;
                        }
                        else if (num <= 92)
                        {
                            GenTier = 2;
                        }
                        /*old check worser math. either gets a tier 2 or 3.
                        if (65.0 < num && num <= 92.0)
                        {
                            GenTier = 2;
                        }
                        else if (num < 92.0)
                        {
                            GenTier = 3;
                        } //else --> GenTier = 1 */
            ItemList.Add(LootID, new LootItem(LootID, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{GenTier}", GenTier, GenTier)); // GiveAmount for armor is how many tick it has. gotta reuse stuff
                    }
                    else
                    {
                        if (num <= 60.0) // Skip :) [???]
                        {
                        }
                        else if (num <= 66.0) // Tape
                        {
                            ItemList.Add(LootID, new LootItem(LootID, LootType.Tape, WeaponType.NotWeapon, "Tape", 0, 1));
                        }
                        else // Weapon Generation
                        {
                            // WARNING | These gun creations have the potential to make the RNG go out of sync due to the more random nature of this item type generation
                            // If anything goes wrong with RNG... well it might be this but it also might not. But most changes to WeaponData WILL have an effect here

                            short thisInd = WeaponsToChooseByIndex[(int)MerTwist.NextUInt(0U, (uint)WeaponsToChooseByIndex.Count)];
                            Weapon GeneratedWeapon = s_WeaponsList[(int)thisInd];
                            //num = MerTwist.NextUInt(0U, (uint)Weapon.AllWeaponsInGame.Length);
                            //num = MerTwist.NextUInt(0U, 101U);

                            //Logger.Basic($"   -> Initial Weapon Chosen: {num}");

                            //WeaponType WeaponType_ = GeneratedWeapon.WeaponType;
                            //for now, I just made it advance a frame

                            //num = allWeaponsList[ (int)( MerTwist(0U, (uint)( allWeaponsList.Count ) ) ) ];

                            /* This is very annoying.
                             * Game loads a list of every single weapon in the game. Literally everything.
                             * Stores that in a list somewhere. Uses that to generate weapos.
                             * num = some randomly chosen weapon
                             * WeaponType = what the actual WeaponType for that weapon is
                             * if the weapon is a melee one, we skip
                             * if it is a gun go do gun stuff
                             * if it is a throwable you just spawn it depending on how many should spawn in the overworld
                             *  (if memory serves correctly; bananas you are able to 2 of. everything other throwable spawns in 1s)
                             */


                            if (GeneratedWeapon.WeaponType == WeaponType.Gun)
                            {
                                byte ItemRarity = 0;
                                if (YesMakeBetter) MinGenValue = 22U;
                                num = MerTwist.NextUInt(MinGenValue, 100U);
                                if (58.0 < num && num <= 80.0)
                                {
                                    ItemRarity = 1;
                                }
                                else if (80.0 < num && num <= 91.0)
                                {
                                    ItemRarity = 2;
                                }
                                else if (91.0 < num && num <= 97.0)
                                {
                                    ItemRarity = 3;
                                }
                                if (ItemRarity > GeneratedWeapon.RarityMaxVal) ItemRarity = GeneratedWeapon.RarityMaxVal;
                                if (ItemRarity < GeneratedWeapon.RarityMinVal) ItemRarity = GeneratedWeapon.RarityMinVal;
                                ItemList.Add(LootID, new LootItem(LootID, LootType.Weapon, WeaponType.Gun, GeneratedWeapon.Name, ItemRarity, (byte)GeneratedWeapon.JSONIndex, GeneratedWeapon.ClipSize));

                                // Spawn Ammo
                                for (int a = 0; a < 2; a++)
                                {
                                    LootID = s_TotalLootCounter;
                                    s_TotalLootCounter++;
                                    //LootID++;
                                    ItemList.Add(LootID, new LootItem(LootID, LootType.Ammo, WeaponType.NotWeapon, $"Ammo-Type{GeneratedWeapon.AmmoType}", GeneratedWeapon.AmmoType, GeneratedWeapon.AmmoSpawnAmount));
                                }
                            }
                            else if (GeneratedWeapon.WeaponType == WeaponType.Throwable)
                            {
                                ItemList.Add(LootID, new LootItem(LootID, LootType.Weapon, WeaponType.Throwable, GeneratedWeapon.Name, 0, (byte)GeneratedWeapon.JSONIndex, GeneratedWeapon.SpawnSizeOverworld));
                            }
                        }
                    }
                }
            }
            //Logger.Success($"Successfully generated the ItemList.Count:LootIDCount {ItemList.Keys.Count}:{LootID + 1}");
            //Logger.Success($"ItemList.Count:LootIDCount -- {ItemList.Keys.Count}:{s_TotalLootCounter}");
        }

        #region player list methods
        /// <summary>
        /// Puts null instances in the playerlist at the bottom of the list. Does not any of them in sequential order.
        /// </summary>
        private void sortPlayersListNull()
        {
            Logger.testmsg("attempting to sort playerlist...");
            if (!isSorting)
            {
                isSorting = true;
                Player[] temp_plrlst = new Player[player_list.Length];
                int newIndex = 0;
                for (int i = 0; i < player_list.Length; i++)
                {
                    if (player_list[i] != null)
                    {
                        temp_plrlst[newIndex] = player_list[i];
                        newIndex++;
                    }
                }
                player_list = temp_plrlst;
                isSorting = false;
                isSorted = true;
            } else
            {
                Logger.Warn("Attempted to sort out nulls in playerlist while sorting already in progress.\n");
                return;
            }
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

        private short getPlayerListLength()
        {
            // This isn't used but the older version was so stupidly overcomplicated.
            short length;
            if (!isSorted)
            {
                sortPlayersListNull();
            }
            Logger.Basic($"length of playerList array = {player_list.Length}");
            for (length = 0; length < player_list.Length; length++)
            {
                if (player_list[length] == null)
                {
                    break;
                }
            }
            Logger.Basic($"returned value: {length}");
            return length;
        }

        /// <summary>
        /// Traverses the player list array in 
        /// </summary>
        /// <returns>True if ID is found in the array; False if otherwise.</returns>
        private bool tryFindPlayerIDbyName(string searchName, out short outID)
        {
            searchName = searchName.ToLower();
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myName.ToLower() == searchName)
                {
                    outID = player_list[i].myID;
                    return true;
                }
            }
            outID = -1;
            return false;
        }
        /// <summary>
        /// Traverses the player list array in search of the index which the provided Player ID is located.
        /// </summary>
        /// <returns>True if ID is found in the array; False if otherwise.</returns>
        private bool tryFindArrayIndexByID(int searchID, out int returnedIndex)
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myID == searchID) //searchID is int, myID is short.
                {
                    returnedIndex = player_list[i].myID;
                    return true;
                }
            }
            returnedIndex = -1;
            return false;
        }

        //Helper Functions to get playerID
        /// <summary>
        /// Finds the ID of a player with a given name. Returns the first found instance. Returns -1 if the player cannot be found.
        /// </summary>
        private short getPlayerIDwithUsername(string searchName)
        {
            short retID = -1;
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myName == searchName)
                {
                    retID = player_list[i].myID;
                }
            }
            return retID;
        }
        /// <summary>
        /// Finds the array index of a user with the provided ID. Returns -1 if the player cannot be found.
        /// </summary>
        private int findArrayIndexWithID(short searchID)
        {
            int retID = -1;
            Logger.Success("working... 1");
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myID == searchID) //searchID is int, myID is short.
                {
                    retID = player_list[i].myID;
                    break;
                }
            }
            if (retID == -1) { Logger.Failure("NO ONE WAS FOUND WITH THAT INDEX. RET ID IS -1 (NO ONE FOUND)"); }
            return retID;
        }

        /// <summary>
        /// Grabs the ID of a player with the provided NetConnection. If a player is unable to be located "-1" is returned.
        /// </summary>
        //todo: make sure to fix along with getPlayerArrayIndex (have not checked in a while)
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
        }
        private short getPlayerArrayIndex(NetConnection thisSender)
        {
            short id = -1;
            for (id = 0;  id < player_list.Length; id++)
            {
                if (player_list[id] != null)
                {
                    if (player_list[id].sender == thisSender)
                    {
                        //Logger.Success($"This sender ({thisSender.RemoteEndPoint}) is at array-index {id}, with an assigned ID of {player_list[id].myID}");
                        return id;
                    }
                }
            }
            Logger.Failure("NO PLAYER WAS FOUND WITH THE GIVEN SENDER ADDRESS");
            return -1;
            //Logger.Header($"Returned ID will be: {id}");
            //return id;
        }
        #endregion
    }
}