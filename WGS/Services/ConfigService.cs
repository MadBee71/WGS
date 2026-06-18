using System.IO;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Services;

public class ConfigService
{
    static readonly string ExeDir =
        Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
        ?? AppContext.BaseDirectory;

    public string AppDataPath { get; }
    public string ServersFile { get; }
    public string SettingsFile { get; }
    public string DefaultInstallRoot { get; set; }
    public string BackupPath  { get; set; }
    public string SteamLogin    { get; set; } = string.Empty;
    public string SteamPassword { get; set; } = string.Empty;
    public bool   WebApiEnabled          { get; set; } = false;
    public int    WebApiPort             { get; set; } = 8765;
    public string WebApiToken            { get; set; } = string.Empty;
    public bool   SlaveMode              { get; set; } = false;
    public string SlaveName              { get; set; } = "This Machine";
    public bool   CrashPredictionDiscord { get; set; } = false;
    /// <summary>When true, skip the per-server CPU/RAM heuristics and only warn when system-wide RAM is critically low.</summary>
    public bool   CrashPredictionLowMemOnly { get; set; } = false;
    /// <summary>Free-RAM percentage below which the low-memory warning fires.</summary>
    public double CrashPredictionLowMemPercent { get; set; } = 5.0;
    public bool   EnableUPnP             { get; set; } = false;
    public string SortMode               { get; set; } = "name-asc";

    /// <summary>True when the Web API must be started — either by user choice or slave mode.</summary>
    public bool WebApiRequired => WebApiEnabled || SlaveMode;

    public ConfigService()
    {
        AppDataPath        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WGS");
        ServersFile        = Path.Combine(AppDataPath, "servers.json");
        SettingsFile       = Path.Combine(AppDataPath, "settings.json");
        DefaultInstallRoot = Path.Combine(ExeDir, "servers");
        BackupPath         = Path.Combine(ExeDir, "backups");
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(DefaultInstallRoot);
        LoadSettings();
        Directory.CreateDirectory(BackupPath);
    }

    private record SettingsData(
        string DefaultInstallRoot,
        string SteamLogin,
        string SteamPasswordEncrypted,
        string BackupPath              = "",
        bool   WebApiEnabled           = false,
        int    WebApiPort              = 8765,
        string WebApiToken             = "",
        bool   SlaveMode               = false,
        string SlaveName               = "This Machine",
        bool   CrashPredictionDiscord  = false,
        bool   EnableUPnP             = false,
        string SortMode               = "name-asc",
        bool   CrashPredictionLowMemOnly = false,
        double CrashPredictionLowMemPercent = 5.0);

    private void LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return;
        try
        {
            var d = JsonConvert.DeserializeObject<SettingsData>(File.ReadAllText(SettingsFile));
            if (d == null) return;
            if (!string.IsNullOrEmpty(d.DefaultInstallRoot) && Directory.Exists(d.DefaultInstallRoot))
                DefaultInstallRoot = d.DefaultInstallRoot;
            if (!string.IsNullOrEmpty(d.BackupPath) && Directory.Exists(d.BackupPath))
                BackupPath = d.BackupPath;
            SteamLogin    = d.SteamLogin;
            SteamPassword = string.IsNullOrEmpty(d.SteamPasswordEncrypted)
                ? string.Empty
                : EncryptionService.Decrypt(d.SteamPasswordEncrypted);
            WebApiEnabled          = d.WebApiEnabled;
            WebApiPort             = d.WebApiPort > 0 ? d.WebApiPort : 8765;
            WebApiToken            = d.WebApiToken;
            SlaveMode              = d.SlaveMode;
            SlaveName              = string.IsNullOrEmpty(d.SlaveName) ? "This Machine" : d.SlaveName;
            CrashPredictionDiscord = d.CrashPredictionDiscord;
            EnableUPnP             = d.EnableUPnP;
            SortMode               = string.IsNullOrEmpty(d.SortMode) ? "name-asc" : d.SortMode;
            CrashPredictionLowMemOnly = d.CrashPredictionLowMemOnly;
            CrashPredictionLowMemPercent = d.CrashPredictionLowMemPercent > 0 ? d.CrashPredictionLowMemPercent : 5.0;
        }
        catch { }
    }

    public void Save()
    {
        var encryptedPassword = string.IsNullOrEmpty(SteamPassword)
            ? string.Empty
            : EncryptionService.Encrypt(SteamPassword);
        var d = new SettingsData(DefaultInstallRoot, SteamLogin, encryptedPassword, BackupPath,
            WebApiEnabled, WebApiPort, WebApiToken, SlaveMode, SlaveName, CrashPredictionDiscord,
            EnableUPnP, SortMode, CrashPredictionLowMemOnly, CrashPredictionLowMemPercent);
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(d, Formatting.Indented));
    }

    public List<GameServer> LoadServers()
    {
        if (!File.Exists(ServersFile)) return [];
        try   { return JsonConvert.DeserializeObject<List<GameServer>>(File.ReadAllText(ServersFile)) ?? []; }
        catch { return []; }
    }

    public void SaveServers(IEnumerable<GameServer> servers)
        => File.WriteAllText(ServersFile, JsonConvert.SerializeObject(servers, Formatting.Indented));
}
