using WGS.Models;

namespace WGS.Games;

public class HumanitZPlugin : GamePluginBase
{
    public override string GameId          => "humanitz";
    public override string GameName        => "HumanitZ";
    public override string Description     => "Open-world zombie survival co-op";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2728330;
    public override string Executable      => @"HumanitZServer\Binaries\Win64\HumanitZServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
