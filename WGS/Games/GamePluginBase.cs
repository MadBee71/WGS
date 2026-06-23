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

    /// <summary>For SteamAppId == 0 games that can still be auto-installed from a direct zip download
    /// (e.g. FiveM/RedM's FXServer) — return the current build number and its download URL, or null
    /// if this game truly requires a manual install WGS can't automate.</summary>
    public virtual Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server) => Task.FromResult<(string, string)?>(null);

    /// <summary>For installs that don't fit the "download one zip and extract it" shape — a single
    /// jar download (Paper, vanilla Minecraft), or running a vendor installer/build step (Forge,
    /// Fabric, Spigot's BuildTools). Returns false for any plugin that doesn't need this.</summary>
    public virtual Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log) => Task.FromResult(false);

    /// <summary>Checks whether a newer build is available than what GameSpecificSettings records as
    /// installed. Returns null if this game doesn't support version checking.</summary>
    public virtual Task<string?> CheckForUpdateAsync(GameServer server) => Task.FromResult<string?>(null);

    /// <summary>True if this plugin tracks an installed build number and can check for updates (currently FiveM/RedM).</summary>
    public virtual bool SupportsVersionCheck => false;

    /// <summary>For games with a Recommended/Latest build channel choice (currently FiveM/RedM) —
    /// returns both build numbers so the UI can let the user pick before installing.</summary>
    public virtual Task<(string? Recommended, string? Latest)> GetAvailableBuildsAsync(GameServer server)
        => Task.FromResult<(string?, string?)>((null, null));

    /// <summary>
    /// Reads the build number SteamCMD itself recorded for this install, straight from the app
    /// manifest it always writes (steamapps/appmanifest_&lt;id&gt;.acf, "buildid" field) — works for
    /// every Steam-installed game generically, no per-plugin tracking needed.
    /// </summary>
    public string? GetSteamInstalledBuildId(GameServer server)
    {
        if (SteamAppId <= 0) return null;
        try
        {
            var path = Path.Combine(server.InstallPath, "steamapps", $"appmanifest_{SteamAppId}.acf");
            if (!File.Exists(path)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(
                File.ReadAllText(path), "\"buildid\"\\s*\"(\\d+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch { return null; }
    }

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
    public virtual string? ValidateBeforeStart(GameServer server) => null;

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
