using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WGS.ServiceHost;

// UseWindowsService() only changes behavior when the process is actually started by the
// Windows Service Control Manager (a real installed service) — run directly (as in every
// smoke test so far), this still behaves like a normal console app. See README section for
// install/uninstall commands (sc.exe create/delete), or Worker.cs for what it actually does.

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "WGS Background Service");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
