using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.Plugins
{
    public class FabricMC
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.FabricMC", // WindowsGSM.XXXX
            author = "Chroma",
            description = "🧩 WindowsGSM plugin for supporting Minecraft: Fabric Servers",
            version = "1.0",
            url = "https://github.com/chromamods/WindowsGSM.FabricMC", // Github repository link (Best practice)
            color = "#FFEC00" // Color Hex
        };


        // - Standard Constructor and properties
        public FabricMC(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath => "start.bat"; // Game server start path
        public string FullName = "Minecraft: Fabric Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new UT3(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "25565"; // Default port
        public string QueryPort = "25565"; // Default query port
        public string Defaultmap = "world"; // Default map name
        public string Maxplayers = "20"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
			// Create server properties
            var sb = new StringBuilder();
            sb.AppendLine($"motd={_serverData.ServerName}");
            sb.AppendLine($"server-port={_serverData.ServerPort}");
            sb.AppendLine("enable-query=true");
            sb.AppendLine($"query.port={_serverData.ServerQueryPort}");
            sb.AppendLine($"rcon.port={int.Parse(_serverData.ServerPort) + 10}");
            sb.AppendLine($"rcon.password={ _serverData.GetRCONPassword()}");
            File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, "server.properties"), sb.ToString());
			
			// Create the batch file for starting/installing the server.
			var bat = new StringBuilder();
			var javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
			bat.AppendLine($"SET /p Arguments=<launchargs.txt");
			bat.AppendLine($"IF EXIST fabric-server-launch.jar GOTO start_server");
			bat.AppendLine($"GOTO install_server");
			bat.AppendLine($"EXIT");
			bat.AppendLine($":install_server");
			bat.Append(javaPath);			
			bat.AppendLine($" -jar fabric-server-installer.jar server -downloadMinecraft");
			bat.AppendLine($"GOTO start_server");
			bat.AppendLine($":start_server");
			bat.Append(javaPath);
			bat.Append($" %Arguments% -jar fabric-server-launch.jar nogui");
			File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, "start.bat"), bat.ToString());
			
        }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
			// Reads the launch arguments from WindowsGSM and creates the arguments text file
			// Note, leaving the arguments field empty in WindowsGSM will mean the server starts with no arguments.
			var readarg = _serverData.ServerParam;
			var arg = new StringBuilder();
			arg.AppendLine(readarg.ToString());
			File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, "launchargs.txt"), arg.ToString());
			
			
            // Check Java exists
            var javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
            if (javaPath.Length == 0)
            {
                Error = "Java is not installed. Please install Java first, then reinstall the server files.";
                return null;
            }
	

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
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
                Error = e.Message;
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
                    // Send "stop" command to StandardInput stream if EmbedConsole is on
                    p.StandardInput.WriteLine("stop");
                }
                else
                {
                    // Send "stop" command to game server process MainWindow
                    ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "stop");
                }
            });
        }

        // - Install server function
        public async Task<Process> Install()
        {
            // EULA agreement
            var agreedPrompt = await UI.CreateYesNoPromptV1("Agreement to the EULA", "By continuing you are indicating your agreement to the EULA.\n(https://account.mojang.com/documents/minecraft_eula)", "Agree", "Decline");
            if (!agreedPrompt)
            { 
                Error = "Disagree to the EULA";
                return null;
            }

            // Install Java if not installed
            if (!JavaHelper.IsJREInstalled())
            {
                var taskResult = await JavaHelper.DownloadJREToServer(_serverData.ServerID);
                if (!taskResult.installed)
                {
                    Error = taskResult.error;
                    return null;
                }
            }

            // Download the latest Fabric installer to /serverfiles
            try
            {
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync($"https://maven.fabricmc.net/net/fabricmc/fabric-installer/0.10.2/fabric-installer-0.10.2.jar", ServerPath.GetServersServerFiles(_serverData.ServerID, "fabric-server-installer.jar"));
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }

            // Create eula.txt
            var eulaFile = ServerPath.GetServersServerFiles(_serverData.ServerID, "eula.txt");
            File.WriteAllText(eulaFile, "#By changing the setting below to TRUE you are indicating your agreement to our EULA (https://account.mojang.com/documents/minecraft_eula).\neula=true");

            return null;
        }


        // - Update server function
        public async Task<Process> Update()
        {
            // Delete the old fabric files
            var fabricJar = ServerPath.GetServersServerFiles(_serverData.ServerID, "fabric-server-launch.jar");
			var installerJar = ServerPath.GetServersServerFiles(_serverData.ServerID, "fabric-server-installer.jar");
            if (File.Exists(fabricJar))
            {
                File.Delete(fabricJar);
            }
			if (File.Exists(installerJar))
            {
                File.Delete(installerJar);
            }          

            // Download the latest fabric installer to /serverfiles
            try
            {
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync($"https://maven.fabricmc.net/net/fabricmc/fabric-installer/0.10.2/fabric-installer-0.10.2.jar", ServerPath.GetServersServerFiles(_serverData.ServerID, "fabric-server-installer.jar"));
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }

            return null;
        }


        // - Check if the installation is successful
        public bool IsInstallValid()
        {
            // Had to rig this since the install is in two parts
            return true;
        }


        // - Check if the directory contains start.bat for import
        public bool IsImportValid(string path)
        {
            // Check to see if the start.bat file exists
            var exePath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {StartPath}";
            return File.Exists(exePath);
        }


        // - Get Local server version
        public string GetLocalBuild()
        {
			// This is rigged since unable to get the latest fabric version from site.
            return string.Empty;
        }


        // - Get Latest server version
        public async Task<string> GetRemoteBuild()
        {               
			// This is rigged since unable to get the latest fabric version from site.
            return string.Empty;
        }
    }
}
