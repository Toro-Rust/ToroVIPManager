using System;
using System.Collections.Generic;
using System.IO;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Net;
using static Oxide.Plugins.ToroVIPManager;

namespace Oxide.Plugins
{
    [Info("ToroVIPManager", "Yac Vaguer", "1.0.1")]
    [Description("Manage VIP status with expiration")]
    class ToroVIPManager : RustPlugin
    {
        private Dictionary<ulong, VIPEntry> activeVIPs = new Dictionary<ulong, VIPEntry>();
        private string dataFile = Path.Combine(Interface.Oxide.DataDirectory, "VIPManager/active-vip");
        private ConfigData configData;
        private VIPEntry vipEntry;

        [ConsoleCommand("vip.add")]
        void GrantUserVIPStatus(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 2)
            {
                Puts("Usage: vip.add <STEAMID> <REWARD_POINTS>");
                return;
            }

            string steamID = arg.Args[0];
            string serverReward = arg.Args[1];
            ulong userID;

            if (!ulong.TryParse(steamID, out userID))
            {
                Puts("Invalid STEAMID");
                return;
            }
            Puts("------------------------------");
            Puts(" ");
            Puts($"Setting VIP Status for {userID}");
            AddOrUpdateVIPToDB(userID);
            AddUserToVipGroup(userID);
            AddPerks(userID);
            AddServerRewards(userID, serverReward);

            Puts($"Added/extended VIP for {steamID}");
            Puts("");

            activeVIPs[userID] = vipEntry;
            SaveData();

            Puts("------------------------------");

            SendDiscordMessage($"User {userID} activated as VIP for 30 days");
        }


        void OnServerInitialized()
        {
            LoadData();
            LoadConfigVariables();
            CheckExpiredVIPs();
        }

        private void CheckExpiredVIPs()
        {
            Puts("------------------------------");
            Puts("");
            Puts("Checking Expired VIPs");
            List<ulong> expiredVIPs = new List<ulong>();

            foreach (KeyValuePair<ulong, VIPEntry> kvp in activeVIPs)
            {
                ulong userID = kvp.Key;
                VIPEntry vipEntry = kvp.Value;

                if (vipEntry != null && vipEntry.Expiration < DateTime.Now && vipEntry.Status == "enabled")
                {
                    expiredVIPs.Add(userID);
                }
            }

            foreach (ulong userID in expiredVIPs)
            {
                ExpireVIP(userID);
            }

            Puts("Check Finished");
            Puts("------------------------------");
            Puts("");
        }

        private void LoadData()
        {
            activeVIPs = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, VIPEntry>>(dataFile);
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject(dataFile, activeVIPs);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
        }

        private void SaveConfig()
        {
            Config.WriteObject(configData, true);
        }

        VIPEntry GetVipEntry(ulong userID)
        {
            if (!activeVIPs.TryGetValue(userID, out VIPEntry vipEntry))
            {
                vipEntry = new VIPEntry();
            }

            return vipEntry;
        }

        void AddOrUpdateVIPToDB(ulong userID)
        {
            vipEntry = GetVipEntry(userID);

            if (vipEntry.Expiration > DateTime.Now)
            {
                vipEntry.Expiration = vipEntry.Expiration.AddDays(30);
            }
            else
            {
                vipEntry.Expiration = DateTime.Now.AddDays(30);
            }

            vipEntry.Status = "enabled";
            Puts($"User {userID} added to the Database as VIP");
            Puts("");
        }

        void AddUserToVipGroup(ulong userID)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "oxide.usergroup add " + userID.ToString() + " vip");
            Puts($"User {userID} Granted VIP features");
            Puts("");
        }
        
        void ExpireVIP(ulong userID)
        {
            if (activeVIPs.TryGetValue(userID, out VIPEntry vipEntry))
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "oxide.usergroup remove " + userID.ToString() + " vip");
                vipEntry.Status = "disabled";

                activeVIPs[userID] = vipEntry;
                SaveData();
                Puts($"{userID} VIP expired and it was removed from our VIPs");
                SendDiscordMessage($"{userID} VIP expired and it was removed from our VIPs");
            }
        }

        void AddServerRewards(ulong userID, string serverRewardPoints)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "sr add " + userID.ToString() + " " + serverRewardPoints);
            Puts("We added " + serverRewardPoints + " to " + userID);
            Puts("");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "sr check " + userID.ToString());
        }

        void AddPerks(ulong userID)
        {

            if (!vipEntry.Bonus)
            {
                vipEntry.Bonus = true;
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "zl.lvl " + userID.ToString() + " * +5");
                Puts($"We added Perks to {userID}");
                return;
            }
            
            Puts("Perks were given before");
            
        }

        private void SendDiscordMessage(string message)
        {
            if (configData == null)
            {
                Puts("Config data is not loaded.");
                return;
            }

            if (string.IsNullOrEmpty(configData.DiscordWebhookUrl))
            {
                Puts("Discord webhook URL is not configured.");
                return;
            }

            var payload = JsonConvert.SerializeObject(new { content = message });

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";

                try
                {
                    client.UploadString(configData.DiscordWebhookUrl, "POST", payload);
                }
                catch (Exception ex)
                {
                    Puts($"Error sending Discord message: {ex.Message}");
                }
            }
        }

        public class VIPEntry
        {
            public DateTime Expiration { get; set; }
            public bool Bonus { get; set; }
            public string Status { get; set; }
        }

        class ConfigData
        {
            public string DiscordWebhookUrl { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                DiscordWebhookUrl = "https://discord.com/api/webhooks/1172735351979266149/R2aoNIBZKfES_I5C-OWgSJVtGwI-8tbqABv2_T9u5K4tXQoFmvLElhB8a1sxXKjAmM0O"
            };

            SaveConfig();
        }

    }
}
