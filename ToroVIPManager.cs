using System;
using System.Collections.Generic;
using System.IO;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Oxide.Ext.Chaos.Data;
using ProtoBuf;

/**
 * @author Yac Vaguer
 * @description As a Server Admin you might want to give your players VIP services this plugin will help you to administrate the VIP service 
 *
 * @todo
 * [] Check every day for expired VIP 
 * [] Add/Remove user from a discord group permission  
 * [] Set reminders if a vip is going to reach expiration date soon
 * [] Alert when the user is upgraded/remove to/from VIP 
 * [] Backup information in the API 
 * 
 */
namespace Oxide.Plugins
{
    [Info("ToroVIPManager", "Yac Vaguer", "0.0.1")]
    [Description("Manage VIP status with expiration")]
    class ToroVIPManager : RustPlugin
    {
        private Dictionary<ulong, VIPData> activeVIPs = new Dictionary<ulong, VIPData>();
        private string dataFile = Path.Combine(Interface.Oxide.DataDirectory, "VIPManager/active-vip");

        [ConsoleCommand("vip.add")]
        void cmdVIPAdd(ConsoleSystem.Arg arg)
        {
            Puts("Activate a user as VIP");

            if (arg.Args == null || arg.Args.Length != 2)
            {
                Puts("Usage: vip.add <STEAMID> <RP>");
                return;
            }

            string steamID = arg.Args[0];
            string rewardPoints = arg.Args[1];
            ulong userID;
            if (ulong.TryParse(steamID, out userID))
            {
                AddOrUpdateVIP(userID, rewardPoints);
                return;
            }

            Puts("Invalid STEAMID");

        }

        [ConsoleCommand("vip.remove")]
        void cmdVIPRemove(ConsoleSystem.Arg arg)
        {
            Puts("Remove a user as VIP");

            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: vip.remove <STEAMID>");
                return;
            }

            string steamID = arg.Args[0];
            ulong userID;
            if (ulong.TryParse(steamID, out userID))
            {
                RemoveVIP(userID);
                Puts($"Removed VIP for {steamID}");
                return;
            }

            Puts("Invalid STEAMID");

        }

        /**
         * Removing a user only remove the user from the group vip 
         * You will still have the zlevel bonus but not the rest of the benefits
         */
        void RemoveVIP(ulong userID)
        {
            executeCommand("oxide.usergroup remove {STEAMID} vip", userID);

            activeVIPs[userID].Expiration = DateTime.Now;
            activeVIPs[userID].Status = "disabled";
            SaveData();
        }

        /**
         * Adding a user as VIP means
         * Add the user to the user group vip
         * Increase the zlevels +5 to all the skills
         * Give the user as many RP defined in the command second argument
         */
        void AddOrUpdateVIP(ulong userID, string rewardPoints)
        {

            createVIPIfNotExists(userID);

            if (activeVIPs[userID].Bonus == false)
            {
                /** Only when you add a new user, these are the command we will execute **/
                executeCommand("zl.lvl {STEAMID} * +5", userID);
                Puts("VIP Subscription renewed");
            }
            executeCommand("sr add {STEAMID} {REWARDPOINTS} " + rewardPoints, userID);
            executeCommand("oxide.usergroup add {STEAMID} vip", userID);

            activeVIPs[userID].Expiration = getExpirationDate(userID);
            activeVIPs[userID].Bonus = true;
            activeVIPs[userID].Status = "enabled";
            SaveData();
        }

        /** 
         * Goes over the whole database checking if any VIP expired, in case that 
         * it found some it will remove it as VIP automatically
         */
        void CheckExpired()
        {

            foreach (var player in activeVIPs)
            {
                ulong userID = player.Key;
                VIPData vipData = player.Value;

                if (vipData.Status == "enabled" && vipData.Expiration < DateTime.Now)
                {
                    RemoveVIP(userID);
                }
            }
        }

        void createVIPIfNotExists(ulong userID)
        {
            if (!activeVIPs.ContainsKey(userID))
            {
                VIPData newVIP = new VIPData
                {
                    Expiration = DateTime.Now,
                    Bonus = false,
                    Status = "disabled"
                };
                activeVIPs.Add(userID, newVIP);
            }

        }

        private DateTime getExpirationDate(ulong userID)
        {

            if (activeVIPs[userID].Status == "enabled")
            {
                Puts("VIP Subscription renewed");
                return activeVIPs[userID].Expiration.AddDays(30);

            }
            Puts("VIP Subscription activated");
            return DateTime.Now.AddDays(30);
        }

        void executeCommand(string command, ulong userID)
        {
            string replacedCommand = command.Replace("{STEAMID}", userID.ToString());
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), replacedCommand);
        }

        void OnServerInitialized()
        {
            LoadData();
            CheckExpired();
        }

        private void LoadData()
        {
            activeVIPs = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, VIPData>>(dataFile);

        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject(dataFile, activeVIPs);
        }

    }

    public class VIPData
    {
        public DateTime Expiration { get; set; }
        public bool Bonus { get; set; }
        public string Status { get; set; }
    }

}
