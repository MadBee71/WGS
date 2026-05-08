using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WGS.Services;

public record PortCheckResult(int Port, bool IsAvailable, string Message);

public class PortCheckerService
{
    public static PortCheckResult CheckPort(int port, string protocol = "TCP")
    {
        bool inUse = protocol.ToUpper() == "UDP"
            ? IsUdpPortInUse(port)
            : IsTcpPortInUse(port);

        return new PortCheckResult(
            port,
            !inUse,
            inUse ? $"Port {port}/{protocol} is already in use!" : $"Port {port}/{protocol} is free"
        );
    }

    public static List<PortCheckResult> CheckServerPorts(Models.GameServer server)
    {
        var results = new List<PortCheckResult>();
        results.Add(CheckPort(server.ServerPort, "UDP"));
        if (server.QueryPort > 0 && server.QueryPort != server.ServerPort)
            results.Add(CheckPort(server.QueryPort, "UDP"));
        if (server.RconPort > 0)
            results.Add(CheckPort(server.RconPort, "TCP"));
        return results;
    }

    public static async Task<bool> IsPortReachableExternallyAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            // try connecting to ourselves via external IP to test NAT
            var externalIp = await GetExternalIpAsync();
            if (externalIp == null) return false;
            await client.ConnectAsync(externalIp, port).WaitAsync(TimeSpan.FromSeconds(3));
            return client.Connected;
        }
        catch { return false; }
    }

    public static async Task<IPAddress?> GetExternalIpAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            var ip = (await http.GetStringAsync("https://api.ipify.org")).Trim();
            return IPAddress.TryParse(ip, out var addr) ? addr : null;
        }
        catch { return null; }
    }

    private static bool IsTcpPortInUse(int port)
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        return props.GetActiveTcpListeners().Any(ep => ep.Port == port);
    }

    private static bool IsUdpPortInUse(int port)
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        return props.GetActiveUdpListeners().Any(ep => ep.Port == port);
    }
}
