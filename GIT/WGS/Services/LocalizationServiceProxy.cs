namespace WGS.Services;

/// <summary>
/// XAML resource proxy — wraps the singleton so App.xaml can expose it as {StaticResource Loc}.
/// All property access is forwarded to LocalizationService.Instance.
/// </summary>
public class LocalizationServiceProxy
{
    public static LocalizationService L => LocalizationService.Instance;

    // Forward every property so XAML can bind {Binding Source={StaticResource Loc}, Path=BtnStart}
    public string AppTitle           => L.AppTitle;
    public string Running            => L.Running;
    public string Total              => L.Total;
    public string Servers            => L.Servers;
    public string AddServer          => L.AddServer;
    public string BackupAll          => L.BackupAll;
    public string SettingsBtn        => L.SettingsBtn;
    public string BtnStart           => L.BtnStart;
    public string BtnStop            => L.BtnStop;
    public string BtnRestart         => L.BtnRestart;
    public string BtnInstall         => L.BtnInstall;
    public string Uptime             => L.Uptime;
    public string TabConsole         => L.TabConsole;
    public string TabSettings        => L.TabSettings;
    public string TabBackups         => L.TabBackups;
    public string TabInfo            => L.TabInfo;
    public string ConsoleSend        => L.ConsoleSend;
    public string ConsoleClear       => L.ConsoleClear;
    public string ConsoleFilter      => L.ConsoleFilter;
    public string RconConnect        => L.RconConnect;
    public string RconDisconnect     => L.RconDisconnect;
    public string DialogTitle        => L.DialogTitle;
    public string DialogGame         => L.DialogGame;
    public string DialogName         => L.DialogName;
    public string DialogInstall      => L.DialogInstall;
    public string DialogCancel       => L.DialogCancel;
    public string DialogCreate       => L.DialogCreate;
    public string BackupCreate       => L.BackupCreate;
    public string BackupRestore      => L.BackupRestore;
    public string GlobalSettings     => L.GlobalSettings;
    public string DiscordSection     => L.DiscordSection;
    public string DiscordEnable      => L.DiscordEnable;
    public string DiscordWebhook     => L.DiscordWebhook;
    public string DiscordTest        => L.DiscordTest;
    public string DiscordSave        => L.DiscordSave;
    public string GeneralSection     => L.GeneralSection;
    public string DefaultInstallDir  => L.DefaultInstallDir;
    public string SteamCmdDir        => L.SteamCmdDir;
    public string AboutSection       => L.AboutSection;
    public string EmptyTitle         => L.EmptyTitle;
    public string EmptySubtitle      => L.EmptySubtitle;
    public string InstallingText     => L.InstallingText;
    public string FieldDisplayName   => L.FieldDisplayName;
    public string FieldServerName    => L.FieldServerName;
    public string FieldIp            => L.FieldIp;
    public string FieldPort          => L.FieldPort;
    public string FieldQueryPort     => L.FieldQueryPort;
    public string FieldMaxPlayers    => L.FieldMaxPlayers;
    public string FieldPassword      => L.FieldPassword;
    public string FieldRconPort      => L.FieldRconPort;
    public string FieldRconPassword  => L.FieldRconPassword;
    public string FieldExtraArgs     => L.FieldExtraArgs;
    public string FieldInstallPath   => L.FieldInstallPath;
    public string CheckAutoRestart   => L.CheckAutoRestart;
    public string CheckAutoUpdate    => L.CheckAutoUpdate;
    public string BtnCheckPorts      => L.BtnCheckPorts;
    public string BtnOpenFolder      => L.BtnOpenFolder;
    public string InfoGame           => L.InfoGame;
    public string InfoCategory       => L.InfoCategory;
    public string InfoSteamId        => L.InfoSteamId;
    public string InfoDefaultPort    => L.InfoDefaultPort;
    public string InfoRcon           => L.InfoRcon;
    public string InfoDescription    => L.InfoDescription;
    public string SettingsTitle      => L.SettingsTitle;
    public string SettingsGeneral    => L.SettingsGeneral;
    public string SettingsAutomation => L.SettingsAutomation;
    public string SettingsFiles      => L.SettingsFiles;
    public string InstallDone        => L.InstallDone;
    public string RconConnectedMsg    => L.RconConnectedMsg;
    public string RconFailedMsg       => L.RconFailedMsg;
    public string RconDisconnectedMsg => L.RconDisconnectedMsg;
    public string BackupCreating     => L.BackupCreating;
    public string BackupDone         => L.BackupDone;
    public string RestoreStopFirst   => L.RestoreStopFirst;
    public string RestoreStarting    => L.RestoreStarting;
    public string RestoreDone        => L.RestoreDone;
    public string PortChecking       => L.PortChecking;
    public string ExternalIp         => L.ExternalIp;
}
