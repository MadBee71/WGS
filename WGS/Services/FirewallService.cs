using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Manages Windows Firewall rules via the native COM Firewall API (HNetCfg.FwPolicy2) instead
/// of shelling out to netsh.exe. Spawning netsh.exe to silently add firewall rules is a classic
/// "Impair Defenses" pattern (MITRE ATT&CK T1562.004) that Defender's behavioral heuristics flag,
/// even though the actual purpose here — opening a game server's own ports — is legitimate.
/// </summary>
public static class FirewallService
{
    private const string Prefix = "WGS - ";
    private const int NET_FW_IP_PROTOCOL_TCP = 6;
    private const int NET_FW_IP_PROTOCOL_UDP = 17;
    private const int NET_FW_RULE_DIR_IN     = 1;
    private const int NET_FW_ACTION_ALLOW    = 1;
    private const int NET_FW_PROFILE2_ALL    = 0x7FFFFFFF;

    public static void AddRules(GameServer server)
    {
        var name = RuleName(server.DisplayName);
        RemoveRules(server); // avoid duplicates

        AddRule(name, server.ServerPort, NET_FW_IP_PROTOCOL_UDP);
        AddRule(name, server.ServerPort, NET_FW_IP_PROTOCOL_TCP);

        if (server.QueryPort > 0 && server.QueryPort != server.ServerPort)
            AddRule($"{name} (Query)", server.QueryPort, NET_FW_IP_PROTOCOL_UDP);

        if (server.SteamPort > 0 && server.SteamPort != server.ServerPort && server.SteamPort != server.QueryPort)
        {
            AddRule($"{name} (Steam)", server.SteamPort, NET_FW_IP_PROTOCOL_UDP);
            AddRule($"{name} (Steam)", server.SteamPort, NET_FW_IP_PROTOCOL_TCP);
        }

        if (server.RconPort > 0)
            AddRule($"{name} (RCON)", server.RconPort, NET_FW_IP_PROTOCOL_TCP);
    }

    public static void RemoveRules(GameServer server)
    {
        var name = RuleName(server.DisplayName);
        RemoveRule(name);
        RemoveRule($"{name} (Query)");
        RemoveRule($"{name} (Steam)");
        RemoveRule($"{name} (RCON)");
    }

    private static string RuleName(string displayName)
        => Prefix + string.Concat(displayName.Split('<', '>', '"', '&', '|'));

    private static dynamic? CreatePolicy()
    {
        var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
        return type == null ? null : Activator.CreateInstance(type);
    }

    private static void AddRule(string name, int port, int protocol)
    {
        try
        {
            dynamic? policy = CreatePolicy();
            if (policy == null) return;

            var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (ruleType == null) return;
            dynamic rule = Activator.CreateInstance(ruleType)!;

            rule.Name       = name;
            rule.Protocol   = protocol;
            rule.LocalPorts = port.ToString();
            rule.Direction  = NET_FW_RULE_DIR_IN;
            rule.Action     = NET_FW_ACTION_ALLOW;
            rule.Enabled    = true;
            rule.Profiles   = NET_FW_PROFILE2_ALL;

            policy.Rules.Add(rule);
        }
        catch { }
    }

    private static void RemoveRule(string name)
    {
        try { CreatePolicy()?.Rules.Remove(name); }
        catch { }
    }
}
