using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class Necesse : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Necesse", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting Necesse Dedicated Server",
            version = "1.0.1",
            url = "https://github.com/Raziel7893/WindowsGSM.Necesse", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1169370"; // Game server appId Steam

        // - Standard Constructor and properties
        public Necesse(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "NecesseServer.exe"; // Game server start path
        public override string StartPath => "jre\\bin\\java.exe";
        public string FullName = "Necesse Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        public string JvmArgs = "user_jvm_args.txt";

        // - Game server default values
        public string Port = "14159"; // Default port

        public string Additional = "-nogui -datadir .\\data"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "32"; // Default maxplayers        
        public string QueryPort = "27015"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "YourWorldName"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string configFile = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "data\\cfg\\server.cfg");
            Directory.CreateDirectory(Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "data\\cfg"));

            StringBuilder sb = new StringBuilder();
            sb.Append("SERVER = {\r\n" +
                $"\tport = {serverData.ServerPort}, // [0 - 65535] Server default port\r\n" +
                $"\tslots = {serverData.ServerMaxPlayer}, // [1 - 250] Server default slots\r\n" +
                "\tpassword = , // Leave blank for no password\r\n" +
                "\tmaxClientLatencySeconds = 30,\r\n\tpauseWhenEmpty = true,\r\n" +
                "\tgiveClientsPower = true, // If true, clients will have much more power over what hits them, their position etc\r\n" +
                "\tlogging = true, // If true, will create log files for each server start\r\n" +
                "\tlanguage = en,\r\n" +
                "\tunloadLevelsCooldown = 30, // The number of seconds a level will stay loaded after the last player has left it\r\n" +
                "\tworldBorderSize = -1, // The max distance from spawn players can travel. -1 for no border\r\n" +
                "\tdroppedItemsLifeMinutes = 0, // Minutes that dropped items will stay in the world. 0 or less for indefinite\r\n" +
                "\tunloadSettlements = false, // If the server should unload player settlements or keep them loaded\r\n" +
                "\tmaxSettlementsPerPlayer = -1, // The maximum amount of settlements per player. -1 or less means infinite\r\n" +
                "\tmaxSettlersPerSettlement = -1, // The maximum amount of settlers per settlement. -1 or less means infinite\r\n" +
                "\tjobSearchRange = 100, // The tile search range of settler jobs\r\n" +
                "\tzipSaves = true, // If true, will create new saves uncompressed\r\n" +
                "\tMOTD =  // Message of the day\r\n}");

            File.WriteAllText(configFile, sb.ToString());

            string jvmArgsFile = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, JvmArgs);

            File.WriteAllText(jvmArgsFile,
                "# Note: Not all server panels support this file. You may need to set these options in the panel itself.\r\n" +
                "# Xmx and Xms set the maximum and minimum RAM usage, respectively.\r\n" +
                "# They can take any number, followed by an M (for megabyte) or a G (for gigabyte).\r\n" +
                "# For example, to set the maximum to 3GB: -Xmx3G\r\n" +
                "# To set the minimum to 2.5GB: -Xms2500M\r\n" +
                "# A good default for a modded server is 4GB. Do not allocate excessive amounts of RAM as too much may cause lag or crashes.\r\n" +
                "# Uncomment the next line to set it. To uncomment, remove the # at the beginning of the line.\r\n" +
                "# -Xms4G -Xmx4G");
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipJrePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            string shipGamePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "Server.jar");
            string jvmArgsFile = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, JvmArgs);

            if (!File.Exists(shipJrePath))
            {
                Error = $"{Path.GetFileName(shipJrePath)} not found ({shipJrePath})";
                return null;
            }

            if (!File.Exists(shipGamePath))
            {
                Error = $"{Path.GetFileName(shipGamePath)} not found ({shipGamePath})";
                return null;
            }

            StringBuilder jvmArgs = new StringBuilder();
            if (File.Exists(jvmArgsFile))
            {
                using (var fileStream = File.OpenRead(jvmArgsFile))
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 256))
                {
                    String line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        if (!line.StartsWith("#"))
                        {
                            jvmArgs.Append(line);
                        }
                    }
                }
            }
            //Try gather a password from the gui
            var sb = new StringBuilder();
            sb.Append($"{jvmArgs} -jar Server.jar");
            sb.Append($" -world {serverData.ServerMap} ");
            sb.Append($" -port {serverData.ServerPort} ");
            sb.Append($" -slots {serverData.ServerMaxPlayer} ");
            //sb.Append($" -ip {serverData.ServerIP} "); //just ignore it for now, default should work for 95% of users, avoids misconfig
            sb.Append($" {serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipJrePath,
                    Arguments = sb.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (serverData.EmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                var serverConsole = new ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (serverData.EmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("stop");
                Functions.ServerConsole.SendWaitToMainWindow("{ENTER}");
                p.WaitForExit(2500);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
