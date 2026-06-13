using System.IO;
using WGS.Models;

namespace WGS.Games;

/// <summary>Shared PreStartAsync for all Minecraft-based plugins.</summary>
public abstract class MinecraftPluginBase : GamePluginBase
{
    public override string  EngineFamily                                     => MinecraftRcon.Family;
    public override string? GetKickCommand(string p)                         => MinecraftRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => MinecraftRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => MinecraftRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => MinecraftRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => MinecraftRcon.Unban(p);
    public override string? GetPlayersCommand()                              => MinecraftRcon.Players();

    public override Task PreStartAsync(GameServer s)
    {
        var propsPath = Path.Combine(s.InstallPath, "server.properties");
        WriteConfigIfMissing(propsPath, BuildServerProperties(s));
        return Task.CompletedTask;
    }

    private string BuildServerProperties(GameServer s)
    {
        var difficulty = S(s, "difficulty", "normal");
        var gamemode   = S(s, "gamemode",   "survival");
        var onlineMode = S(s, "onlineMode", "true");
        var rconPort   = s.RconPort > 0 ? s.RconPort : s.ServerPort + 10;

        return
            $"""
            motd={s.ServerName}
            server-port={s.ServerPort}
            max-players={s.MaxPlayers}
            online-mode={onlineMode}
            difficulty={difficulty}
            gamemode={gamemode}
            enable-rcon=true
            rcon.port={rconPort}
            rcon.password={s.RconPassword}
            enable-query=true
            query.port={s.QueryPort}
            server-ip=
            """;
    }

}
