using System.Diagnostics;
using WGS.Models;

namespace WGS.Services;

public static class FirewallService
{
    private const string Prefix = "WGS - ";

    public static void AddRules(GameServer server)
    {
        var name = RuleName(server.DisplayName);
        RemoveRules(server); // avoid duplicates

        Netsh($"advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol=UDP localport={server.ServerPort}");
        Netsh($"advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol=TCP localport={server.ServerPort}");

        if (server.QueryPort > 0 && server.QueryPort != server.ServerPort)
            Netsh($"advfirewall firewall add rule name=\"{name} (Query)\" dir=in action=allow protocol=UDP localport={server.QueryPort}");

        if (server.SteamPort > 0 && server.SteamPort != server.ServerPort && server.SteamPort != server.QueryPort)
        {
            Netsh($"advfirewall firewall add rule name=\"{name} (Steam)\" dir=in action=allow protocol=UDP localport={server.SteamPort}");
            Netsh($"advfirewall firewall add rule name=\"{name} (Steam)\" dir=in action=allow protocol=TCP localport={server.SteamPort}");
        }

        if (server.RconPort > 0)
            Netsh($"advfirewall firewall add rule name=\"{name} (RCON)\" dir=in action=allow protocol=TCP localport={server.RconPort}");
    }

    public static void RemoveRules(GameServer server)
    {
        var name = RuleName(server.DisplayName);
        Netsh($"advfirewall firewall delete rule name=\"{name}\"");
        Netsh($"advfirewall firewall delete rule name=\"{name} (Query)\"");
        Netsh($"advfirewall firewall delete rule name=\"{name} (Steam)\"");
        Netsh($"advfirewall firewall delete rule name=\"{name} (RCON)\"");
    }

    private static string RuleName(string displayName)
        => Prefix + string.Concat(displayName.Split('<', '>', '"', '&', '|'));

    private static void Netsh(string args)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("netsh", args)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
            });
            proc?.WaitForExit(3000);
        }
        catch { }
    }
}
