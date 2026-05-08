using CommunityToolkit.Mvvm.ComponentModel;

namespace WGS.Services;

public partial class LocalizationService : ObservableObject
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    // ── Window / Titlebar ──────────────────────────────────────────────
    public string AppTitle           => "Windows Game Server";
    public string Running            => "running";
    public string Total              => "total";

    // ── Sidebar ────────────────────────────────────────────────────────
    public string Servers            => "SERVERS";
    public string AddServer          => "+ Add Server";
    public string BackupAll          => "💾 Backup All";
    public string SettingsBtn        => "⚙ Settings";

    // ── Server header ──────────────────────────────────────────────────
    public string BtnStart           => "▶  Start";
    public string BtnStop            => "■  Stop";
    public string BtnRestart         => "↺";
    public string BtnInstall         => "↓  Install / Update";
    public string Uptime             => "Uptime";

    // ── Status ─────────────────────────────────────────────────────────
    public string StatusRunning      => "Running";
    public string StatusStopped      => "Stopped";
    public string StatusStarting     => "Starting...";
    public string StatusStopping     => "Stopping...";
    public string StatusInstalling   => "Installing...";
    public string StatusUpdating     => "Updating...";
    public string StatusError        => "Error";
    public string StatusNotInstalled => "Not installed";

    // ── Tabs ───────────────────────────────────────────────────────────
    public string TabConsole         => "Console";
    public string TabSettings        => "Settings";
    public string TabBackups         => "Backups";
    public string TabInfo            => "Info";

    // ── Console ────────────────────────────────────────────────────────
    public string ConsolePlaceholder => "Type command...";
    public string ConsoleSend        => "Send";
    public string ConsoleClear       => "Clear";
    public string ConsoleFilter      => "Filter...";
    public string RconConnect        => "RCON: Connect";
    public string RconDisconnect     => "RCON: Disconnect";
    public string RconConnectedTxt   => "Connected";
    public string RconDisconnectedTxt=> "Disconnected";

    // ── Settings tab ───────────────────────────────────────────────────
    public string SettingsTitle      => "Server settings";
    public string SettingsGeneral    => "General";
    public string SettingsAutomation => "Automation";
    public string SettingsFiles      => "Files & Ports";
    public string FieldDisplayName   => "Display name";
    public string FieldServerName    => "Server name (in-game)";
    public string FieldIp            => "IP address";
    public string FieldPort          => "Game port";
    public string FieldQueryPort     => "Query port";
    public string FieldMaxPlayers    => "Max players";
    public string FieldPassword      => "Password";
    public string FieldRconPort      => "RCON port";
    public string FieldRconPassword  => "RCON password";
    public string FieldExtraArgs     => "Extra arguments";
    public string FieldInstallPath   => "Install directory";
    public string CheckAutoRestart   => "Auto-restart after crash";
    public string CheckAutoUpdate    => "Auto-update on startup";
    public string BtnCheckPorts      => "🔍  Check ports";
    public string BtnOpenFolder      => "📁 Open";

    // ── Backups ────────────────────────────────────────────────────────
    public string BackupCreate       => "💾  Create backup";
    public string BackupRestore      => "↩ Restore";
    public string BackupCount        => "backups";

    // ── Info tab ───────────────────────────────────────────────────────
    public string InfoGame           => "Game";
    public string InfoCategory       => "Category";
    public string InfoSteamId        => "Steam App ID";
    public string InfoDefaultPort    => "Default port";
    public string InfoRcon           => "RCON";
    public string InfoDescription    => "Description";

    // ── Add dialog ─────────────────────────────────────────────────────
    public string DialogTitle        => "New game server";
    public string DialogGame         => "Game";
    public string DialogName         => "Server name";
    public string DialogInstall      => "Install folder";
    public string DialogCancel       => "Cancel";
    public string DialogCreate       => "Create server";

    // ── Global settings page ───────────────────────────────────────────
    public string GlobalSettings     => "Settings";
    public string DiscordSection     => "Discord Notifications";
    public string DiscordEnable      => "Enable Discord notifications";
    public string DiscordWebhook     => "Webhook URL";
    public string DiscordTest        => "Test";
    public string DiscordSave        => "Save";
    public string GeneralSection     => "General Settings";
    public string DefaultInstallDir  => "Default install directory";
    public string SteamCmdDir        => "SteamCMD directory";
    public string AboutSection       => "About";

    // ── Empty state ────────────────────────────────────────────────────
    public string EmptyTitle         => "Select a server on the left";
    public string EmptySubtitle      => "or add a new server";

    // ── Installing bar ─────────────────────────────────────────────────
    public string InstallingText     => "Installing / updating...";
    public string InstallDone        => "Installation complete";

    // ── RCON messages ──────────────────────────────────────────────────
    public string RconConnectedMsg    => "Connected.";
    public string RconFailedMsg       => "Connection failed.";
    public string RconDisconnectedMsg => "Disconnected.";

    // ── Backup / Restore messages ──────────────────────────────────────
    public string BackupCreating     => "Creating backup...";
    public string BackupDone         => "Done";
    public string RestoreStopFirst   => "Stop the server before restoring.";
    public string RestoreStarting    => "Restoring";
    public string RestoreDone        => "Restore complete.";

    // ── Port checker messages ──────────────────────────────────────────
    public string PortChecking       => "Checking...";
    public string ExternalIp         => "External IP";

    public void Save(ConfigService config) { }
    public void Load(ConfigService config) { }
}
