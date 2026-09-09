using WGS.Models;

namespace WGS.Services;

/// <summary>
/// The seam between ServerViewModel's commands and where server-management logic actually
/// executes. Exactly one implementation is active per server at a time:
///
///   - LocalServerBackend  — calls the existing WGS.csproj Services/* classes directly,
///     in-process. This is today's behavior, unchanged, used in "all-in-one" mode.
///   - RemoteServerBackend — proxies every call over HTTP to a WGS.ServiceHost instance
///     (localhost when in "service mode" on this machine, or a genuinely remote machine).
///
/// Scope note: this interface intentionally does NOT cover every [RelayCommand] in
/// ServerViewModel — only the ones that touch the live server process, files under
/// Server.InstallPath, or service-owned config data (servers.json / templates.json /
/// scheduled_tasks.json / workshop.db). Commands that are pure client-side UI, a direct
/// network protocol call (RCON, A2S, port checks), or an external HTTP API check
/// (CheckForGameUpdateAsync) do not need a backend — they run identically regardless of
/// which mode the app is in and are left calling their services directly in ServerViewModel.
/// </summary>
public interface IServerBackend
{
    // ── Lifecycle ────────────────────────────────────────────────────────────
    // log receives the same user-facing messages the console used to see inline (pre-start
    // backup, etc.) — the caller decides how to render them (AppendLog in ServerViewModel).
    Task StartAsync(GameServer server, IProgress<string>? log = null);
    Task StopAsync(GameServer server, IProgress<string>? log = null);
    Task KillAsync(GameServer server);
    /// <summary>Full install/update flow: stop-if-running → SteamCMD or manual-download/custom
    /// install → plugin PostInstall/PostUpdate → restart-if-was-running. Mirrors
    /// ServerViewModel.InstallAsync (ServerViewModel.cs:630-668) including the FiveM/RedM
    /// build-channel decision — see BuildChannelChoice below for how that's surfaced remotely.</summary>
    Task InstallAsync(GameServer server, IProgress<string>? log = null);
    /// <summary>Result of a running install/update op that needs the user to pick a build
    /// channel (FiveM/RedM today — see ServerViewModel.cs:767-780). Local backend can just show
    /// the dialog synchronously and return the choice; Remote backend must expose this as a
    /// poll-able "awaiting input" state on the service side (service pauses InstallAsync until
    /// SubmitBuildChannelChoice is called) rather than blocking a WPF dialog that can't exist
    /// server-side. This is the one piece of real behavioral redesign in this interface —
    /// everything else is a straight proxy.</summary>
    Task<bool> HasPendingBuildChannelChoiceAsync(string serverId);
    Task<(string recommended, string latest)?> GetBuildChannelOptionsAsync(string serverId);
    Task SubmitBuildChannelChoiceAsync(string serverId, bool useLatest);

    // ── Console (non-RCON path only — RCON itself stays a direct client→game-server socket,
    // it never needs to go through this backend regardless of mode) ───────────────────────────
    Task SendConsoleCommandAsync(string serverId, string command);
    Task<(List<string> lines, List<string> types, int nextOffset)> GetLogAsync(string serverId, int offset);

    // ── Backups ──────────────────────────────────────────────────────────────
    Task<BackupEntry> CreateBackupAsync(GameServer server);
    Task<List<BackupEntry>> GetBackupsAsync(GameServer server);
    Task RestoreBackupAsync(GameServer server, string fileName);
    Task DeleteBackupAsync(GameServer server, string fileName);
    Task<List<BackupEntry>> CleanupBackupsAsync(GameServer server); // returns what was deleted

    // ── Config file editor (files live under Server.InstallPath) ───────────────────────────────
    Task<List<ConfigFileEntry>> GetConfigFilesAsync(GameServer server);
    Task<string> ReadConfigFileAsync(GameServer server, string fileName);
    Task SaveConfigFileAsync(GameServer server, string fileName, string content);
    Task<List<ConfigSnapshot>> GetConfigSnapshotsAsync(GameServer server, string fileName);
    Task RestoreConfigSnapshotAsync(GameServer server, ConfigSnapshot snapshot);

    // ── Config presets ───────────────────────────────────────────────────────
    Task<string?> ApplyPresetAsync(GameServer server, ConfigPreset preset); // returns backup path or null on failure

    // ── Mods (Oxide/Paper/Spigot/Purpur/Fabric/Forge/Vanilla installers) ────────────────────────
    Task InstallModFrameworkAsync(GameServer server, string framework, IProgress<(int pct, string msg)> progress);

    // ── SourceMod plugin manager ─────────────────────────────────────────────
    Task<(List<SourceModPlugin> active, List<SourceModPlugin> disabled)> GetSourceModPluginsAsync(GameServer server);
    Task SetSourceModPluginEnabledAsync(GameServer server, string fileName, bool enabled);

    // ── Steam Workshop ───────────────────────────────────────────────────────
    Task<List<WorkshopMod>> GetWorkshopModsAsync(GameServer server);
    Task<List<WorkshopItem>> SearchWorkshopAsync(GameServer server, string query);
    Task InstallWorkshopItemAsync(GameServer server, ulong itemId, IProgress<(int pct, string msg)>? progress = null);
    Task UninstallWorkshopItemAsync(GameServer server, WorkshopMod mod);
    Task SetWorkshopModEnabledAsync(GameServer server, WorkshopMod mod, bool enabled);
    Task<List<WorkshopMod>> CheckOutdatedModsAsync(GameServer server);
    Task UpdateModsAsync(GameServer server, IEnumerable<WorkshopMod>? onlyThese = null, IProgress<(int pct, string msg)>? progress = null);

    // ── Cleanup ──────────────────────────────────────────────────────────────
    Task<List<string>> CleanupJunkFilesAsync(GameServer server); // returns what was removed

    // ── Templates (server-config templates — templates.json is service-owned data) ─────────────
    Task SaveAsTemplateAsync(GameServer server, string name, string description, string category, List<string> tags);
    Task ApplyTemplateAsync(GameServer server, ServerTemplate template);
    Task<ServerTemplate> CloneTemplateAsync(ServerTemplate template);
    Task DeleteTemplateAsync(ServerTemplate template);

    // ── Scheduled tasks (scheduled_tasks.json is service-owned data) ────────────────────────────
    Task<List<ScheduledTask>> GetScheduledTasksAsync(string serverId);
    Task AddScheduledTaskAsync(ScheduledTask task);
    Task RemoveScheduledTaskAsync(ScheduledTask task);

    // ── Quick commands / log-watch rules (persisted on the GameServer model itself, which the
    // backend owner is the sole writer of servers.json for) ─────────────────────────────────────
    Task AddQuickCommandAsync(GameServer server, QuickCommand qc);
    Task RemoveQuickCommandAsync(GameServer server, QuickCommand qc);
    Task AddLogWatchRuleAsync(GameServer server, Models.LogWatchRule rule);
    Task RemoveLogWatchRuleAsync(GameServer server, Models.LogWatchRule rule);

    // ── Players (server-side player list derived from console log parsing for games with no
    // RCON player-list command — RCON-capable games can query this directly client-side instead) ─
    // Takes the live GameServer (like every other method here) rather than just its id — a
    // caller that already holds the object (ServerViewModel does, every 15s per running server)
    // shouldn't force a fresh servers.json disk read/deserialize just to look it up again.
    Task<List<Models.OnlinePlayer>> GetOnlinePlayersAsync(GameServer server);
}
