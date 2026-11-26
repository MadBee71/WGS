using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WGS.ServiceHost;

// UseWindowsService() only changes behavior when the process is actually started by the
// Windows Service Control Manager (a real installed service) — run directly (as in every
// smoke test so far), this still behaves like a normal console app. See README section for
// install/uninstall commands (sc.exe create/delete), or Worker.cs for what it actually does.

// Short-lived helper mode (Ctrl+C stop of a game server): the service starts a copy of itself to do the console
// attachment, so the service process itself never attaches to a game's console. See WGS.Services.ConsoleCtrlHelper.
if (args.Length > 0 && args[0] == WGS.Services.ConsoleCtrlHelper.HelperArg)
    return WGS.Services.ConsoleCtrlHelper.RunHelper(args);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "WGS Background Service");

// Same folder ConfigService uses for servers.json/settings.json (exe folder\WGS_Data) — service.log lives next to them.
var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(exeDir, "WGS_Data", "service.log")));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
return 0;
