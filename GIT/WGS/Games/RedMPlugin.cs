using WGS.Models;

namespace WGS.Games;

public class RedMPlugin : GamePluginBase
{
    public override string GameId          => "redm";
    public override string GameName        => "RedM";
    public override string Description     => "Red Dead Redemption 2 multiplayer — download FXServer from cfx.re/redm";
    public override string Category        => "Other";
    public override int    SteamAppId      => 0;   // Not on Steam; install manually from cfx.re/redm
    public override string Executable      => @"server\FXServer.exe";
    public override int    DefaultPort     => 30120;
    public override int    DefaultQueryPort => 30120;
    public override int    DefaultMaxPlayers => 32;

    public override string BuildStartArguments(GameServer s)
        => $"+set citizen_dir \"{s.InstallPath}\\server\\citizen\"";

    public override string? GetStopCommand(GameServer server) => "quit";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
