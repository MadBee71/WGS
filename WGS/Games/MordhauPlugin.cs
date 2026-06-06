using WGS.Models;

namespace WGS.Games;

public class MordhauPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "mordhau";
    public override string GameName        => "MORDHAU";
    public override string Description     => "Medieval multiplayer melee combat with deep skill mechanics";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 629800;
    public override int    GameStoreAppId  => 629760;
    public override int    WorkshopAppId   => 629800;

    public string ModTargetDirectory => @"Mordhau/Content/Paks";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Mordhau\Binaries\Win64\MordhauServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 64;
    public override bool   HasRcon         => true;

        public override string  EngineFamily                                     => UnrealRcon.Family;
    public override string? GetKickCommand(string p)                         => UnrealRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => UnrealRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => UnrealRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => UnrealRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => UnrealRcon.Unban(p);
    public override string? GetPlayersCommand()                              => UnrealRcon.Players();

    public override string BuildStartArguments(GameServer s) =>
        $"MORDHAU -Port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
