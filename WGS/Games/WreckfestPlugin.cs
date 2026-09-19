using System.IO;
using WGS.Models;

namespace WGS.Games;

public class WreckfestPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId          => "wreckfest";
    public override string GameName        => "Wreckfest";
    public override string Description     => "Demolition derby and contact racing";
    public override string Category        => "Racing";
    public override int    SteamAppId      => 361580;
    public override int    GameStoreAppId  => 228380;
    public override int    WorkshopAppId   => 228380;

    public string ModTargetDirectory => "data/mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    SteamClientAppId => 0; // don't write steam_appid.txt — Wreckfest manages its own Steam context
    public override string Executable      => "Wreckfest_x64.exe";
    public override int    DefaultPort      => 33540;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultSteamPort => 27015;
    public override int    DefaultMaxPlayers => 24;
    public override bool   RequiresSteamLogin  => true;
    public override bool   UseNativeConsole    => true;
    // Wreckfest has no stdin stop command, RCON, or REST API to shut down through — Stop always
    // falls straight through to a hard kill. A Ctrl+C-forward-to-console mechanism was prototyped
    // and tested 19.9.2026 as a possible fix (see project_wgs_ctrlc_stop_investigation_2026-09-19.md)
    // but proved unreliable in-process — roughly half the test runs crashed the calling .NET
    // process itself instead of only signalling the target, an unacceptable risk for WGS's own
    // long-running process. Not implemented; would need a separate disposable helper process to be
    // safe, which is a bigger change than attempted today.


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-s server_config=server_config.cfg -server_set server_name={s.ServerName} max_players={s.MaxPlayers} game_port={s.ServerPort} query_port={s.QueryPort}";

    public override Task PreStartAsync(GameServer server)
    {
        var cfgPath = Path.Combine(server.InstallPath, "server_config.cfg");

        // Only create config if it doesn't exist — don't overwrite existing working config
        if (!File.Exists(cfgPath))
        {
            var initial = Path.Combine(server.InstallPath, "initial_server_config.cfg");
            if (File.Exists(initial))
                File.Copy(initial, cfgPath);
        }

        return Task.CompletedTask;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
