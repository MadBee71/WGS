using WGS.Models;

namespace WGS.Games;

public class RunescapeDragonwildsPlugin : GamePluginBase
{
    public override string GameId          => "runescapedragonwilds";
    public override string GameName        => "RuneScape: Dragonwilds";
    public override string Description     => "Survival crafting game set in the RuneScape universe";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 4019830;
    public override int    GameStoreAppId  => 1510330;
    public override string Executable      => @"RSDragonwilds\Binaries\Win64\RSDragonwildsServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -ini:Game:[/Script/Engine.GameSession]:MaxPlayers={s.MaxPlayers} -log -NewConsole";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
