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
    public class Waterfall
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Waterfall", // WindowsGSM.XXXX
            author = "1stian",
            description = "Adds waterfall proxy support for Minecraft: Paper servers.",
            version = "1.1",
            url = "https://github.com/1stian/WindowsGSM.WATERFALL", // Github repository link (Best practice)
            color = "#ffffff" // Color Hex
        };


        // - Standard Constructor and properties
        public Waterfall(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath => "waterfall.jar"; // Game server start path
        public string FullName = "Minecraft: Waterfall"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new UT3(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "25565"; // Default port
        public string QueryPort = "25565"; // Default query port
        public string Defaultmap = "none"; // Default map name
        public string Maxplayers = "50"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string configPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "config.yml");
            if (await DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{motd}}", _serverData.ServerName);
                configText = configText.Replace("{{maxplayers}}", _serverData.ServerMaxPlayer);
                configText = configText.Replace("{{queryport}}", _serverData.ServerQueryPort);
                configText = configText.Replace("{{ip_port}}", _serverData.ServerIP + ":" + _serverData.ServerPort);
                File.WriteAllText(configPath, configText);
            }
        }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Check Java exists
            var javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
            if (javaPath.Length == 0)
            {
                Error = "Java is not installed";
                return null;
            }

            // Prepare start parameter
            var param = new StringBuilder($"{_serverData.ServerParam} -jar {StartPath} nogui");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = javaPath,
                    Arguments = param.ToString(),
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
                    p.StandardInput.WriteLine("end");
                }
                else
                {
                    // Send "stop" command to game server process MainWindow
                    ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "end");
                }
            });
        }


        // - Install server function
        public async Task<Process> Install()
        {
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

            // Try getting the latest version and build
            var build = await GetRemoteBuild();
            if (string.IsNullOrWhiteSpace(build)) { return null; }

            // Download the latest waterfall.jar to /serverfiles
            try
            {
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync($"https://papermc.io/api/v1/waterfall/{build}/download", ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
            return null;
        }


        // - Update server function
        public async Task<Process> Update()
        {
            // Delete the old waterfall.jar
            var waterfallJar = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (File.Exists(waterfallJar))
            {
                if (await Task.Run(() =>
                {
                    try
                    {
                        File.Delete(waterfallJar);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Error = e.Message;
                        return false;
                    }
                }))
                {
                    return null;
                }
            }

            // Try getting the latest version and build
            var build = await GetRemoteBuild();
            if (string.IsNullOrWhiteSpace(build)) { return null; }

            // Download the latest waterfall.jar to /serverfiles
            try
            {
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync($"https://papermc.io/api/v1/waterfall/{build}/download", ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
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
            // Check waterfall.jar exists
            return File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }


        // - Check if the directory contains waterfall.jar for import
        public bool IsImportValid(string path)
        {
            // Check waterfall.jar exists
            var exePath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {StartPath}";
            return File.Exists(exePath);
        }


        // - Get Local server version
        public string GetLocalBuild()
        {
            return "";
        }


        // - Get Latest server version
        public async Task<string> GetRemoteBuild()
        {
            // Get latest version and build at https://papermc.io/api/v1/waterfall
            try
            {
                using (var webClient = new WebClient())
                {
                    var version = JObject.Parse(await webClient.DownloadStringTaskAsync("https://papermc.io/api/v1/waterfall"))["versions"][0].ToString(); // "1.16.1"
                    var build = JObject.Parse(await webClient.DownloadStringTaskAsync($"https://papermc.io/api/v1/waterfall/{version}"))["builds"]["latest"].ToString(); // "133"
                    return $"{version}/{build}";
                }
            }
            catch
            {
                Error = "Fail to get remote version and build";
                return string.Empty;
            }
        }

        // Get config.yml
        public static async Task<bool> DownloadGameServerConfig(string filePath, string gameFullName)
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
                    await webClient.DownloadFileTaskAsync($"https://raw.githubusercontent.com/1stian/WindowsGSM-Configs/master/Minecraft%3A%20Waterfall/config.yml", filePath);
                }
            } catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Github.DownloadGameServerConfig {e}");
            }

            return File.Exists(filePath);
        }
    }
}
