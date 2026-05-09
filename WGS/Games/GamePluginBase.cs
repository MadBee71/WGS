using WGS.Models;

namespace WGS.Games;

public abstract class GamePluginBase : IGamePlugin
{
    public abstract string GameId { get; }
    public abstract string GameName { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public abstract int SteamAppId { get; }
    public abstract string Executable { get; }
    public abstract int DefaultPort { get; }
    public abstract int DefaultQueryPort { get; }
    public virtual  int DefaultSteamPort { get; } = 0;
    public abstract int DefaultMaxPlayers { get; }
    public virtual bool   RequiresSteamLogin => false;
    public virtual bool   HasRcon            => false;
    public virtual bool   UseNativeConsole   => false;
    public virtual bool   SupportsOxide      => false;
    public virtual string MinecraftFlavor    => string.Empty;
    public virtual int SteamClientAppId => 0;
    public virtual int GameStoreAppId   => 0; // override in each plugin with the game's store AppID

    public abstract string BuildStartArguments(GameServer server);
    public abstract Dictionary<string, string> GetDefaultSettings();
    public abstract List<ConfigField> GetConfigFields();
    public virtual string? GetStopCommand(GameServer server) => null;
    public virtual Task PreStartAsync(GameServer server) => Task.CompletedTask;

    public override string ToString() => $"{GameName}  ({Category})";

    protected string S(GameServer server, string key, string fallback = "")
        => server.GameSpecificSettings.TryGetValue(key, out var v) ? v : fallback;

    protected List<ConfigField> BaseFields() =>
    [
        new() { Key = "serverName",  Label = "Palvelimen nimi",   FieldType = ConfigFieldType.Text,     DefaultValue = GameName + " Server" },
        new() { Key = "maxPlayers",  Label = "Max pelaajat",      FieldType = ConfigFieldType.Number,   DefaultValue = DefaultMaxPlayers.ToString(), Min = 1, Max = 256 },
        new() { Key = "serverPass",  Label = "Salasana",          FieldType = ConfigFieldType.Password, DefaultValue = "" },
    ];
}
