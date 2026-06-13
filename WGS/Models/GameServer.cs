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
    public bool UpdateOnStart          { get; set; } = false;
    public bool BackupOnStart          { get; set; } = false;
    public bool DiscordAlertsEnabled   { get; set; } = true;
    public bool DailyRestartEnabled    { get; set; } = false;
    public TimeSpan DailyRestartTime   { get; set; } = TimeSpan.FromHours(4); // 04:00 default
    public string CustomArgs { get; set; } = string.Empty;
    public string Gslt       { get; set; } = string.Empty;
    public long   CpuAffinityMask  { get; set; } = 0; // 0 = all cores
    public string ProcessPriority  { get; set; } = "Normal";
    public bool BackupEnabled      { get; set; } = false;
    public int  BackupRetention    { get; set; } = 5;
    public bool FirewallAutoManage { get; set; } = true;
    public Dictionary<string, string> GameSpecificSettings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastStarted { get; set; }
    public string GroupId { get; set; } = string.Empty;

    [JsonIgnore]
    public ServerStatus Status { get; set; } = ServerStatus.NotInstalled;

    [JsonIgnore]
    public int CurrentPlayers { get; set; }

    [JsonIgnore]
    public TimeSpan Uptime { get; set; }
}
