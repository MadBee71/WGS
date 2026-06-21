using WGS.Models;

namespace WGS.Games;

public class ScpSecretLabPlugin : GamePluginBase
{
    public override string GameId          => "scpsecretlab";
    public override string GameName        => "SCP: Secret Laboratory";
    public override string Description     => "Asymmetrical multiplayer horror based on the SCP universe";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 996560;
    public override int    GameStoreAppId  => 700330;
    public override string Executable      => "LocalAdmin.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 20;

    public override string BuildStartArguments(GameServer s) => $"{s.ServerPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
