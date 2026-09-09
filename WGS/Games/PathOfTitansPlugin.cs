using System.Diagnostics;
using System.IO;
using System.Net.Http;
using WGS.Models;

namespace WGS.Games;

public class PathOfTitansPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "pathoftitans";
    public override string GameName        => "Path of Titans";
    public override string Description     => "⚠ Requires an Alderon Games auth token BEFORE installing (Settings → Auth token) — Dinosaur MMO installed via Alderon Games' own AlderonGamesCmd tool";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 0;
    public override string Executable      => "PathOfTitansServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 200;

    public override string? ValidateBeforeStart(GameServer server)
        => string.IsNullOrWhiteSpace(S(server, "authToken", ""))
            ? "Path of Titans requires a per-server auth token from your Alderon Games account (PoT hosting token page → Generate new token). Paste it in Settings → Auth token."
            : null;

    // AlderonGamesCmd is Alderon Games' own installer tool (not SteamCMD) — verified download URL
    // and command-line syntax below. The auth token itself must still come from the user's own
    // Alderon Games account; nothing about obtaining one can be automated.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        var token = S(server, "authToken", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            log("[PathOfTitans] No auth token set — generate one on your Alderon Games account and set it in Settings first.");
            return false;
        }

        var toolPath = Path.Combine(server.InstallPath, "AlderonGamesCmd-Win64.exe");
        log("[PathOfTitans] Downloading AlderonGamesCmd...");
        Directory.CreateDirectory(server.InstallPath);
        using (var stream = await _http.GetStreamAsync("https://launcher-cdn.alderongames.com/AlderonGamesCmd-Win64.exe"))
        using (var file = File.Create(toolPath))
            await stream.CopyToAsync(file);

        log("[PathOfTitans] Running AlderonGamesCmd install...");
        var branch = S(server, "betaBranch", "public");
        var args = $"--game path-of-titans --server true --beta-branch {branch} --auth-token {token} --install-dir \"{server.InstallPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = args,
            WorkingDirectory = server.InstallPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) log(e.Data); };

        try { proc.Start(); }
        catch (Exception ex) { log($"[PathOfTitans] Couldn't start AlderonGamesCmd: {ex.Message}"); return false; }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        var completed = await Task.Run(() => proc.WaitForExit(15 * 60 * 1000));
        if (!completed)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            log("[PathOfTitans] Install timed out.");
            return false;
        }
        return proc.ExitCode == 0;
    }

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "PathOfTitans", "Saved", "Config", "WindowsServer", "Game.ini"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Path of Titans Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        return
            $"""
            [/Script/Nomad.NomadGameSession]
            ServerName={name}
            MaxPlayers={s.MaxPlayers}
            ServerPassword={pass}
            """;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var token = S(s, "authToken", "");
        return $"Nomad?Port={s.ServerPort}?QueryPort={s.QueryPort}?AuthToken={token}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["authToken"]   = "",
        ["betaBranch"]  = "public",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "authToken", Label = "Alderon Games auth token", FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Per-server token — generate one on the Alderon Games PoT hosting token page. Required for both install and start." },
            new() { Key = "betaBranch", Label = "Branch", FieldType = ConfigFieldType.Text, DefaultValue = "public" },
        ]);
        return fields;
    }
}
