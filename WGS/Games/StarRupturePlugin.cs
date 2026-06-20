using System.IO;
using System.Linq;
using WGS.Models;

namespace WGS.Games;

public class StarRupturePlugin : GamePluginBase
{
    public override string GameId          => "starrupture";
    public override string GameName        => "Star Rupture";
    public override string Description     => "Sci-fi base-building survival game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3809400;
    public override string Executable      => @"StarRupture\Binaries\Win64\StarRuptureServerEOS-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 4;

    public override Task PreStartAsync(GameServer s)
    {
        var passwordPath = Path.Combine(s.InstallPath, "Password.json");
        WriteConfigIfMissing(passwordPath, """{ "Password": "" }""");

        var playerPasswordPath = Path.Combine(s.InstallPath, "PlayerPassword.json");
        WriteConfigIfMissing(playerPasswordPath, """{ "Password": "" }""");

        var sessionName = new string(s.ServerName.Where(char.IsLetterOrDigit).ToArray());
        if (sessionName.Length > 20) sessionName = sessionName[..20];

        var cfgPath = Path.Combine(s.InstallPath, "DSSettings.txt");
        WriteConfigIfMissing(cfgPath,
            $$"""
            {
              "SessionName": "{{sessionName}}",
              "SaveGameInterval": "300",
              "StartNewGame": "true",
              "LoadSavedGame": "false",
              "SaveGameName": "AutoSave0.sav"
            }
            """);
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
        => $"-log -Port={s.ServerPort} -ServerQueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers} " +
           $"-Multihome={s.ServerIp} -ServerName={s.ServerName}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
