using System.Diagnostics;
using System.IO;
using WGS.Models;

namespace WGS.Games;

public class FactorioPlugin : GamePluginBase
{
    public override string GameId          => "factorio";
    public override string GameName        => "Factorio";
    public override string Description     => "Factory-building and automation game with multiplayer co-op";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 427520;
    public override int    GameStoreAppId  => 427520;
    // Factorio has no separate free dedicated-server depot on Windows (unlike the Linux headless
    // tarball) — the same paid appid is used for both client and server, so anonymous SteamCMD
    // login fails with "No subscription". The user's own Steam account (which owns the game) is
    // required in WGS's Steam login settings.
    public override bool   RequiresSteamLogin => true;
    public override string Executable      => @"bin\x64\factorio.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 34197;
    public override int    DefaultQueryPort => 34197;
    public override int    DefaultMaxPlayers => 32;

    public override async Task PreStartAsync(GameServer s)
    {
        var savePath = Path.Combine(s.InstallPath, "saves", "dedicated.zip");
        if (!File.Exists(savePath))
        {
            Directory.CreateDirectory(Path.Combine(s.InstallPath, "saves"));
            await CreateSaveAsync(s.InstallPath, savePath);
        }

        WriteConfigIfMissing(Path.Combine(s.InstallPath, "server-settings.json"), BuildServerSettings(s));
    }

    private static async Task CreateSaveAsync(string installPath, string savePath)
    {
        var exe = Path.Combine(installPath, @"bin\x64\factorio.exe");
        if (!File.Exists(exe)) return; // not installed yet — PreStartAsync runs again on next start attempt

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--create \"{savePath}\"",
            WorkingDirectory = installPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using var proc = Process.Start(psi);
            if (proc != null)
                await Task.Run(() => proc.WaitForExit(120_000));
        }
        catch { /* save creation failed — server start will surface the real error */ }
    }

    private string BuildServerSettings(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Factorio Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        var isPublic = S(s, "public", "false") == "true" ? "true" : "false";
        return
            $$"""
            {
              "name": "{{name}}",
              "description": "",
              "tags": [],
              "max_players": {{s.MaxPlayers}},
              "visibility": { "public": {{isPublic}}, "lan": true },
              "username": "",
              "password": "{{pass}}",
              "game_password": "{{pass}}",
              "require_user_verification": true,
              "max_upload_in_kilobytes_per_second": 0,
              "max_upload_slots": 5,
              "minimum_latency_in_ticks": 0,
              "ignore_player_limit_for_returning_players": false,
              "allow_commands": "admins-only",
              "autosave_interval": 10,
              "autosave_slots": 5,
              "afk_autokick_interval": 0,
              "auto_pause": false,
              "only_admins_can_pause_the_game": true,
              "autosave_only_on_server": true,
              "non_blocking_saving": false
            }
            """;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var savePath = Path.Combine(s.InstallPath, "saves", "dedicated.zip");
        return $"--start-server \"{savePath}\" --port {s.ServerPort} --server-settings \"{s.InstallPath}\\server-settings.json\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["public"] = "false",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "public", Label = "Public server listing", FieldType = ConfigFieldType.Toggle, DefaultValue = "false",
                    Description = "List this server on Factorio's public server browser (requires a valid Factorio.com account configured in-game)." },
        ]);
        return fields;
    }
}
