using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class ASTRONEER : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ASTRONEER", // WindowsGSM.XXXX
            author = "1stian",
            description = "WindowsGSM plugin for supporting Astroneer Dedicated Server",
            version = "1.0",
            url = "https://github.com/1stian/WindowsGSM.ASTRONEER", // Github repository link (Best practice)
            color = "#34c9eb" // Color Hex
        };


        // - Standard Constructor and properties
        public ASTRONEER(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "728470"; // Game server appId, ASTRONEER is 728470


        // - Game server Fixed variables
        public override string StartPath => @"Astro\Binaries\Win64\AstroServer-Win64-Shipping.exe"; // Game server start path
        public string FullName = "Astroneer Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = false;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = null; // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "7777"; // Default port
        public string QueryPort = "7777"; // Default query port
        public string Defaultmap = "planet"; // Default map name
        public string Maxplayers = "4"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() 
        {
            string configPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"Astro\Saved\Config\WindowsServer");
            if (!Directory.Exists(configPath))
            {
                try
                {
                    Directory.CreateDirectory(configPath);
                } catch
                {
                    Error = "Couldn't create config path!";
                    return;
                }

            }

            string AstroServerSettingsIni = "AstroServerSettings.ini";
            string EngineIni = "Engine.ini";

            if (await DownloadGameServerConfig(AstroServerSettingsIni, Path.Combine(configPath, AstroServerSettingsIni)))
            {
                string configText = File.ReadAllText(configPath + "\\" + AstroServerSettingsIni);
                configText = configText.Replace("{{CPW}}", _serverData.GetRCONPassword());
                configText = configText.Replace("{{IP}}", _serverData.ServerIP);
                configText = configText.Replace("{{SERVER_NAME}}", _serverData.ServerName);
                File.WriteAllText(configPath + "\\" + AstroServerSettingsIni, configText);
            }

            if (await DownloadGameServerConfig(EngineIni, Path.Combine(configPath, EngineIni)))
            {
                string configText = File.ReadAllText(configPath + "\\" + EngineIni);
                configText = configText.Replace("{{PORT}}", _serverData.ServerPort);
                File.WriteAllText(configPath + "\\" + EngineIni, configText);
            }
        }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            QueryPort = Port;
            string param = "-log";
            param += $" {_serverData.ServerParam}";

            string workingDir = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
            string runPath = Path.Combine(workingDir, "Astro\\Binaries\\Win64\\AstroServer-Win64-Shipping.exe");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = runPath,
                    Arguments = param.ToString()
                },
                EnableRaisingEvents = true
            };

            // Start Process
            try
            {
                p.Start();
                return p;
            } catch (Exception e)
            {
                base.Error = e.Message;
                return null; // return null if fail to start
            }
        }


        // - Stop server function
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); });

        // Get ini files
        public static async Task<bool> DownloadGameServerConfig(string fileSource, string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            try
            {
                using (WebClient webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync($"https://raw.githubusercontent.com/1stian/WindowsGSM-Configs/master/Astroneer%20Dedicated%20Server/{fileSource}", filePath);
                }
            } catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Github.DownloadGameServerConfig {e}");
            }

            return File.Exists(filePath);
        }
    }
}
