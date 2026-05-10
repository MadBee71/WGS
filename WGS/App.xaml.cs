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
        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        // Load saved language
        var config = Services.GetRequiredService<ConfigService>();
        LocalizationService.Instance.Load(config);

        // Start Web API if configured
        var webApi = Services.GetRequiredService<WebApiService>();
        if (config.WebApiEnabled && config.WebApiPort > 0)
            webApi.Start(config.WebApiPort, config.WebApiToken);

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
        s.AddSingleton<ServerGroupService>();
        s.AddSingleton<WebApiService>();
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
