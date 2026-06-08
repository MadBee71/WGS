using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class Spigot
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Spigot", // WindowsGSM.XXXX
            author = "1stian",
            description = "WindowsGSM plugin that provides support for Minecraft: Spigot Server",
            version = "1.2",
            url = "https://github.com/1stian/WindowsGSM.Spigot", // Github repository link (Best practice)
            color = "#fc7f03" // Color Hex
        };


        // - Standard Constructor and properties
        public Spigot(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath => "spigot.jar"; // Game server start path
        public string FullName = "Minecraft: Spigot Server"; // Game server FullName
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
            var sb = new StringBuilder();
            sb.AppendLine($"motd={_serverData.ServerName}");
            sb.AppendLine($"server-port={_serverData.ServerPort}");
            sb.AppendLine("enable-query=true");
            sb.AppendLine($"query.port={_serverData.ServerQueryPort}");
            sb.AppendLine($"rcon.port={int.Parse(_serverData.ServerPort) + 10}");
            sb.AppendLine($"rcon.password={ _serverData.GetRCONPassword()}");
            File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, "server.properties"), sb.ToString());
        }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Checking for spigot jar in build folder - after update
            if (!File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath)))
            {
                string buildPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "build");

                string partialName = "spigot-";
                DirectoryInfo sd = new DirectoryInfo($"{buildPath}");
                FileInfo[] filesInDir = sd.GetFiles(partialName + "*.*");

                string fileName = string.Empty;
                foreach (FileInfo fFile in filesInDir)
                {
                    fileName = fFile.FullName;
                }

                try
                {
                    File.Copy(Path.Combine(buildPath, fileName), Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID), "spigot.jar"));
                }
                catch (Exception e)
                {
                    Error = $"Couldn't copy spigot.jar to serverfiles folder.. {e}";
                }
            }

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

            // Download the latest BuildTools.jar to /serverfiles/build
            string buildPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "build");
            string batchFile = Path.Combine(buildPath, "gather.bat");
            string buildFile = Path.Combine(buildPath, "build.bat");
            string jarFile = Path.Combine(buildPath, "BuildTools.jar");
            // Creates build path if it's not there
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            if (!File.Exists(batchFile) && !File.Exists(buildFile))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("curl.exe https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar --ssl-no-revoke -o BuildTools.jar");
                File.WriteAllText(batchFile, sb.ToString());

                StringBuilder sb2 = new StringBuilder();
                sb2.Append($"java -jar {jarFile} --rev latest");
                File.WriteAllText(buildFile, sb2.ToString());
            }

            var pr = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = buildPath,
                    FileName = batchFile,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow=true,
                    RedirectStandardInput=true,
                    RedirectStandardOutput=true,
                    RedirectStandardError=true,
                    UseShellExecute = false
                },
            };

            try
            {
                pr.Start();
                pr.WaitForExit();
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }

            // Create eula.txt
            var eulaFile = ServerPath.GetServersServerFiles(_serverData.ServerID, "eula.txt");
            File.WriteAllText(eulaFile, "#By changing the setting below to TRUE you are indicating your agreement to our EULA (https://account.mojang.com/documents/minecraft_eula).\neula=true");

            // Check Java exists
            var javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
            if (javaPath.Length == 0)
            {
                Error = "Java is not installed";
                return null;
            }

            // Compiling spigot.jar
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = buildPath,
                    FileName = buildFile,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow=true,
                    RedirectStandardInput=true,
                    RedirectStandardOutput=true,
                    RedirectStandardError=true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            try
            {
                p.Start();
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
            }

            return null;
        }

        // - Update server function
        public async Task<Process> Update()
        {
            // Download the latest BuildTool.jar to /serverfiles/build
            string buildPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "build");
            string batchFile = Path.Combine(buildPath, "gather.bat");
            string buildFile = Path.Combine(buildPath, "build.bat");
            string jarFile = Path.Combine(buildPath, "BuildTools.jar");

            // Delete the old spigot.jar
            var spigotJar = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (File.Exists(spigotJar))
            {
                if (await Task.Run(() =>
                {
                    try
                    {
                        File.Delete(spigotJar);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Error = e.Message;
                        return false;
                    }
                }))
                {

                }
            }

            var buildtoolJar = ServerPath.GetServersServerFiles(_serverData.ServerID, buildPath);
            if (File.Exists(jarFile))
            {
                if (await Task.Run(() =>
                {
                    try
                    {
                        string partialName = "spigot-";
                        DirectoryInfo sd = new DirectoryInfo($"{buildPath}");
                        FileInfo[] filesInDir = sd.GetFiles(partialName + "*.*");

                        string fileName = string.Empty;
                        foreach (FileInfo fFile in filesInDir)
                        {
                            File.Delete(fFile.FullName);
                        }

                        File.Delete(jarFile);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Error = e.Message;
                        return false;
                    }
                }))
                {

                }
            }

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("curl.exe https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar -o BuildTools.jar");
                File.WriteAllText(batchFile, sb.ToString());
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }

            var pr = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = buildPath,
                    FileName = batchFile,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow=true,
                    RedirectStandardInput=true,
                    RedirectStandardOutput=true,
                    RedirectStandardError=true,
                    UseShellExecute = false
                },
            };

            try
            {
                pr.Start();
                pr.WaitForExit();
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }

            // Compiling spigot.jar
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = buildPath,
                    FileName = buildFile,
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

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


        // - Check if the installation is successful
        public bool IsInstallValid()
        {
            string buildPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "build");

            string partialName = "spigot-";
            DirectoryInfo sd = new DirectoryInfo($"{buildPath}");
            FileInfo[] filesInDir = sd.GetFiles(partialName + "*.*");

            string fileName = string.Empty;
            foreach (FileInfo fFile in filesInDir)
            {
                fileName = fFile.FullName;
            }

            try
            {
                File.Copy(Path.Combine(buildPath, fileName), Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID), "spigot.jar"));
            }
            catch (Exception e)
            {
                Error = $"Couldn't copy spigot.jar to serverfiles folder.. {e}";
            }

            // Check spigot.jar exists
            return File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }


        // - Check if the directory contains spigot.jar for import
        public bool IsImportValid(string path)
        {
            // Check spigot.jar exists
            var exePath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {StartPath}";
            return File.Exists(exePath);
        }


        // - Get Local server version
        public string GetLocalBuild()
        {
            return "1";
        }


        // - Get Latest server version
        public async Task<string> GetRemoteBuild()
        {
            return "2";
        }
    }
}
