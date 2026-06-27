using WGS.Models;

namespace WGS.Games;

public class GarrysModPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "garrysmod";
    public override string GameName        => "Garry's Mod";
    public override string Description     => "Sandbox physics game with endless gamemodes";
    public override string Category        => "Other";
    public override int    SteamAppId      => 4020;
    public override int    GameStoreAppId  => 4000;
    public override int    WorkshopAppId   => 4000;

    public string ModTargetDirectory => @"garrysmod\addons";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 24;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;

        public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public override string BuildStartArguments(GameServer s)
    {
        var gamemode = S(s, "gamemode", "sandbox");
        var map      = S(s, "map",      "gm_flatgrass");
        return $"-game garrysmod -console -ip 0.0.0.0 -port {s.ServerPort} " +
               $"+maxplayers {s.MaxPlayers} +map {map} +gamemode {gamemode} " +
               $"+hostname \"{s.ServerName}\" +sv_password \"{s.ServerPassword}\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["gamemode"] = "sandbox",
        ["map"]      = "gm_flatgrass",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "gamemode", Label = "Gamemode",   FieldType = ConfigFieldType.Text, DefaultValue = "sandbox" },
            new() { Key = "map",      Label = "Oletuskartta", FieldType = ConfigFieldType.Text, DefaultValue = "gm_flatgrass" },
        ]);
        return fields;
    }
}
