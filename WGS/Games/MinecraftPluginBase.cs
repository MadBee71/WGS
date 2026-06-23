using System.IO;
using System.Security.Cryptography;
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

    // Java/JVM and Forge/Fabric emit these to stderr on every startup — they are
    // informational noise, not errors. Suppressing them keeps the console red-free so
    // real errors stand out.
    public override bool IsNoiseLine(string line) =>
        line.Contains("sun.misc.Unsafe")                          ||
        line.Contains("A terminally deprecated method")           ||
        line.Contains("A restricted method in")                   ||
        line.Contains("--enable-native-access=")                   ||
        line.Contains("Restricted methods will be blocked")       ||
        line.Contains("Advanced terminal features are not available") ||
        line.Contains("java.lang.System::load has been called")   ||
        line.Contains("Only supported on OSX/BSD")                ||
        line.Contains("Only supported on Linux")                  ||
        line.Contains("io.netty.channel.kqueue.Native")             ||
        line.Contains("io.netty.channel.epoll.Native")            ||
        line.Contains("Please consider reporting this to the maintainers") ||
        line.Contains("SECURE-BOOTSTRAP/")                        ||  // Forge log4j bootstrap stack frames
        line.Contains("at TRANSFORMER/")                          ||  // Forge transformer stack frames
        line.Contains("AppenderLoggingException")                 ||  // wraps the kqueue/epoll noise
        line.Contains("An exception occurred processing Appender DebugFile") ||
        (line.TrimStart().StartsWith("...") && line.Contains("more")) ||
        line.Contains("at java.base/");

    public override Task PreStartAsync(GameServer s)
    {
        var propsPath = Path.Combine(s.InstallPath, "server.properties");
        WriteConfigIfMissing(propsPath, BuildServerProperties(s));
        // Mojang-derived server jars (vanilla/Paper/Spigot/Forge) refuse to start at all without
        // this — without TryCustomInstallAsync ever running (e.g. an already-installed server),
        // this is the safety net.
        MinecraftInstallHelper.WriteEulaIfMissing(s.InstallPath);
        return Task.CompletedTask;
    }

    private string BuildServerProperties(GameServer s)
    {
        var difficulty = S(s, "difficulty", "normal");
        var gamemode   = S(s, "gamemode",   "survival");
        var onlineMode = S(s, "onlineMode", "true");
        var rconPort   = s.RconPort > 0 ? s.RconPort : s.ServerPort + 10;
        // Minecraft silently disables RCON when the password is empty — auto-generate one so
        // WGS can always connect without the user having to configure it manually.
        if (string.IsNullOrWhiteSpace(s.RconPassword))
            s.RconPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLower();

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
