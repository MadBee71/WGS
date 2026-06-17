using System.IO;
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
    public virtual string SteamBranch        => string.Empty;
    public virtual bool   HasRcon            => false;
    public virtual bool   UseNativeConsole   => false;
    public virtual bool   SupportsOxide      => false;
    public virtual string MinecraftFlavor    => string.Empty;
    public virtual int    WorkshopAppId      => 0;
    public virtual List<string> ConfigFiles  => [];
    public virtual int SteamClientAppId => 0;
    public virtual int GameStoreAppId   => 0; // override in each plugin with the game's store AppID

    /// <summary>Set true in Unity-based game plugins to suppress harmless shader/GPU noise lines.</summary>
    protected virtual bool FilterUnityShaderNoise => false;

    public virtual bool IsNoiseLine(string line)
    {
        if (!FilterUnityShaderNoise) return false;
        return line.Contains("shader is not supported on this GPU") ||
               line.Contains("Shader Unsupported:") ||
               line.Contains("Shader Did you use #pragma only_renderers") ||
               line.Contains("Shader If subshaders removal was intentional") ||
               line.Contains("3D Noise requires higher shader capabilities") ||
               line.Contains("Microsoft Media Foundation video decoding") ||
               line.StartsWith("WARNING: Shader ");
    }
    public abstract string BuildStartArguments(GameServer server);
    public abstract Dictionary<string, string> GetDefaultSettings();
    public abstract List<ConfigField> GetConfigFields();
    public virtual string? GetStopCommand(GameServer server) => null;
    public virtual Task PreStartAsync(GameServer server) => Task.CompletedTask;

    // Player management — override in plugins that support RCON player commands
    public virtual string? GetKickCommand(string playerName)                => null;
    public virtual string? GetKickCommand(string playerName, string reason) => GetKickCommand(playerName);
    public virtual string? GetBanCommand(string playerName)                 => null;
    public virtual string? GetBanCommand(string playerName, string reason)  => GetBanCommand(playerName);
    public virtual string? GetUnbanCommand(string playerName)               => null;
    public virtual string? GetPlayersCommand()                              => null;
    public virtual string  EngineFamily                                     => string.Empty;

    public virtual string? GetBroadcastCommand(string message) => EngineFamily switch
    {
        SourceRcon.Family    => SourceRcon.Broadcast(message),
        RustRcon.Family      => RustRcon.Broadcast(message),
        MinecraftRcon.Family => MinecraftRcon.Broadcast(message),
        ArkRcon.Family       => ArkRcon.Broadcast(message),
        _                    => null, // no known broadcast syntax for this engine family
    };

    public override string ToString() => $"{GameName}  ({Category})";

    /// <summary>Writes content to path only if the file does not yet exist.</summary>
    protected static void WriteConfigIfMissing(string path, string content)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    protected string S(GameServer server, string key, string fallback = "")
        => server.GameSpecificSettings.TryGetValue(key, out var v) ? v : fallback;

    protected List<ConfigField> BaseFields() =>
    [
        new() { Key = "serverName",  Label = "Server name",   FieldType = ConfigFieldType.Text,     DefaultValue = GameName + " Server" },
        new() { Key = "maxPlayers",  Label = "Max players",        FieldType = ConfigFieldType.Number,   DefaultValue = DefaultMaxPlayers.ToString(), Min = 1, Max = 256 },
        new() { Key = "serverPass",  Label = "Password",          FieldType = ConfigFieldType.Password, DefaultValue = "" },
    ];
}
