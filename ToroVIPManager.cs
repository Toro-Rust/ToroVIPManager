using System;
using System.Collections.Generic;
using System.IO;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ToroVIPManager", "Yac Vaguer", "1.0.1")]
    [Description("Manage VIP status with expiration")]
    class ToroVIPManager : CovalencePlugin
    {
        private Dictionary<ulong, DateTime> activeVIPs = new Dictionary<ulong, DateTime>();
        private string dataFile = Path.Combine(Interface.Oxide.DataDirectory, "VIPManager/active.json");
        private ConfigData configData;

        void OnServerInitialized()
        {
            LoadData();
            LoadConfigVariables();
        }

        void Unload()
        {
            SaveData();
            SaveConfig();
        }

        private void LoadData()
        {
            activeVIPs = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, DateTime>>(dataFile);
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

        void AddOrUpdateVIP(ulong userID)
        {
            DateTime expiration;
            if (activeVIPs.ContainsKey(userID) && activeVIPs[userID] > DateTime.Now)
            {
                expiration = activeVIPs[userID].AddDays(30);
            }
            else
            {
                expiration = DateTime.Now.AddDays(30);
            }

            activeVIPs[userID] = expiration;
            SaveData();

            // Execute the list of commands for adding a user to VIP
            foreach (string command in configData.Commands.AddCommands)
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Normal, command.Replace("{STEAMID}", userID.ToString()));
            }
        }

        void ExpireVIP(ulong userID)
        {
            if (activeVIPs.ContainsKey(userID))
            {
                activeVIPs.Remove(userID);
                SaveData();

                // Execute the list of commands for removing a user from VIP
                foreach (string command in configData.Commands.RemoveCommands)
                {
                    ConsoleSystem.Run(ConsoleSystem.Option.Server.Normal, command.Replace("{STEAMID}", userID.ToString()));
                }
            }
        }

        [ConsoleCommand("vip.add")]
        void cmdVIPAdd(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: vip.add <STEAMID>");
                return;
            }

            string steamID = arg.Args[0];
            ulong userID;
            if (ulong.TryParse(steamID, out userID))
            {
                AddOrUpdateVIP(userID);
                Puts($"Added/extended VIP for {steamID}");
            }
            else
            {
                Puts("Invalid STEAMID");
            }
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



