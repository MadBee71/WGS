using System.Text.Json.Serialization;

namespace WGS.Models;

public class GameServer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GameId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string ServerIp { get; set; } = "0.0.0.0";
    public int ServerPort { get; set; }
    public int QueryPort { get; set; }
    public int SteamPort { get; set; }
    public int RconPort { get; set; }
    public string RconPassword { get; set; } = string.Empty;
    public string ServerPassword { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public int MaxPlayers { get; set; }
    public bool AutoRestart            { get; set; } = false;
    public int  AutoRestartMaxRetries  { get; set; } = 5;    // per 10-min window
    public int  AutoRestartDelaySec    { get; set; } = 10;   // seconds before restart
    public bool AutoUpdate             { get; set; } = false;
    public int  AutoUpdateIntervalMin  { get; set; } = 30;   // minutes between update checks
    public bool AutoStart              { get; set; } = false;
    public bool WakeOnDemand           { get; set; } = false;
    public bool ShutDownWhenEmpty      { get; set; } = false;
    public int  ShutDownIdleMinutes    { get; set; } = 10;
    public bool UpdateOnStart          { get; set; } = false;
    public bool BackupOnStart          { get; set; } = false;
    public bool BackupOnShutdown       { get; set; } = false;
    public bool   DiscordAlertsEnabled    { get; set; } = true;
    /// <summary>Server-specific Discord webhook URL. Falls back to global setting when empty.</summary>
    public string DiscordWebhookUrl       { get; set; } = string.Empty;
    public bool DailyRestartEnabled    { get; set; } = false;
    public TimeSpan DailyRestartTime   { get; set; } = TimeSpan.FromHours(4); // 04:00 default
    public string CustomArgs { get; set; } = string.Empty;
    public string Gslt       { get; set; } = string.Empty;
    public long   CpuAffinityMask  { get; set; } = 0; // 0 = all cores
    public string ProcessPriority  { get; set; } = "Normal";
    public long   MaxRamMb         { get; set; } = 0; // 0 = unlimited
    public bool BackupEnabled      { get; set; } = false;
    public int  BackupRetention    { get; set; } = 5;
    /// <summary>Delete backups older than this many days. 0 = disabled (age-based deletion off).</summary>
    public int  BackupMaxAgeDays   { get; set; } = 0;
    /// <summary>When true, only changed files are backed up after the first full backup.</summary>
    public bool UseIncrementalBackups { get; set; } = false;
    /// <summary>Force a full backup every N backups (bounds how long an incremental chain gets).</summary>
    public int  FullBackupEveryN   { get; set; } = 7;
    /// <summary>
    /// Relative path(s) within InstallPath to backup, separated by semicolons.
    /// If empty, the game default (or full InstallPath) is used.
    /// Example: "savegame" or "Saves;config"
    /// </summary>
    public string BackupSavePath   { get; set; } = string.Empty;
    public bool FirewallAutoManage { get; set; } = true;
    public Dictionary<string, string> GameSpecificSettings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastStarted { get; set; }
    public string GroupId { get; set; } = string.Empty;
    /// <summary>Saved console command shortcuts shown as one-click buttons in the Console tab.</summary>
    public List<QuickCommand> QuickCommands { get; set; } = [];

    /// <summary>
    /// PID of the launched process, persisted to disk so WGS can reattach to a still-running
    /// server after WGS itself was closed and reopened. 0 = not running (or not tracked).
    /// </summary>
    public int RunningPid { get; set; }

    [JsonIgnore]
    public ServerStatus Status { get; set; } = ServerStatus.NotInstalled;

    [JsonIgnore]
    public int CurrentPlayers { get; set; }

    [JsonIgnore]
    public TimeSpan Uptime { get; set; }
}

public class QuickCommand
{
    public string Label   { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}
