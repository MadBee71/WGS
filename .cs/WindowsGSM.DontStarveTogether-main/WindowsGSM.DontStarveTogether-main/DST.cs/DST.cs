using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.IO;
using System.Linq;
using System.Net;


namespace WindowsGSM.Plugins
{
    public class DST : SteamCMDAgent // SteamCMDAgent is used because DST relies on SteamCMD for installation and update process
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.DontStarveTogether", // WindowsGSM.XXXX
            author = "Andy",
            description = "🧩 WindowsGSM plugin for supporting Don't Starve Together Dedicated Server",
            version = "1.2",
            url = "https://github.com/Kickbut101/WindowsGSM.DontStarveTogether", // Github repository link (Best practice)
            color = "#800080"
        };


        // - Standard Constructor and properties
        public DST(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData; // Store server start metadata, such as start ip, port, start param, etc


        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "343050"; // Game server appId, DST is 343050


        // - Game server Fixed variables
        public override string StartPath => @"bin/dontstarve_dedicated_server_nullrenderer.exe"; // Game server start path, for DST, it is \bin\dontstarve_dedicated_server_nullrenderer.exe
        public string FullName = "Don't Starve Together"; // Game server FullName
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 3; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "10999"; // Default port (called "server_port") in server.ini
        public string QueryPort = "27016"; // Default query port (Called "master_server_port") in server.ini
        public string Defaultmap = "MyDediServer"; // Default world and shard name
        public string Maxplayers = "10"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() 
        {}


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipWorkingPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID); // c:\windowsgsm\servers\1\serverfiles\
            string shipWorkingBinPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, @"bin/")).ToString(); // c:\windowsgsm\servers\1\serverfiles\bin
            string shipWorkingEXEPathFull = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath)); // c:\windowsgsm\servers\1\serverfiles\ + bin\dontstarve_dedicated_server_nullrenderer.exe

            // Flip the backslashes for forwards slashes. Unsure if this was necessary.
            shipWorkingBinPath = shipWorkingBinPath.Replace(@"\","/");

            // Does \bin\ path exist?
            if(!Directory.Exists(shipWorkingBinPath))
            {
                Error = $"Directory not found - ({shipWorkingBinPath})";
                return null;
            }

            // Does .exe path exist?
            if (!File.Exists(shipWorkingEXEPathFull))
            {
                Error = $"{Path.GetFileName(shipWorkingEXEPathFull)} not found in ({shipWorkingPath})";
                return null;
            }

            // Prepare start parameters
            var param = new StringBuilder();
            param.Append($" -persistent_storage_root \"{shipWorkingBinPath}\"");
            param.Append($" -conf_dir serverdatafolder");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $" -cluster \"{_serverData.ServerMap}\""); 
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $" -shard \"{_serverData.ServerMap}_Shard\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port {_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -steam_master_server_port {_serverData.ServerQueryPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -players {_serverData.ServerMaxPlayer}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");

            // Output the startupcommands used. Helpful for troubleshooting server commands and testing them out - leaving this in because it's helpful af.
            var startupCommandsOutputTxtFile = ServerPath.GetServersServerFiles(_serverData.ServerID, "startupCommandsUsed.log");
            File.WriteAllText(startupCommandsOutputTxtFile, $"{param}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = shipWorkingBinPath,
                    FileName = shipWorkingEXEPathFull,
                    Arguments = param.ToString()
                },
                EnableRaisingEvents = true
            };


            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;

                // Start Process
                try
                {
                    p.Start();
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    return null; // return null if fail to start
                }

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return p;

            }
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
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p.StartInfo.RedirectStandardInput)
                {
                    // Send "save and shutdown" command to StandardInput stream if EmbedConsole is on
                    p.StandardInput.WriteLine("c_shutdown(true)");
                }
                else
                {
                    // Send "save and shutdown" command to game server process MainWindow
                    ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "c_shutdown(true)");
                }
            });
        }
    }
}
