/*
Copyright 2025 estvn_ca

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the “Software”), to deal in the Software without
restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/


using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.Plugins
{
    public class PurpurMC
    {
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.PurpurMC",
            author = "estvn-ca",
            description = "WindowsGSM plugin for Minecraft: Purpur Server",
            version = "1.0",
            url = "",
            color = "#ff00cc"
        };

        private readonly ServerConfig _serverData;
        public PurpurMC(ServerConfig serverData) => _serverData = serverData;

        public string Error, Notice;
        public string StartPath => FindJar();
        public string FullName = "Minecraft: Purpur Server";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public object QueryMethod = new UT3();
        public string Port = "25565";
        public string QueryPort = "25565";
        public string Defaultmap = "world";
        public string Maxplayers = "50";  // Preconfigured for 50 players
        public string Additional = $"-Xms8G -Xmx8G";  // Preconfigured 8 GB RAM

        private string FindJar()
        {
            var dir = ServerPath.GetServersServerFiles(_serverData.ServerID);
            var jar = Directory.GetFiles(dir, "purpur*.jar");
            return jar.Length > 0 ? Path.GetFileName(jar[0]) : "purpur.jar";
        }

        public Task CreateServerCFG()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"motd={_serverData.ServerName}");
            sb.AppendLine($"server-port={_serverData.ServerPort}");
            sb.AppendLine("enable-query=true");
            sb.AppendLine($"query.port={_serverData.ServerQueryPort}");
            sb.AppendLine($"rcon.port={int.Parse(_serverData.ServerPort) + 10}");
            sb.AppendLine($"rcon.password={_serverData.GetRCONPassword()}");
            File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, "server.properties"), sb.ToString());
            return Task.CompletedTask;
        }

        public Task<Process> Start()
        {
            var javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
            if (javaPath.Length == 0) { Error = "Java not found"; return Task.FromResult<Process>(null); }

            var param = $"{_serverData.ServerParam} -jar {StartPath} nogui";
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = javaPath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var c = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += c.AddOutput;
                p.ErrorDataReceived += c.AddOutput;
                try { p.Start(); } catch (Exception e) { Error = e.Message; return Task.FromResult<Process>(null); }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return Task.FromResult(p);
            }

            try { p.Start(); return Task.FromResult(p); } catch (Exception e) { Error = e.Message; return Task.FromResult<Process>(null); }
        }

        public Task Stop(Process p)
        {
            if (p == null) return Task.CompletedTask;
            if (p.StartInfo.RedirectStandardInput) p.StandardInput.WriteLine("stop");
            else ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "stop");
            return Task.CompletedTask;
        }

        public async Task<Process> Install()
        {
            var agree = await UI.CreateYesNoPromptV1("EULA", "Agree to Mojang EULA?", "Agree", "Decline");
            if (!agree) { Error = "EULA declined"; return null; }
            if (!JavaHelper.IsJREInstalled())
            {
                var r = await JavaHelper.DownloadJREToServer(_serverData.ServerID);
                if (!r.installed) { Error = r.error; return null; }
            }

            string downloadUrl = await GetRemoteBuild();
            if (string.IsNullOrWhiteSpace(downloadUrl)) { Error = "Invalid remote build"; return null; }

            try
            {
                using (var w = new WebClient())
                {
                    await w.DownloadFileTaskAsync(downloadUrl, ServerPath.GetServersServerFiles(_serverData.ServerID, Path.GetFileName(downloadUrl)));
                }
            }
            catch (Exception e) { Error = e.Message; return null; }

            var eula = ServerPath.GetServersServerFiles(_serverData.ServerID, "eula.txt");
            File.WriteAllText(eula, "eula=true");

            return null;
        }

        public async Task<Process> Update()
        {
            var jar = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (File.Exists(jar)) File.Delete(jar);
            return await Install();
        }

        public bool IsInstallValid() => File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        public bool IsImportValid(string path) => Directory.GetFiles(path, "purpur*.jar").Length > 0;
        public string GetLocalBuild() => "";

        public async Task<string> GetRemoteBuild()
        {
            try
            {
                using (var w = new WebClient())
                {
                    var j = JObject.Parse(await w.DownloadStringTaskAsync("https://api.purpurmc.org/v2/purpur"));
                    var v = j["versions"].Last.ToString();
                    var b = JObject.Parse(await w.DownloadStringTaskAsync($"https://api.purpurmc.org/v2/purpur/versions/{v}"))["builds"].Last.ToString();
                    return $"https://api.purpurmc.org/v2/purpur/versions/{v}/builds/{b}/downloads/purpur-{v}-{b}.jar";
                }
            }
            catch { return ""; }
        }
    }
}
