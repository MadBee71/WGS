using WGS.Models;

namespace WGS.Games;

public class RisingWorldPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "risingworld";
    public override string GameName        => "Rising World";
    public override string Description     => "Open-world survival with building and exploration";
    public override string Category        => "Survival";
    public override int    SteamAppId       => 339010;
    public override int    GameStoreAppId   => 327080;
    public override int    WorkshopAppId   => 339010;

    public string ModTargetDirectory => @"mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string SteamBranch      => "unity";
    public override string Executable       => "RisingWorldServer.exe";
    public override int    DefaultPort      => 4255;
    public override int    DefaultQueryPort => 4254;
    public override int    DefaultMaxPlayers => 16;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics " +
           $"+server_name=\"{s.ServerName}\" +Server_Port={s.ServerPort} " +
           $"+Server_QueryPort={s.QueryPort} +Settings_MaxPlayers={s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["gamemode"] = "default",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "gamemode", Label = "Pelimoodi", FieldType = ConfigFieldType.Dropdown, DefaultValue = "default",
                    Options = ["default","creative","adventure"] },
        ]);
        return fields;
    }
}
