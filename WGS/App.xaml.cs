using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WGS.Services;
using WGS.ViewModels;
using WGS.Views;

namespace WGS;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private TrayService? _tray;

    // Kept alive for the app's lifetime — running two WGS instances at once leads to port
    // conflicts and duplicate process management of the same servers (reported by a user
    // running multiple copies without realizing it).
    private static System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _singleInstanceMutex = new System.Threading.Mutex(true, "WGS_SingleInstance_Mutex", out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Windows Game Server is already running. Check your system tray.",
                "WGS already running",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        CultureInfo.DefaultThreadCurrentCulture   = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

        // Force clipboard shortcuts to work in all TextBoxes regardless of command routing
        System.Windows.EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
            System.Windows.UIElement.PreviewKeyDownEvent,
            new System.Windows.Input.KeyEventHandler((s, ev) =>
            {
                if (s is not System.Windows.Controls.TextBox tb) return;
                if (ev.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
                    { tb.Paste(); ev.Handled = true; }
                else if (ev.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                    { tb.Copy(); ev.Handled = true; }
                else if (ev.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
                    { tb.Cut(); ev.Handled = true; }
                else if (ev.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
                    { tb.SelectAll(); ev.Handled = true; }
            }));

        base.OnStartup(e);

        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WGS", "crash.log");

        void WriteLog(string msg)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }

        WriteLog("=== WGS starting ===");

        if (SelfUpdateService.CleanupLeftovers())
        {
            System.Windows.MessageBox.Show(
                "The last update couldn't finish — the file swap failed and never completed. " +
                "You're still on the old version. Try again, or update manually from " +
                $"{UpdateCheckerService.ReleasesUrl}",
                "Update did not apply",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        // Custom games created in the Plugin Creator were only ever loaded into the registry
        // right after being saved — never on a fresh startup — so they vanished from every list
        // (Add Server, etc.) the moment WGS was restarted.
        Games.GameRegistry.LoadCustomPlugins(ViewModels.PluginCreatorViewModel.PluginsPath);

        // UI thread exceptions
        DispatcherUnhandledException += (_, ex) =>
        {
            WriteLog($"DISPATCHER: {ex.Exception}");
            System.Windows.MessageBox.Show(
                $"Error:\n{ex.Exception.Message}\n\nLog: {logPath}",
                "WGS — Error", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            ex.Handled = true;
        };

        // Background thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteLog($"APPDOMAIN: {ex.ExceptionObject}");

        // Task exceptions
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            WriteLog($"TASK: {ex.Exception}");
            ex.SetObserved();
        };

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        // Load saved language
        var config = Services.GetRequiredService<ConfigService>();
        LocalizationService.Instance.Load(config);

        // One-time prompt after WGS's data folder was auto-migrated from %AppData% to next to
        // the exe — the old copy is harmless but the user, not WGS, should decide whether to
        // keep it around as a backup or clean it up.
        if (config.MigratedFromPath != null)
        {
            var result = System.Windows.MessageBox.Show(
                $"WGS's data (servers, settings, backups list) has moved to a folder next to the exe.\n\n" +
                $"The old copy at:\n{config.MigratedFromPath}\n\nis no longer used. Delete it now, or keep it as a backup?",
                "WGS data folder moved",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
                config.DeleteMigratedAppDataFolder();
        }

        // Start Web API if configured (includes slave mode via WebApiRequired)
        var webApi = Services.GetRequiredService<WebApiService>();
        webApi.DashboardEnabled = config.WebApiEnabled;
        if (config.WebApiRequired && config.WebApiPort > 0)
            webApi.Start(config.WebApiPort, config.WebApiToken);

        // Start background services after DI is fully built
        Services.GetRequiredService<RemoteMachineService>().Initialize();
        Services.GetRequiredService<CrashPredictionService>().Initialize();

        _tray = Services.GetRequiredService<TrayService>();
        _tray.ShowWindowRequested += () =>
        {
            MainWindow.Show();
            MainWindow.WindowState = System.Windows.WindowState.Normal;
            MainWindow.Activate();
        };
        _tray.ExitRequested += () => Shutdown();

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        // Graceful shutdown: stop all running servers on exit
        Exit += OnApplicationExit;
    }

    /// <summary>Set to true before a self-update restart so OnApplicationExit leaves servers running for reattach.</summary>
    public static bool IsRestartingForUpdate { get; set; }

    private static void OnApplicationExit(object sender, System.Windows.ExitEventArgs e)
    {
        try
        {
            var mainVm  = Services.GetRequiredService<MainViewModel>();
            var manager = Services.GetRequiredService<ServerManagerService>();

            mainVm.Save();

            // When restarting for a self-update, leave servers running so the new
            // WGS instance can reattach to them via RunningPid.
            if (IsRestartingForUpdate) return;

            foreach (var serverVm in mainVm.Servers)
            {
                if (!serverVm.IsRunning) continue;
                try
                {
                    var t = Task.Run(async () => {
                        try { await manager.StopAsync(serverVm.Server); } catch { }
                    });
                    t.Wait(5000);
                }
                catch { }
            }

            // Final safety net: kill anything still alive
            manager.KillAll();
        }
        catch { }
    }

    private static void ConfigureServices(IServiceCollection s)
    {
        s.AddSingleton<ConfigService>();
        // Local ("all-in-one") vs. Remote ("service mode" — talks to a background
        // WGS.ServiceHost over HTTP) is chosen once at startup from Settings. Everything else in
        // the DI graph depends on IServerBackend, not the concrete type, so nothing else needs to
        // change based on this — see SettingsView's "Service mode" toggle (requires app restart).
        s.AddSingleton<IServerBackend>(sp =>
        {
            var config = sp.GetRequiredService<ConfigService>();
            if (config.ServiceModeEnabled && !string.IsNullOrWhiteSpace(config.ServiceModeUrl))
                return new RemoteServerBackend(config.ServiceModeUrl, config.ServiceModeToken);
            return new LocalServerBackend(
                sp.GetRequiredService<ServerManagerService>(),
                sp.GetRequiredService<SteamCmdService>(),
                sp.GetRequiredService<BackupService>(),
                sp.GetRequiredService<NotificationService>(),
                sp.GetRequiredService<ConfigService>(),
                sp.GetRequiredService<ModManagerService>(),
                sp.GetRequiredService<SourceModService>(),
                sp.GetRequiredService<ConfigEditorService>(),
                sp.GetRequiredService<SteamWorkshopService>(),
                sp.GetRequiredService<WorkshopDbService>(),
                sp.GetRequiredService<TemplateService>(),
                sp.GetRequiredService<ScheduledTaskService>(),
                sp.GetRequiredService<ServerHygieneService>(),
                sp.GetRequiredService<ConfigPresetService>());
        });
        s.AddSingleton<SteamCmdService>();
        s.AddSingleton<ServerManagerService>();
        s.AddSingleton<BackupService>();
        s.AddSingleton<NotificationService>();
        s.AddSingleton<PerformanceMonitorService>();
        s.AddSingleton<ScheduledTaskService>();
        s.AddSingleton<TrayService>();
        s.AddSingleton<SystemMetricsService>();
        s.AddSingleton<ModManagerService>();
        s.AddSingleton<SourceModService>();
        s.AddSingleton<DiscordBotService>();
        s.AddSingleton<ConfigEditorService>();
        s.AddSingleton<ConfigPresetService>();
        s.AddSingleton<PlayerStatsService>();
        s.AddSingleton<PerfHistoryService>();
        s.AddSingleton<SteamWorkshopService>();
        s.AddSingleton<WorkshopDbService>();
        s.AddSingleton<UPnPService>();
        s.AddSingleton<TemplateService>();
        s.AddSingleton<NetworkMonitorService>();
        s.AddSingleton<UserService>();
        s.AddSingleton<ServerGroupService>();
        s.AddSingleton<GroupBanListService>();
        s.AddSingleton<ServerHygieneService>();
        s.AddSingleton<WebApiService>();
        s.AddSingleton<RemoteMachineService>();
        s.AddSingleton<CrashPredictionService>();
        s.AddSingleton<LogWatcherService>();
        s.AddSingleton<ServerHealthService>();
        s.AddSingleton<WakeOnDemandService>();
        s.AddSingleton<MainViewModel>();
        s.AddSingleton<SettingsViewModel>();
        s.AddSingleton<DashboardViewModel>(sp =>
        {
            var main = sp.GetRequiredService<MainViewModel>();
            return main.Dashboard;
        });
        s.AddTransient<PluginCreatorViewModel>();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _tray?.Dispose();
        (Services as IDisposable)?.Dispose();
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
