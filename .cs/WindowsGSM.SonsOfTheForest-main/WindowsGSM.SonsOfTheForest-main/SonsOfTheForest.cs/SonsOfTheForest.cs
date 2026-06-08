using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System;
using WindowsGSM.Installer;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.Functions;

namespace WindowsGSM.Plugins
{
    public class SonsOfTheForest : SteamCMDAgent
    {
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.SonsOfTheForest",
            author = "ma-halo",
            description = "🧩 WindowsGSM plugin for Sons Of The Forest",
            version = "0.1",
            url = "https://github.com/ma-halo/WindowsGSM.SonsOfTheForest",
            color = "#5c1504" // Color Hex
        };

        private readonly Functions.ServerConfig _serverData;

        public new string Error;
        public new string Notice;

        public string FullName = "Sons Of The Forest";
        public new string StartPath = "SonsOfTheForestDS.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public dynamic QueryMethod = null;

        public string Defaultmap = string.Empty;
        public string Maxplayers = "8";
        public string Additional = string.Empty;

        public string Port = "8766";
        public string QueryPort = "27016";
        public string BlobSyncPort = "9700";

        public new string AppId = "2465200";

        public SonsOfTheForest(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;

        public async void CreateServerCFG()
        {
            //Create steam_appid.txt
            string txtPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "steam_appid.txt");
            File.WriteAllText(txtPath, "1326470");
        }

        public async Task<Process> Start()
        {
            string exeName = StartPath;
            string workingDir = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
            string userdatapath = $"{workingDir}\\config";
            string exePath = Path.Combine(workingDir, exeName);

            if (!File.Exists(exePath))
            {
                Error = $"{exeName} not found ({exePath})";
                return null;
            }

            string configPath = Path.Combine(userdatapath, "dedicatedserver.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"dedicatedserver.cfg not found ({configPath})";
            }
            string param = String.Empty;
            param += $" -dedicatedserver.IpAddress {_serverData.ServerIP}";
            param += $" -dedicatedserver.ServerName \"{_serverData.ServerName}\"";
            param += $" -dedicatedserver.MaxPlayers {_serverData.ServerMaxPlayer}";
            //param += $" -dedicatedserver.SkipNetworkAccessibilityTest true";
            param += $" -dedicatedserver.LogFilesEnabled true";
            param += $" -dedicatedserver.GamePort {_serverData.ServerPort}";
            param += $" -dedicatedserver.QueryPort {_serverData.ServerQueryPort}";
            param += $" -userdatapath \"{userdatapath}\" {_serverData.ServerParam}";

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = workingDir,
                        FileName = exePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = workingDir,
                        FileName = exePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                p.CloseMainWindow();
                Functions.ServerConsole.SendWaitToMainWindow("{ENTER}");
            });
        }

        public new async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();            

            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId);
            Error = steamCMD.Error;

            return p;
        }

        public new async Task<Process> Update(bool validate = false, string custom = null)
        {
            //custom = "-beta latest_experimental";
			validate = true;
			
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom);
            Error = error;
            return p;
        }

        public new bool IsInstallValid()
        {
            string exeName = StartPath;
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, exeName);

            return File.Exists(exePath);
        }

        public new bool IsImportValid(string path)
        {
            string exeFile = StartPath;
            string exePath = Path.Combine(path, exeFile);

            Error = $"Invalid Path! Fail to find {exeFile}";
            return File.Exists(exePath);
        }

        public new string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public new async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
