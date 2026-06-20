using WGS.Models;

namespace WGS.Games;

public class NightOfTheDeadPlugin : GamePluginBase
{
    public override string GameId          => "nightofthedead";
    public override string GameName        => "Night of the Dead";
    public override string Description     => "Co-op zombie survival and base building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1420710;
    public override string Executable      => @"LF\Binaries\Win64\LFServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"?listen -log -CRASHREPORTS -port={s.ServerPort} -name=\"{s.ServerName}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
