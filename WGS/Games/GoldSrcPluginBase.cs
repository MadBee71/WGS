using System.IO;
using WGS.Models;

namespace WGS.Games;

/// <summary>Shared base for all GoldSrc (HLDS) game server plugins.</summary>
public abstract class GoldSrcPluginBase : GamePluginBase
{
    public override int    SteamAppId       => 90;
    public override string Executable       => "hlds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override bool   HasRcon          => true;

    public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    /// <summary>The game directory name inside the HLDS install (e.g. "cstrike").</summary>
    protected abstract string GameDir { get; }

    public override Task PreStartAsync(GameServer s)
    {
        var cfgPath = Path.Combine(s.InstallPath, GameDir, "server.cfg");
        WriteConfigIfMissing(cfgPath, BuildServerCfg(s));
        return Task.CompletedTask;
    }

    private static string BuildServerCfg(GameServer s) =>
        $"""
        hostname "{s.ServerName}"
        sv_password "{s.ServerPassword}"
        rcon_password "{s.RconPassword}"
        mp_timelimit 20
        """;
}
