// Default RCON player command implementations for common games.
// Plugins override IGamePlugin default interface methods to provide these.
namespace WGS.Games;

internal static class SourceRcon
{
    public const string Family = "source";
    public static string? Kick(string p)                  => $"kickid \"{p}\"";
    public static string? Kick(string p, string reason)   => $"kickid \"{p}\" \"{reason}\"";
    public static string? Ban(string p)                   => $"banid 0 \"{p}\" kick";
    public static string? Ban(string p, string reason)    => $"banid 0 \"{p}\" kick; say \"{reason}\"";
    public static string? Unban(string p)                 => $"removeid \"{p}\"";
    public static string? Players()                       => "status";
}

internal static class RustRcon
{
    public const string Family = "rust";
    public static string? Kick(string p)                  => $"kick \"{p}\" \"Kicked by admin\"";
    public static string? Kick(string p, string reason)   => $"kick \"{p}\" \"{reason}\"";
    public static string? Ban(string p)                   => $"ban \"{p}\" \"Banned by admin\"";
    public static string? Ban(string p, string reason)    => $"ban \"{p}\" \"{reason}\"";
    public static string? Unban(string p)                 => $"unban \"{p}\"";
    public static string? Players()                       => "playerlist";
}

internal static class MinecraftRcon
{
    public const string Family = "minecraft";
    public static string? Kick(string p)                  => $"kick {p}";
    public static string? Kick(string p, string reason)   => $"kick {p} {reason}";
    public static string? Ban(string p)                   => $"ban {p}";
    public static string? Ban(string p, string reason)    => $"ban {p} {reason}";
    public static string? Unban(string p)                 => $"pardon {p}";
    public static string? Players()                       => "list";
}

internal static class ArkRcon
{
    public const string Family = "ark";
    public static string? Kick(string p)                  => $"KickPlayer {p}";
    public static string? Kick(string p, string reason)   => $"KickPlayer {p}";
    public static string? Ban(string p)                   => $"BanPlayer {p}";
    public static string? Ban(string p, string reason)    => $"BanPlayer {p}";
    public static string? Unban(string p)                 => $"UnBanPlayer {p}";
    public static string? Players()                       => "ListPlayers";
}

internal static class UnrealRcon
{
    public const string Family = "unreal";
    public static string? Kick(string p)                  => $"Kick {p}";
    public static string? Kick(string p, string reason)   => $"Kick {p} {reason}";
    public static string? Ban(string p)                   => $"Ban {p}";
    public static string? Ban(string p, string reason)    => $"Ban {p} {reason}";
    public static string? Unban(string p)                 => $"UnBan {p}";
    public static string? Players()                       => "PlayerList";
}
