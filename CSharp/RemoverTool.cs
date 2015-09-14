using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RemoverTool", "Reneb", "3.0.0", ResourceId = 651)]
    class RemoverTool : RustPlugin
    {

        static FieldInfo serverinput;
        static FieldInfo buildingPrivlidges;
        static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building" });

        enum RemoveType
        {
            Normal,
            Admin,
            All
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Oxide Hooks
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        void Loaded()
        {
            json = json.Replace("{xmin}", xmin).Replace("{xmax}", xmax).Replace("{ymin}", ymin).Replace("{ymax}", ymax);
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            buildingPrivlidges = typeof(BasePlayer).GetField("buildingPrivlidges", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }

        void OnServerInitialized()
        {
            InitializeRustIO();
            InitializeTable();
            if (!permission.PermissionExists(normalPermission)) permission.RegisterPermission(normalPermission, this);
            if (!permission.PermissionExists(adminPermission)) permission.RegisterPermission(adminPermission, this);
            if (!permission.PermissionExists(allPermission)) permission.RegisterPermission(allPermission, this);
            if (!permission.PermissionExists(targetPermission)) permission.RegisterPermission(targetPermission, this);
        }

        void Unload()
        {
            foreach(ToolRemover toolremover in Resources.FindObjectsOfTypeAll<ToolRemover>())
            {
                GameObject.Destroy(toolremover);
            }
        }

        private static Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();
        private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Configs
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        protected override void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        static string xmin = "0.1";
        static string xmax = "0.4";
        static string ymin = "0.65";
        static string ymax = "0.90";

        static int RemoveTimeDefault = 30;
        static int MaxRemoveTime = 120;
        static int playerDistanceRemove = 3;
        static int adminDistanceRemove = 20;
        static int allDistanceRemove = 300;

        static int adminAuthLevel = 1;
        static int playerAuthLevel = 0;
        static string normalPermission = "canremove";
        static string adminPermission = "canremoveadmin";
        static string allPermission = "canremoveall";
        static string targetPermission = "canremovetarget";

        static bool useBuildingOwners = true;
        static bool useRustIO = true;
        static bool useToolCupboard = true;

        static bool useRaidBlocker = true;
        static int RaidBlockerTime = 300;
        static int RaidBlockerRadius = 80;

        static bool usePay = true;
        static bool payDeployable = true;
        static bool payStructure = true;
        static Dictionary<string, object> payForRemove = defaultPay();

        static bool useRefund = true;
        static bool refundDeployable = true;
        static bool refundStructure = true;
        static Dictionary<string, object> refundPercentage = defaultRefund();

        static string MessageErrorNoAccess = "You are not allowed to use this command";
        static string MessageMultiplePlayersFound = "Multiple players found";
        static string MessageNoPlayersFound = "No players found";
        static string MessageTargetRemoveEnded = "The Remover Tool for {0} has ended";
        static string MessageErrorNothingToRemove = "Couldn't find anything to remove. Are you close enough?";
        static string MessageErrorNotAllowedToRemove = "You have no rights to remove this";
        static string MessageErrorNotEnoughPay = "You don't have enough to pay for this remove";
        static string MessageErrorExternalBlock = "You are not allowed use the remover tool at the moment";
        static string MessageOverrideDisabled = "The remover tool was disabled for the time being.";
        static string MessageToolDeactivated = "{0}: Remover Tool Deactivated";

        void Init()
        {
            CheckCfg<string>("Message - Not Allowed", ref MessageErrorNoAccess);
            CheckCfg<string>("Message - Multiple Players Found", ref MessageMultiplePlayersFound);
            CheckCfg<string>("Message - No Players Found", ref MessageNoPlayersFound);
            CheckCfg<string>("Message - Target Remover Tool Ended", ref MessageTargetRemoveEnded);
            CheckCfg<string>("Message - Nothing To Remove", ref MessageErrorNothingToRemove);
            CheckCfg<string>("Message - No Rights To Remove This", ref MessageErrorNotAllowedToRemove);
            CheckCfg<string>("Message - Not Enough To Pay", ref MessageErrorNotEnoughPay);
            CheckCfg<string>("Message - External Plugin Blocking Remove", ref MessageErrorExternalBlock);
            CheckCfg<string>("Message - Admin Override Disabled the Remover Tool", ref MessageOverrideDisabled);
            CheckCfg<string>("Message - Remover Tool Ended", ref MessageToolDeactivated);

            CheckCfg<string>("GUI - Position - X Min", ref xmin);
            CheckCfg<string>("GUI - Position - X Max", ref xmax);
            CheckCfg<string>("GUI - Position - Y Min", ref ymin);
            CheckCfg<string>("GUI - Position - Y Max", ref ymax);

            CheckCfg<int>("Remove - Default Time", ref RemoveTimeDefault);
            CheckCfg<int>("Remove - Max Remove Time", ref MaxRemoveTime);
            if(MaxRemoveTime > 300)
            {
                Debug.Log("RemoverTool: Sorry but i won't let you use the Max Remove Time for longer then 300seconds");
                MaxRemoveTime = 300;
            }
            CheckCfg<int>("Remove - Distance - Player", ref playerDistanceRemove);
            CheckCfg<int>("Remove - Distance - Admin", ref adminDistanceRemove);
            CheckCfg<int>("Remove - Distance - All", ref allDistanceRemove);

            CheckCfg<int>("Remove - Auth - AuthLevel - Normal Remove", ref playerAuthLevel);
            CheckCfg<int>("Remove - Auth - AuthLevel - Admin Commands", ref adminAuthLevel);
            CheckCfg<string>("Remove - Auth - Permission - Normal Remove", ref normalPermission);
            CheckCfg<string>("Remove - Auth - Permission - Admin Remove", ref adminPermission);
            CheckCfg<string>("Remove - Auth - Permission - All Remove", ref allPermission);
            CheckCfg<string>("Remove - Auth - Permission - Target Remove", ref targetPermission);

            CheckCfg<bool>("Remove - Access - Use Building Owners", ref useBuildingOwners);
            CheckCfg<bool>("Remove - Access - Use RustIO & BuildingOwners (Building Owners needs to be true)", ref useRustIO);
            CheckCfg<bool>("Remove - Access - Use ToolCupboards", ref useToolCupboard);

            CheckCfg<bool>("Remove - Pay", ref usePay);
            CheckCfg<bool>("Remove - Pay - Deployables", ref payDeployable);
            CheckCfg<bool>("Remove - Pay - Structures", ref payStructure);
            CheckCfg<Dictionary<string,object>>("Remove - Pay - Costs", ref payForRemove);

            CheckCfg<bool>("Remove - Refund", ref useRefund);
            CheckCfg<bool>("Remove - Refund - Deployables", ref refundDeployable);
            CheckCfg<bool>("Remove - Refund - Structures", ref refundStructure);
            CheckCfg<Dictionary<string, object>>("Remove - Refund - Percentage (Structures Only)", ref refundPercentage);

            SaveConfig();
        }

        static Dictionary<string, object> defaultPay()
        {
            var dp = new Dictionary<string, object>();

            var dp0 = new Dictionary<string, object>();
            dp0.Add("wood", "1");
            dp.Add("0", dp0);

            var dp1 = new Dictionary<string, object>();
            dp1.Add("wood", "100");
            dp.Add("1", dp1);

            var dp2 = new Dictionary<string, object>();
            dp2.Add("wood", "100");
            dp2.Add("stone", "150");
            dp.Add("2", dp2);

            var dp3 = new Dictionary<string, object>();
            dp3.Add("wood", "100");
            dp3.Add("stone", "50");
            dp3.Add("metal fragments", "75");
            dp.Add("3", dp3);

            var dp4 = new Dictionary<string, object>();
            dp4.Add("wood", "250");
            dp4.Add("stone", "350");
            dp4.Add("metal fragments", "75");
            dp4.Add("high quality metal", "25");
            dp.Add("4", dp4);

            var dpdepoyable = new Dictionary<string, object>();
            dpdepoyable.Add("wood", "50");
            dp.Add("deployable", dpdepoyable);

            return dp;
        }

        static Dictionary<string, object> defaultRefund()
        {
            var dr = new Dictionary<string, object>();

            dr.Add("0", "100.0");
            dr.Add("1", "80.0");
            dr.Add("2", "60.0");
            dr.Add("3", "40.0");
            dr.Add("4", "20.0");

            return dr;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// RustIO Inclusion
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static Library RustIO;
        private static MethodInfo isInstalled;
        private static MethodInfo hasFriend;

        private static bool RustIOIsInstalled()
        {
            if (RustIO == null) return false;
            return (bool)isInstalled.Invoke(RustIO, new object[] { });
        }
        private void InitializeRustIO()
        {
            if (!useRustIO)
            {
                RustIO = null;
                return;
            }
            RustIO = Interface.GetMod().GetLibrary<Library>("RustIO");
            if (RustIO == null || (isInstalled = RustIO.GetFunction("IsInstalled")) == null || (hasFriend = RustIO.GetFunction("HasFriend")) == null)
            {
                RustIO = null;
                Puts("{0}: {1}", Title, "Rust:IO is not present. You need to install Rust:IO first in order to use the RustIO option!");
            }
        }
        private static bool HasFriend(string playerId, string friendId)
        {
            if (RustIO == null) return false;
            return (bool)hasFriend.Invoke(RustIO, new object[] { playerId, friendId });
        }
        
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Random Functions
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        bool hasAccess(BasePlayer player, string permissionName, int minimumAuth)
        {
            if (player.net.connection.authLevel >= minimumAuth) return true;
            if (permission.UserHasPermission(player.userID.ToString(), permissionName)) return true;
            SendReply(player, MessageErrorNoAccess);
            return false;
        }

        object FindOnlinePlayer(string arg, out BasePlayer playerFound)
        {
            playerFound = null;

            ulong steamid = 0L;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (steamid != 0L)
                    if (player.userID == steamid)
                    {
                        playerFound = player;
                        return true;
                    }
                string lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    if (playerFound == null)
                        playerFound = player;
                    else
                        return MessageMultiplePlayersFound;
                }
            }
            if (playerFound == null) return MessageNoPlayersFound;
            return true;
        }

        static void PrintToChat(BasePlayer player, string message)
        {
            player.SendConsoleCommand("chat.add", new object[] { 0, message, 1f });
        }

        static BaseEntity FindRemoveObject(Ray ray, float distance)
        {
            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(ray, out hit, distance, constructionColl))
                return null;
            return hit.collider.GetComponentInParent<BaseEntity>();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// class Tool Remover
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        class ToolRemover : MonoBehaviour
        {
            public BasePlayer player;
            public int endTime;
            public int timeLeft;
            public RemoveType removeType;
            public BasePlayer playerActivator;
            public float distance;
            public float lastUpdate;

            public InputState inputState;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            }

            public void RefreshDestroy()
            {
                timeLeft = endTime;
                CancelInvoke("DoDestroy");
                CancelInvoke("RefreshRemoveGui");
                Invoke("DoDestroy", endTime);
                InvokeRepeating("RefreshRemoveGui", 1, 1);
            }

            void DoDestroy()
            {
                GameObject.Destroy(this);
            }

            void RefreshRemoveGui()
            {
                timeLeft--;
                RefreshGUI(this);
            }

            void FixedUpdate()
            {
                if (!player.IsConnected() || player.IsDead()) { GameObject.Destroy(this); return; }
                inputState = serverinput.GetValue(player) as InputState;
                if (inputState.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    float currentTime = UnityEngine.Time.realtimeSinceStartup;
                    if (lastUpdate + 0.5f < currentTime)
                    {
                        lastUpdate = currentTime;
                        Ray ray = new Ray(player.eyes.position, Quaternion.Euler(inputState.current.aimAngles) * Vector3.forward);
                        TryRemove(player, ray, removeType, distance);
                    }
                }
            }

            void OnDestroy()
            {
                if(player.net != null)
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "RemoveMsg");
                if(playerActivator != player)
                {
                    if (playerActivator.IsConnected())
                        PrintToChat(playerActivator, string.Format(MessageTargetRemoveEnded, player.displayName));
                }
            }
        }
        void EndRemoverTool(BasePlayer player)
        {
            ToolRemover toolremover = player.GetComponent<ToolRemover>();
            if (toolremover == null) return;
            GameObject.Destroy(toolremover);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// GUI
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static string json = @"[  
		{ 
			""name"": ""RemoveMsg"",
			""parent"": ""Overlay"",
			""components"":
			[
				{
					 ""type"":""UnityEngine.UI.Image"",
					 ""color"":""0.1 0.1 0.1 0.7"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""{xmin} {ymin}"",
					""anchormax"": ""{xmax} {ymax}""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Remover Tool {removeType}"",
					""fontSize"":15,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.0 0.83"",
					""anchormax"": ""1.0 0.98""
				}
			]
		},
        {
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Time left"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.05 0.65"",
					""anchormax"": ""0.3 0.80""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{timeleft}s"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.4 0.65"",
					""anchormax"": ""1.0 0.80""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Entity"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.05 0.50"",
					""anchormax"": ""0.3 0.65""
				}
			]
		},
        {
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{entity}"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.4 0.50"",
					""anchormax"": ""1.0 0.65""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Cost"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.05 0.0"",
					""anchormax"": ""0.3 0.50""
				}
			]
		},
        {
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{cost}"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.4 0.0"",
					""anchormax"": ""1.0 0.5""
				}
			]
		}
		]
		";
        static void RefreshGUI(ToolRemover toolPlayer)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = toolPlayer.player.net.connection }, null, "DestroyUI", "RemoveMsg");
            string cost = string.Empty;
            string entity = string.Empty;

            toolPlayer.inputState = serverinput.GetValue(toolPlayer.player) as InputState;
            Ray ray = new Ray(toolPlayer.player.eyes.position, Quaternion.Euler(toolPlayer.inputState.current.aimAngles) * Vector3.forward);

            BaseEntity removeObject = FindRemoveObject(ray, toolPlayer.distance);
            if (removeObject != null)
            {
                entity = removeObject.ToString();
                entity = entity.Substring(entity.LastIndexOf("/") + 1).Replace(".prefab", "").Replace("_deployed", "").Replace(".deployed", "");
                entity = entity.Substring(0, entity.IndexOf("["));
                if (toolPlayer.removeType == RemoveType.Normal)
                {
                    Dictionary<string, object> costList = GetCost(removeObject);
                    foreach (KeyValuePair<string, object> pair in costList)
                    {
                        cost += string.Format("{0} x{1}\n", pair.Key, pair.Value.ToString());
                    }
                }
            }
            string pjson = json.Replace("{entity}", entity).Replace("{cost}", cost).Replace("{timeleft}", toolPlayer.timeLeft.ToString()).Replace("{removeType}",toolPlayer.removeType == RemoveType.Normal ? string.Empty : toolPlayer.removeType == RemoveType.Admin ? "(Admin)" : "(All)");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = toolPlayer.player.net.connection }, null, "AddUI", pjson);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Remove functions
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static void TryRemove(BasePlayer player, Ray ray, RemoveType removeType, float distance)
        {
            BaseEntity removeObject = FindRemoveObject(ray, distance);
            if (removeObject == null)
            {
                PrintToChat(player, MessageErrorNothingToRemove);
                return;
            }
            if (!CanRemoveEntity(player, removeObject, removeType))
            {
                PrintToChat(player, MessageErrorNotAllowedToRemove);
                return;
            }
            if (!CanPay(player, removeObject, removeType))
            {
                PrintToChat(player, MessageErrorNotEnoughPay);
                return;
            }
            if(removeType == RemoveType.All)
            {
                Interface.Call("RemoveAllFrom",removeObject.transform.position);
                return;
            }
            Refund(player, removeObject, removeType);
            DoRemove(removeObject);
        }

        List<Vector3> removeFrom = new List<Vector3>();
        int currentRemove = 0;
        void RemoveAllFrom(Vector3 pos)
        {
            removeFrom.Add(pos);
            DelayRemoveAll();
        }


        List<Collider> wasRemoved = new List<Collider>();
        void DelayRemoveAll()
        {
            if(currentRemove >= removeFrom.Count)
            {
                currentRemove = 0;
                removeFrom.Clear();
                wasRemoved.Clear();
                return;
            }
            foreach(Collider col in Physics.OverlapSphere(removeFrom[currentRemove], 3f, constructionColl))
            {
                if (wasRemoved.Contains(col)) continue;
                if (!removeFrom.Contains(col.transform.position))
                    removeFrom.Add(col.transform.position);
                wasRemoved.Add(col);
                DoRemove(col.GetComponentInParent<BaseEntity>());
            }
            currentRemove++;
            timer.Once(0.01f, () => DelayRemoveAll());
        }

        static void DoRemove(BaseEntity removeObject)
        {
            if (removeObject == null) return;
            removeObject.KillMessage();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Refund
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static void Refund(BasePlayer player, BaseEntity entity, RemoveType removeType)
        {
            if (removeType == RemoveType.All) return;
            if (refundDeployable && entity is WorldItem)
            {
                WorldItem worlditem = entity as WorldItem;
                if (worlditem.item != null && worlditem.item.info != null)
                    player.inventory.GiveItem(worlditem.item.info.itemid, 1, true);
            }
            else if (refundStructure && entity is BuildingBlock)
            {
                BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;

                int buildingblockGrade = (int)buildingblock.grade;
                for (int i = buildingblockGrade; i >= 0; i--)
                {
                    if (buildingblock.blockDefinition.grades[i] != null && refundPercentage.ContainsKey(i.ToString()))
                    {
                        decimal refundRate = decimal.Parse((string)refundPercentage[i.ToString()]) / 100.0m;
                        List<ItemAmount> currentCost = buildingblock.blockDefinition.grades[i].costToBuild as List<ItemAmount>;
                        foreach (ItemAmount ia in currentCost)
                        {
                            player.inventory.GiveItem(ia.itemid, Convert.ToInt32((decimal)ia.amount * refundRate), true);
                        }
                    }
                }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Check Access
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static bool hasTotalAccess(BasePlayer player)
        {
            List<BuildingPrivlidge> playerpriv = buildingPrivlidges.GetValue(player) as List<BuildingPrivlidge>;
            if (playerpriv.Count == 0)
            {
                return false;
            }
            foreach (BuildingPrivlidge priv in playerpriv.ToArray())
            {
                List<ProtoBuf.PlayerNameID> authorized = priv.authorizedPlayers;
                bool foundplayer = false;
                foreach (ProtoBuf.PlayerNameID pni in authorized.ToArray())
                {
                    if (pni.userid == player.userID)
                        foundplayer = true;
                }
                if (!foundplayer)
                {
                    return false;
                }
            }
            return true;
        }

        static bool CanRemoveEntity(BasePlayer player, BaseEntity entity, RemoveType removeType)
        {
            if (entity.isDestroyed) return false;
            if (removeType == RemoveType.Admin || removeType == RemoveType.All) return true;
            var externalPlugins = Interface.CallHook("canRemove", player);
            if (externalPlugins != null)
            {
                PrintToChat(player, externalPlugins is string ? (string)externalPlugins : MessageErrorExternalBlock);
                return false;
            }
            if (entity is BuildingBlock && useBuildingOwners)
            {
                var returnhook = Interface.GetMod().CallHook("FindBlockData", new object[] { entity as BuildingBlock });
                if (returnhook is string)
                {
                    string ownerid = (string)returnhook;
                    if (player.userID.ToString() == ownerid) return true;
                    if (useRustIO && RustIOIsInstalled())
                    {
                        if (HasFriend(ownerid, player.userID.ToString()))
                        {
                            return true;
                        }
                    }
                }
            }
            if (useToolCupboard)
                if (hasTotalAccess(player))
                    return true;

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Pay functions
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static bool CanPay(BasePlayer player, BaseEntity entity, RemoveType removeType)
        {
            if (removeType == RemoveType.Admin || removeType == RemoveType.All) return true;
            Dictionary<string, object> cost = GetCost(entity);
            
            foreach(KeyValuePair<string,object> pair in cost)
            {
                string itemname = pair.Key.ToLower();
                if (displaynameToShortname.ContainsKey(itemname))
                    itemname = displaynameToShortname[itemname];
                ItemDefinition itemdef = ItemManager.FindItemDefinition(itemname);
                if (itemdef == null) continue;
                int amount = player.inventory.GetAmount(itemdef.itemid);
                if (amount < Convert.ToInt32(pair.Value))
                    return false;
            }
            return true;
        }
        static Dictionary<string,object> GetCost(BaseEntity entity)
        {
            Dictionary<string, object> cost = new Dictionary<string, object>();
            if (entity.GetComponent<BuildingBlock>() != null)
            {
                BuildingBlock block = entity.GetComponent<BuildingBlock>();
                string grade = ((int)block.grade).ToString();
                if (!payForRemove.ContainsKey(grade)) return cost;
                cost = payForRemove[grade] as Dictionary<string,object>;
            }
            else if(entity.GetComponent<Deployable>() != null)
            {
                if (!payForRemove.ContainsKey("deployable")) return cost;
                cost = payForRemove["deployable"] as Dictionary<string, object>;
            }
            return cost;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Raid Blocker
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /*
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            BuildingBlock block = entity.GetComponent<BuildingBlock>();
            if (block == null) return;

            // DO SOME CHECKS TO SEE IF ITS A RAID OR SOMETHING
            // SPHERECAST ALL PLAYERS TO BLOCK THERE REMOVE
        }
        */
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Console Commands
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        bool overrideDisabled = false;
        [ConsoleCommand("remove.allow")]
        void ccmdRemoveAllow(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "remove.allow true/false");
                return;
            }
            if (arg.connection != null)
            {
                if (!hasAccess(arg.connection.player as BasePlayer, adminPermission, adminAuthLevel)) return;
                {
                    SendReply(arg, MessageErrorNoAccess);
                    return;
                }
            }
            switch(arg.Args[0].ToLower())
            {
                case "true":
                case "1":
                    overrideDisabled = false;
                    SendReply(arg, "Remove is now allowed depending on your settings.");
                    break;
                case "false":
                case "0":
                    overrideDisabled = true;
                    SendReply(arg, "Remove is now restricted for all players (exept admins)");
                    foreach(ToolRemover toolremover in Resources.FindObjectsOfTypeAll<ToolRemover>())
                    {
                        if (toolremover.removeType == RemoveType.Normal)
                        {
                            SendReply(toolremover.player, "The Remover Tool has been disabled by the admin");
                            timer.Once(0.01f, () => GameObject.Destroy(toolremover));
                        }
                    }
                    break;
                default:
                    SendReply(arg, "This is not a valid argument");
                    break;
            }
        }

        [ConsoleCommand("remove.give")]
        void ccmdRemoveGive(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "remove.give PLAYER/STEAMID optional:Time");
                return;
            }
            if (arg.connection != null)
            {
                if (!hasAccess(arg.connection.player as BasePlayer, targetPermission, adminAuthLevel)) return;
            }
            BasePlayer targetPlayer;
            var success = FindOnlinePlayer(arg.Args[0], out targetPlayer);
            if(success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            int removeTime = RemoveTimeDefault;
            if(arg.Args.Length > 1)
                int.TryParse(arg.Args[1], out removeTime);
            if (removeTime > MaxRemoveTime)
                removeTime = MaxRemoveTime;
            ToolRemover toolremover = targetPlayer.GetComponent<ToolRemover>();
            if (toolremover == null)
                toolremover = targetPlayer.gameObject.AddComponent<ToolRemover>();
            toolremover.endTime = removeTime;
            toolremover.removeType = RemoveType.Normal;
            toolremover.playerActivator = targetPlayer;
            toolremover.distance = playerDistanceRemove;
            toolremover.RefreshDestroy();

            SendReply(arg, string.Format("Remover tool was given for {1}s to {0}", targetPlayer.displayName, removeTime.ToString()));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Chat Command
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        [ChatCommand("remove")]
        void cmdChatRemove(BasePlayer player, string command, string[] args)
        {
            int removeTime = RemoveTimeDefault;
            BasePlayer target = player;
            RemoveType removetype = RemoveType.Normal;
            int distanceRemove = playerDistanceRemove;

            if (args.Length != 0)
            {
                switch (args[0])
                {
                    case "admin":
                        if (!hasAccess(player, adminPermission, adminAuthLevel)) return;
                        removetype = RemoveType.Admin;
                        distanceRemove = adminDistanceRemove;
                        break;
                    case "all":
                        if (!hasAccess(player, allPermission, adminAuthLevel)) return;
                        removetype = RemoveType.All;
                        distanceRemove = allDistanceRemove;
                        break;
                    case "target":
                        if (!hasAccess(player, targetPermission, adminAuthLevel)) return;
                        if (args.Length == 1)
                        {
                            SendReply(player, "/remove target PLAYERNAME/STEAMID optional:Time");
                            return;
                        }
                        BasePlayer tempTarget = null;
                        var success = FindOnlinePlayer(args[1], out tempTarget);
                        if (success is string)
                        {
                            SendReply(player, (string)success);
                            return;
                        }
                        target = tempTarget;
                        if (args.Length > 2) int.TryParse(args[2], out removeTime);

                        break;
                    default:
                        if (!hasAccess(player, normalPermission, playerAuthLevel)) return;
                        if (overrideDisabled)
                        {
                            SendReply(player, MessageOverrideDisabled);
                            return;
                        }
                        int.TryParse(args[0], out removeTime);
                        break;
                }
            }

            if (removeTime > MaxRemoveTime) removeTime = MaxRemoveTime;

            ToolRemover toolremover = target.GetComponent<ToolRemover>();
            if (toolremover != null && args.Length == 0)
            {
                EndRemoverTool(target);
                SendReply(player, string.Format(MessageToolDeactivated, target.displayName));
                return;
            }

            if (toolremover == null)
                toolremover = target.gameObject.AddComponent<ToolRemover>();

            toolremover.endTime = removeTime;
            toolremover.removeType = removetype;
            toolremover.playerActivator = player;
            toolremover.distance = (int)distanceRemove;
            toolremover.RefreshDestroy();
        }
    }
}
