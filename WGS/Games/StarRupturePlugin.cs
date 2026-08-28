using WGS.Models;

namespace WGS.Games;

public class StarRupturePlugin : GamePluginBase
{
    public override string GameId            => "starrupture";
    public override string GameName          => "StarRupture";
    public override string Description       => "Co-op sci-fi shooter with up to 4 players";
    public override string Category          => "Action";
    public override int    SteamAppId        => 3809400;
    public override string Executable        => @"StarRupture\Binaries\Win64\StarRuptureServerEOS-Win64-Shipping.exe";
    public override int    DefaultPort       => 7777;
    public override int    DefaultQueryPort  => 27015;
    public override int    DefaultMaxPlayers => 4;

    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers}";

    public override Task PreStartAsync(GameServer server) => Task.CompletedTask;

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
