using System.IO;
using Newtonsoft.Json;
using WGS.Models;
using WGS.Services;

namespace WGS.Services;

public class ConfigService
{
    static readonly string ExeDir =
        Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
        ?? AppContext.BaseDirectory;

    /// <summary>WGS's data folder always lives next to its own exe — one rule, no per-user
    /// %AppData% and no separate %ProgramData% case for the service host. This works
    /// identically whether the exe is run interactively or as a background Windows Service
    /// (a service can read/write next to its own executable regardless of which account runs
    /// it), and it's what makes moving/backing up a whole WGS install a matter of copying one
    /// folder. Property kept named AppDataPath for now — many existing call sites read it —
    /// but it no longer means "the AppData folder."</summary>
    static readonly string DataDir = Path.Combine(ExeDir, "WGS_Data");

    /// <summary>Set to the old %AppData%\WGS path only when a migration actually ran this
    /// startup — App.xaml.cs uses this to decide whether to ask the user, once, whether to
    /// delete the now-unused old copy or keep it. Null on every other run (already migrated,
    /// or a fresh install with nothing to migrate).</summary>
    public string? MigratedFromPath { get; private set; }

    /// <summary>
    /// One-time migration for installs that predate this change: if the old per-user
    /// %AppData%\WGS folder has data and the new next-to-exe folder doesn't exist yet, copy it
    /// over so nothing "disappears" on first launch of the updated version. Copies rather than
    /// moves — the old folder is left alone here regardless (harmless leftover); whether to
    /// delete it is the WGS end user's call, asked once via a dialog when MigratedFromPath is set.
    ///
    /// Copies into a staging folder first and only renames it to the real DataDir on full
    /// success. This matters because Directory.Exists(DataDir) is the ONLY signal used to decide
    /// "already migrated" — if the copy loop threw partway with files landing directly in DataDir
    /// (locked file, permission denied, disk full, a long path), that partial folder would exist
    /// on every future launch too, permanently skipping migration with the rest of the old data
    /// never copied and no dialog ever shown (caught in code review, 9.9.2026).
    /// </summary>
    private void MigrateFromAppDataIfNeeded()
    {
        if (Directory.Exists(DataDir)) return; // already migrated (or a fresh install)
        var oldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WGS");
        if (!Directory.Exists(oldPath)) return; // fresh install, nothing to migrate

        var stagingDir = DataDir + ".migrating";
        try
        {
            if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
            foreach (var file in Directory.EnumerateFiles(oldPath, "*", SearchOption.AllDirectories))
            {
                var rel  = Path.GetRelativePath(oldPath, file);
                var dest = Path.Combine(stagingDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
            Directory.Move(stagingDir, DataDir); // atomic rename on the same volume
            MigratedFromPath = oldPath;
        }
        catch
        {
            // Best-effort — clean up the incomplete staging copy so DataDir stays absent and the
            // next launch retries from scratch instead of silently running on partial data.
            try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true); } catch { }
        }
    }

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
    /// <summary>When true, also warns when overall system CPU usage is critically high.</summary>
    public bool   CrashPredictionHighCpuOnly { get; set; } = false;
    /// <summary>System CPU percentage above which the high-CPU warning fires.</summary>
    public double CrashPredictionHighCpuPercent { get; set; } = 98.0;
    public bool   EnableUPnP             { get; set; } = false;
    public string SortMode               { get; set; } = "name-asc";
    public bool   HasSeenOnboarding      { get; set; } = false;
    public bool   HasSeenNewGamesNotice  { get; set; } = false;
    public bool   OptimizeRamBeforeStart { get; set; } = false;
    public bool   HealthCheckEnabled     { get; set; } = true;
    public int    HealthCheckFailThreshold { get; set; } = 3;   // consecutive failures before action
    public HealthCheckAction HealthCheckAction { get; set; } = HealthCheckAction.Notify;

    /// <summary>When true, ServerViewModel/MainViewModel talk to a background WGS.ServiceHost
    /// over HTTP (RemoteServerBackend) instead of managing servers in-process (LocalServerBackend)
    /// — "service mode", the client half of issue #13 (run regardless of logged-in user). Requires
    /// an app restart to take effect (IServerBackend is a DI singleton chosen at startup).</summary>
    public bool   ServiceModeEnabled { get; set; } = false;
    /// <summary>Base URL of the WGS.ServiceHost this client talks to in service mode. Defaults to
    /// localhost — the common case where the service and this client run on the same machine.</summary>
    public string ServiceModeUrl   { get; set; } = "http://localhost:8766";
    /// <summary>Auth token for the WGS.ServiceHost's WebApiService — must match the token that
    /// service was started with.</summary>
    public string ServiceModeToken { get; set; } = string.Empty;

