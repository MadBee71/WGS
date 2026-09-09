using System.IO;
using WGS.Models;

namespace WGS.Games;

public class BannerlordPlugin : GamePluginBase
{
    public override string GameId          => "bannerlord";
    public override string GameName        => "Mount & Blade II: Bannerlord";
    public override string Description     => "⚠ Requires a server token BEFORE starting (in-game 'customserver.gettoken' command, Settings → Server token) — Medieval sandbox RPG/RTS with dedicated multiplayer servers";
    public override string Category        => "Military";
    public override int    SteamAppId      => 1863440;
    public override int    GameStoreAppId  => 261550;
    public override bool   RequiresSteamLogin => true;
    public override string Executable      => @"bin\Win64_Shipping_Server\DedicatedCustomServer.Starter.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 7210;
    public override int    DefaultQueryPort => 7210;
    public override int    DefaultMaxPlayers => 120;

    // Bannerlord requires a server token generated manually inside the actual game client via the
    // "customserver.gettoken" developer console command — there is no way to obtain or automate
    // this from WGS. The user must paste it in here once before the server can start.
    public override string? ValidateBeforeStart(GameServer server)
        => string.IsNullOrWhiteSpace(S(server, "serverToken", ""))
            ? "Mount & Blade II: Bannerlord requires a server token. Run 'customserver.gettoken' in the game's developer console (owning the game), then paste the token in Settings → Server token."
            : null;

    public override Task PreStartAsync(GameServer s)
    {
        var configPath = Path.Combine(s.InstallPath, "Modules", "Native", "wgs_config.txt");
        WriteConfigIfMissing(configPath, BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Bannerlord Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        var admin = S(s, "adminPassword", "");
        return
            $"""
            set_server_name {name}
            set_password {pass}
            set_admin_password {admin}
            set_max_num_players {s.MaxPlayers}
            set_welcome_message Welcome to {name}
            """;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var token   = S(s, "serverToken", "");
        var modules = S(s, "modules", "Native*Multiplayer*DedicatedCustomServerHelper");
        return $"/dedicatedcustomserverconfigfile wgs_config.txt {token} /port {s.ServerPort} _MODULES_*{modules}*_MODULES_";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverToken"]   = "",
        ["adminPassword"] = "",
        ["modules"]       = "Native*Multiplayer*DedicatedCustomServerHelper",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverToken", Label = "Server token", FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Generate in-game with the 'customserver.gettoken' developer console command (requires owning the game). Required — the server will refuse to start without it." },
            new() { Key = "adminPassword", Label = "Admin password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
            new() { Key = "modules", Label = "Module string", FieldType = ConfigFieldType.Text, DefaultValue = "Native*Multiplayer*DedicatedCustomServerHelper",
                    Description = "Asterisk-separated module list. Default is standard Native multiplayer (TDM/Skirmish/Captain rotation)." },
        ]);
        return fields;
    }
}
