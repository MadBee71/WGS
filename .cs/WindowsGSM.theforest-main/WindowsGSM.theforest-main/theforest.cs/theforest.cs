using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class theforest : SteamCMDAgent // SteamCMDAgent is used because the_forest relies on SteamCMD for installation and update process
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.theforest", // WindowsGSM.XXXX
            author = "Tempus Thales",
            description = "🧩 WindowsGSM plugin for The Forest Dedicated Server",
            version = "1.0",
            url = "https://github.com/tempusthales/WindowsGSM.theforest", // Github repository link (Best practice)
            color = "#0a7a04" // Color Hex
        };


        // - Standard Constructor and properties
        public theforest(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData; // Store server start metadata, such as start ip, port, start param, etc


        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false; // the_forest requires to login steam account to install the server, so loginAnonymous = false
        public override string AppId => "556450"; // Game server appId, the_forest is 556450


        // - Game server Fixed variables
        public override string StartPath => "TheForestDedicatedServer.exe"; // Game server start path, for the_forest, it is TheForestDedicatedServer.exe
        public string FullName = "The Forest Dedicated Server; // Game server FullName
        public bool AllowsEmbedConsole = false;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "27017"; // Default port
        public string CommunicationPort = "8766"; // Steam Communications Port
        public string QueryPort = "27016"; // Default query port
        public string Defaultmap = "empty"; // Default map name
        public string Maxplayers = "64"; // Default maxplayers
        public string Additional = "-profiles=the_forestHosts -config=server.cfg"; // Additional server start parameter


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
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); }); // I believe the_forest doesn't have a proper way to stop the server so just kill it
    }
}
