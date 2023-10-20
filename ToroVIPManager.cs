using System;
using System.Collections.Generic;
using System.IO;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ToroVIPManager", "Yac Vaguer", "1.0.0")]
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

        // You can schedule a daily check for expired VIPs here
        // For example, you can use a timer to check VIP expirations once per day
        // Call the ExpireVIP method for users with expired VIP status

        // void OnServerInitialized()
        // {
        //     timer.Every(86400, () =>
        //     {
        //         DateTime now = DateTime.Now;
        //         foreach (var kvp in activeVIPs)
        //         {
        //             if (kvp.Value < now)
        //             {
        //                 ExpireVIP(kvp.Key);
        //             }
        //         }
        //     });
        // }
        
        class ConfigData
        {
            public CommandsConfig Commands { get; set; }
        }

        class CommandsConfig
        {
            public List<string> AddCommands { get; set; }
            public List<string> RemoveCommands { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Commands = new CommandsConfig
                {
                    AddCommands = new List<string>
                    {
                        "oxide.usergroup add {STEAMID} vip"
                        "zlvl {STEAMID} * +5"
                        // Add more commands here as needed
                    },
                    RemoveCommands = new List<string>
                    {
                        "oxide.usergroup remove {STEAMID} vip"
                        "zlvl {STEAMID} * -5"
                        // Add more commands here as needed
                    }
                }
            };
            SaveConfig(config);
        }
    }
}
