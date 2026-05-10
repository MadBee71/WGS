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
    bool RequiresSteamLogin { get; }
    bool HasRcon { get; }
    bool UseNativeConsole { get; }

    // Mod manager support
    bool   SupportsOxide   { get; }   // uMod/Oxide compatible
    string MinecraftFlavor { get; }   // "paper" | "forge" | "fabric" | "" = none

    // Workshop & config
    int          WorkshopAppId { get; }  // Steam Workshop app ID (0 = no workshop)
    List<string> ConfigFiles   { get; }  // Relative paths to game config files

    string BuildStartArguments(GameServer server);
    Dictionary<string, string> GetDefaultSettings();
    List<ConfigField> GetConfigFields();
    string? GetStopCommand(GameServer server);
    Task PreStartAsync(GameServer server);
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
