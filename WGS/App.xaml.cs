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

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
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

        WriteLog("=== WGS käynnistyy ===");

        // UI-säikeen poikkeukset
        DispatcherUnhandledException += (_, ex) =>
        {
            WriteLog($"DISPATCHER: {ex.Exception}");
            System.Windows.MessageBox.Show(
                $"Virhe:\n{ex.Exception.Message}\n\nLoki: {logPath}",
                "WGS — virhe", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            ex.Handled = true;
        };

        // Background-säikeiden poikkeukset
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteLog($"APPDOMAIN: {ex.ExceptionObject}");

        // Task-poikkeukset
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

        // Start Web API if configured (includes slave mode via WebApiRequired)
        var webApi = Services.GetRequiredService<WebApiService>();
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

    private static void OnApplicationExit(object sender, System.Windows.ExitEventArgs e)
    {
        try
        {
            var mainVm  = Services.GetRequiredService<MainViewModel>();
            var manager = Services.GetRequiredService<ServerManagerService>();

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
        s.AddSingleton<SteamCmdService>();
        s.AddSingleton<ServerManagerService>();
        s.AddSingleton<BackupService>();
        s.AddSingleton<NotificationService>();
        s.AddSingleton<PerformanceMonitorService>();
        s.AddSingleton<ScheduledTaskService>();
        s.AddSingleton<TrayService>();
        s.AddSingleton<SystemMetricsService>();
        s.AddSingleton<ModManagerService>();
        s.AddSingleton<DiscordBotService>();
        s.AddSingleton<ConfigEditorService>();
        s.AddSingleton<PlayerStatsService>();
        s.AddSingleton<PerfHistoryService>();
        s.AddSingleton<SteamWorkshopService>();
        s.AddSingleton<WorkshopDbService>();
        s.AddSingleton<UPnPService>();
        s.AddSingleton<TemplateService>();
        s.AddSingleton<NetworkMonitorService>();
        s.AddSingleton<UserService>();
        s.AddSingleton<ServerGroupService>();
        s.AddSingleton<WebApiService>();
        s.AddSingleton<RemoteMachineService>();
        s.AddSingleton<CrashPredictionService>();
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
        base.OnExit(e);
    }
}