    /// <summary>True when the Web API must be started — either by user choice or slave mode.</summary>
    public bool WebApiRequired => WebApiEnabled || SlaveMode;

    public ConfigService()
    {
        MigrateFromAppDataIfNeeded();
        AppDataPath        = DataDir;
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
        double CrashPredictionLowMemPercent = 5.0,
        bool   CrashPredictionHighCpuOnly = false,
        double CrashPredictionHighCpuPercent = 98.0,
        bool   HasSeenOnboarding = false,
        bool   HasSeenNewGamesNotice = false,
        bool   OptimizeRamBeforeStart = false,
        bool   HealthCheckEnabled = true,
        int    HealthCheckFailThreshold = 3,
        HealthCheckAction HealthCheckAction = HealthCheckAction.Notify,
        bool   ServiceModeEnabled = false,
        string ServiceModeUrl = "http://localhost:8766",
        string ServiceModeToken = "");

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
            CrashPredictionHighCpuOnly = d.CrashPredictionHighCpuOnly;
            CrashPredictionHighCpuPercent = d.CrashPredictionHighCpuPercent > 0 ? d.CrashPredictionHighCpuPercent : 98.0;
            HasSeenOnboarding = d.HasSeenOnboarding;
            HasSeenNewGamesNotice = d.HasSeenNewGamesNotice;
            OptimizeRamBeforeStart   = d.OptimizeRamBeforeStart;
            HealthCheckEnabled       = d.HealthCheckEnabled;
            HealthCheckFailThreshold = d.HealthCheckFailThreshold > 0 ? d.HealthCheckFailThreshold : 3;
            HealthCheckAction        = d.HealthCheckAction;
            ServiceModeEnabled = d.ServiceModeEnabled;
            ServiceModeUrl     = string.IsNullOrEmpty(d.ServiceModeUrl) ? "http://localhost:8766" : d.ServiceModeUrl;
            ServiceModeToken   = d.ServiceModeToken;
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
            EnableUPnP, SortMode, CrashPredictionLowMemOnly, CrashPredictionLowMemPercent,
            CrashPredictionHighCpuOnly, CrashPredictionHighCpuPercent, HasSeenOnboarding,
            HasSeenNewGamesNotice, OptimizeRamBeforeStart, HealthCheckEnabled, HealthCheckFailThreshold, HealthCheckAction,
            ServiceModeEnabled, ServiceModeUrl, ServiceModeToken);
        WriteAtomic(SettingsFile, JsonConvert.SerializeObject(d, Formatting.Indented));
    }

    public List<GameServer> LoadServers()
    {
        // Primary file missing or empty → try the atomic backup (.bak) left by File.Replace
        if (!File.Exists(ServersFile) || new FileInfo(ServersFile).Length == 0)
        {
            var bak = ServersFile + ".bak";
            if (File.Exists(bak) && new FileInfo(bak).Length > 0)
                try { File.Copy(bak, ServersFile, overwrite: true); } catch { }
        }
        if (!File.Exists(ServersFile)) return [];
        try
        {
            var list = JsonConvert.DeserializeObject<List<GameServer>>(File.ReadAllText(ServersFile));
            return list?.Count > 0 ? list : [];
        }
        catch { return []; }
    }

    public void SaveServers(IEnumerable<GameServer> servers)
        => WriteAtomic(ServersFile, JsonConvert.SerializeObject(servers, Formatting.Indented));

    /// <summary>Deletes the old %AppData%\WGS folder after a successful migration, if the user
    /// chose to via the one-time migration dialog. No-op if there was nothing to migrate.</summary>
    public void DeleteMigratedAppDataFolder()
    {
        if (MigratedFromPath == null) return;
        try { Directory.Delete(MigratedFromPath, recursive: true); } catch { }
        MigratedFromPath = null;
    }

    // Write to a temp file on the same volume then atomically replace — a BSOD mid-write
    // leaves the old file intact instead of producing an empty or corrupt file.
    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        var bak = path + ".bak";
        // File.Replace is a single rename syscall — atomic on NTFS
        if (File.Exists(path))
            File.Replace(tmp, path, bak);
        else
            File.Move(tmp, path);
    }
}
