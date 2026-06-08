using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.Collections.Generic;

namespace WindowsGSM.Plugins
{
    public class CS2 : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.CS2", // WindowsGSM.XXXX
            author = "ohmcodes",
            description = "WindowsGSM plugin for supporting Counter Strike 2 Dedicated Server",
            version = "2.1.1",
            url = "https://github.com/ohmcodes/WindowsGSM.CS2", // Github repository link (Best practice)
            color = "#FFA500" // Color Hex
        };

        // - Standard Constructor and properties
        public CS2(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "730"; /* taken via https://steamdb.info/app/730/info/ */

        // - Game server Fixed variables
        public override string StartPath => @"game\\bin\\win64\\cs2.exe"; // Game server start path
        public string FullName = "Counter Strike 2 Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = false;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string ServerName = "wgsm_cs2_dedicated";
        public string Defaultmap = "de_dust2"; // Original (MapName)
        public string Maxplayers = "10"; // WGSM reads this as string but originally it is number or int (MaxPlayers)
        public string Port = "27015"; // WGSM reads this as string but originally it is number or int
        public string QueryPort = "27016"; // WGSM reads this as string but originally it is number or int (SteamQueryPort)
        public string Additional = "+sv_logfile 1";


        private Dictionary<string, string> configData = new Dictionary<string, string>();


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            // no config yet
            //+servercfgfile server.cfg
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            string param = "-dedicated";

            param += $" +hostname {_serverData.ServerName}";
            param += $" +map {_serverData.ServerMap}";
            param += $" -ip {_serverData.ServerIP}";
            param += $" -port {_serverData.ServerPort}";
            param += $" -maxplayers {_serverData.ServerMaxPlayer}";

            param += $" {_serverData.ServerParam}";
            

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Start Process
            try
            {
                p.Start();

                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }
		
        // - Update server function
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "PackageInfo.bin");
            Error = $"Invalid Path! Fail to find {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
