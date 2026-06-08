using System.IO;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;

namespace WindowsGSM.Plugins
{
    public class BlackMesa : Source
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.BlackMesa",
            author = "Raziel7893",
            description = "WindowsGSM plugin for supporting Black Mesa Dedicated Server",
            version = "1.0",
            url = "https://github.com/Raziel7893/WindowsGSM.BlackMesa",
            color = "#34FFeb"
        };

        public string FullName = "BlackMesa: Dedicated Server";
        public override string Defaultmap { get { return "gasworks"; } }
        public override string Additional { get { return "-tickrate 64"; } }
        public override string Game { get { return "bms"; } }
        public override string AppId { get { return "346680"; } }

        public BlackMesa(Functions.ServerConfig serverData) : base(serverData)
        {
            base.serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            //Download server.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, Game, "cfg/server.cfg");
            if (File.Exists(configPath))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("Black Mesa: Deathmatch", serverData.ServerName);
                configText = configText + $"\nrcon_password {serverData.GetRCONPassword()}\n";
                configText = configText + $"\nsv_lan 1\n";
                File.WriteAllText(configPath, configText);
            }

            //Edit WindowsGSM.cfg
            string configFile = Functions.ServerPath.GetServersConfigs(serverData.ServerID, "WindowsGSM.cfg");
            if (File.Exists(configFile))
            {
                string configText = File.ReadAllText(configFile);
                configText = configText.Replace("{{clientport}}", (int.Parse(serverData.ServerPort) - 10).ToString());
                File.WriteAllText(configFile, configText);
            }
        }

    }
}
