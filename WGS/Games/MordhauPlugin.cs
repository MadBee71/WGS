using WGS.Models;

namespace WGS.Games;

public class MordhauPlugin : GamePluginBase
{
    public override string GameId          => "mordhau";
    public override string GameName        => "MORDHAU";
    public override string Description     => "Medieval multiplayer melee combat with deep skill mechanics";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 629800;
    public override int    GameStoreAppId  => 629760;
    public override string Executable      => @"Mordhau\Binaries\Win64\MordhauServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 64;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s) =>
        $"MORDHAU -Port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
