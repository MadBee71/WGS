using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using WindowsGSM.GameServer.Query;
using WindowsGSM.Installer;

namespace WindowsGSM.Plugins
{
    public class ARMA2 // SteamCMDAgent is not used because ARMA2 relies on SteamCMD but needed base game for installation and update process
    {
        // Standard variables
        public Functions.ServerConfig serverData;
        public string Error { get; set; }
        public string Notice { get; set; }

        // - Plugin Details
        public Functions.Plugin Plugin = new Functions.Plugin
        {
            name = "WindowsGSM.ARMA2", // WindowsGSM.XXXX
            author = "GTVolk",
            description = "🧩 WindowsGSM plugin for supporting Arma 2 Operation Arrowhead Dedicated Server",
            version = "1.0",
            url = "https://github.com/DevVault/WindowsGSM.ARMA2", // Github repository link (Best practice)
            color = "#9eff99" // Color Hex
        };


        // - Standard Constructor and properties
        public ARMA2(Functions.ServerConfig serverData) => this.serverData = serverData;

        // - Settings properties for SteamCMD installer
        public bool loginAnonymous => false; // ARMA2 requires to login steam account to install the server, so loginAnonymous = false
        public string GameId => "33910"; // Game server appId, ARMA2 is 33910
        public string AppId => "33905"; // Game server appId, ARMA2 is 33905


        // - Game server Fixed variables
        public string StartPath => "Arma2Server.exe"; // Game server start path, for ARMA2, it is Arma2Server.exe
        public string FullName = "Arma 2 Dedicated Server"; // Game server FullName
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
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerPort) ? string.Empty : $" -port={serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerName) ? string.Empty : $" -name=\"{serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerParam) ? string.Empty : $" {serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath),
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
                Error = e.Message;
                return null; // return null if fail to start
            }
        }


        // - Stop server function
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); }); // I believe ARMA2 don't have a proper way to stop the server so just kill it

        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p;

            p = await steamCMD.Install(serverData.ServerID, string.Empty, GameId, loginAnonymous: loginAnonymous);
            await Task.Run(() => p.WaitForExit());

            Error = steamCMD.Error;

            if (String.IsNullOrWhiteSpace(Error))
            {
                p = await steamCMD.Install(serverData.ServerID, string.Empty, AppId, loginAnonymous: loginAnonymous);
                Error = steamCMD.Error;
            }

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var steamCMD = new Installer.SteamCMD();
            Process p;
            String error;

            (p, error) = await SteamCMD.UpdateEx(serverData.ServerID, GameId, validate, loginAnonymous: loginAnonymous, custom: custom);
            await Task.Run(() => p.WaitForExit());

            Error = error;

            if (String.IsNullOrWhiteSpace(Error))
            {
                (p, error) = await SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, loginAnonymous: loginAnonymous, custom: custom);
                Error = error;
            }

            return p;
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

        public bool IsInstallValid()
        {
            string installPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            Error = $"Fail to find {installPath}";
            return File.Exists(installPath);
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }
    }
}
