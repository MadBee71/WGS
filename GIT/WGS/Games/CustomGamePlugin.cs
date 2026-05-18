using WGS.Models;

namespace WGS.Games;

public class CustomGamePlugin : GamePluginBase
{
    private readonly CustomGameDefinition _def;

    public CustomGamePlugin(CustomGameDefinition def) => _def = def;

    public override string GameId            => _def.GameId;
    public override string GameName          => _def.GameName;
    public override string Description       => _def.Description;
    public override string Category          => _def.Category;
    public override int    SteamAppId        => _def.SteamAppId;
    public override int    GameStoreAppId    => _def.SteamAppId;
    public override string Executable        => _def.Executable;
    public override int    DefaultPort       => _def.DefaultPort;
    public override int    DefaultQueryPort  => _def.DefaultQueryPort;
    public override int    DefaultSteamPort  => _def.DefaultSteamPort;
    public override int    DefaultMaxPlayers => _def.DefaultMaxPlayers;
    public override bool   RequiresSteamLogin => _def.RequiresSteamLogin;
    public override bool   HasRcon           => _def.HasRcon;

    public override string BuildStartArguments(GameServer server)
        => $"-port {server.ServerPort} -maxplayers {server.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => [];

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
