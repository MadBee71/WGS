using WGS.Models;

namespace WGS.Games;

public interface IGamePlugin
{
    string GameId { get; }
    string GameName { get; }
    string Description { get; }
    string Category { get; }
    int SteamAppId { get; }
    int SteamClientAppId { get; }  // AppID for steam_appid.txt (0 = same as SteamAppId)
    int GameStoreAppId { get; }    // Game's store AppID for Steam CDN images (may differ from server AppID)
    string Executable { get; }
    int DefaultPort { get; }
    int DefaultQueryPort { get; }
    int DefaultSteamPort { get; }
    int DefaultMaxPlayers { get; }
    bool   RequiresSteamLogin { get; }
    string SteamBranch        { get; }  // beta branch for SteamCMD (empty = default branch)
    bool HasRcon { get; }
    bool UseNativeConsole { get; }

    // Mod manager support
    bool   SupportsOxide   { get; }   // uMod/Oxide compatible
    string MinecraftFlavor { get; }   // "paper" | "forge" | "fabric" | "" = none

    // Workshop & config
    int          WorkshopAppId { get; }  // Steam Workshop app ID (0 = no workshop)
    List<string> ConfigFiles   { get; }  // Relative paths to game config files

    /// <summary>Returns true for known harmless log lines that should be hidden from the console.</summary>
    bool IsNoiseLine(string line);
    string BuildStartArguments(GameServer server);
    Dictionary<string, string> GetDefaultSettings();
    List<ConfigField> GetConfigFields();
    string? GetStopCommand(GameServer server);
    Task PreStartAsync(GameServer server);

    // Player management via RCON (optional — return null if not supported)
    string? GetKickCommand(string playerName);
    string? GetKickCommand(string playerName, string reason);
    string? GetBanCommand(string playerName);
    string? GetBanCommand(string playerName, string reason);
    string? GetUnbanCommand(string playerName);
    string? GetPlayersCommand();

    /// <summary>
    /// Tunniste RCON-vastauksen parserille.
    /// Arvot: "source" | "rust" | "minecraft" | "ark" | "unreal" | ""
    /// </summary>
    string EngineFamily { get; }
}

public class ConfigField
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConfigFieldType FieldType { get; set; } = ConfigFieldType.Text;
    public string DefaultValue { get; set; } = string.Empty;
    public string[]? Options { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
}

public enum ConfigFieldType { Text, Number, Password, Toggle, Dropdown, Slider }
