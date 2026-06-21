using WGS.Models;

namespace WGS.Games;

public class Squad44Plugin : GamePluginBase
{
    public override string GameId          => "squad44";
    public override string GameName        => "Squad 44";
    public override string Description     => "Large-scale WWII teamwork FPS (formerly Post Scriptum)";
    public override string Category        => "Military";
    public override int    SteamAppId      => 746200;
    public override int    GameStoreAppId  => 736220;
    public override string Executable      => @"PostScriptum\Binaries\Win64\PostScriptumServer.exe";
    public override int    DefaultPort      => 7787;
    public override int    DefaultQueryPort => 27165;
    public override int    DefaultMaxPlayers => 80;
    public override bool   HasRcon          => true;

    public override string BuildStartArguments(GameServer s)
        => $"Port={s.ServerPort} QueryPort={s.QueryPort} FIXEDMAXPLAYERS={s.MaxPlayers} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
