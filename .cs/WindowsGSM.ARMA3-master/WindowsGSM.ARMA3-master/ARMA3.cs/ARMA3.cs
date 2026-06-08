using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class ARMA3 : SteamCMDAgent // SteamCMDAgent is used because ARMA3 relies on SteamCMD for installation and update process
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ARMA3", // WindowsGSM.XXXX
            author = "BattlefieldDuck",
            description = "🧩 WindowsGSM plugin for supporting Arma 3 Dedicated Server",
            version = "1.0",
            url = "https://github.com/BattlefieldDuck/WindowsGSM.ARMA3", // Github repository link (Best practice)
            color = "#9eff99" // Color Hex
        };


        // - Standard Constructor and properties
        public ARMA3(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData; // Store server start metadata, such as start ip, port, start param, etc


        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false; // ARMA3 requires to login steam account to install the server, so loginAnonymous = false
        public override string AppId => "233780"; // Game server appId, ARMA3 is 233780


        // - Game server Fixed variables
        public override string StartPath => "arma3server.exe"; // Game server start path, for ARMA3, it is arma3server.exe
        public string FullName = "Arma 3 Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = false;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "2302"; // Default port
        public string QueryPort = "2303"; // Default query port
        public string Defaultmap = "empty"; // Default map name
        public string Maxplayers = "64"; // Default maxplayers
        public string Additional = "-profiles=ArmaHosts -config=server.cfg"; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() { }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Prepare start parameter
            var param = new StringBuilder();
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port={_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -name=\"{_serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");
 
            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath),
                    Arguments = param.ToString()
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
                base.Error = e.Message;
                return null; // return null if fail to start
            }
        }


        // - Stop server function
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); }); // I believe ARMA3 don't have a proper way to stop the server so just kill it
    }
}
