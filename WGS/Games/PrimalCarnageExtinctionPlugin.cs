using WGS.Models;

namespace WGS.Games;

public class PrimalCarnageExtinctionPlugin : GamePluginBase
{
    public override string GameId          => "primalcarnageextinction";
    public override string GameName        => "Primal Carnage: Extinction";
    public override string Description     => "Humans vs. dinosaurs multiplayer shooter";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 336400; // dedicated server — different from the game's own appid (321360)
    public override int    GameStoreAppId  => 321360;
    public override string Executable      => @"Binaries\Win64\PrimalCarnageServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 25;

    public override string BuildStartArguments(GameServer s)
        => $"FR-Valley?game=PrimalCarnageGame.PCFreeRoamGame?MaxPlayers={s.MaxPlayers}?ServerName=\"{s.ServerName}\"" +
           $"?AdminPassword=\"{s.RconPassword}\"?GamePassword=\"{s.ServerPassword}\"?bIsDedicated=true " +
           $"-seekfreeloadingserver -port={s.ServerPort} -QueryPort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
