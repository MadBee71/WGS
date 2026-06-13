using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Discovers the local router via SSDP and manages UPnP port mappings.
/// Fully self-contained — no external packages.
/// </summary>
public class UPnPService
{
    private string?            _controlUrl;
    private bool               _discovered;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HttpClient    _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    // ── Public API ────────────────────────────────────────────────────────────

public async Task AddPortsForServerAsync(GameServer server)
    {
        await EnsureDiscoveredAsync();
        if (!_discovered || _controlUrl == null) return;

        foreach (var (port, proto) in GetPorts(server))
            await AddMappingAsync(port, proto, $"WGS-{server.DisplayName}");
    }

    public async Task RemovePortsForServerAsync(GameServer server)
    {
        if (!_discovered || _controlUrl == null) return;

        foreach (var (port, proto) in GetPorts(server))
            await RemoveMappingAsync(port, proto);
    }

    /// <summary>Explicit one-shot discovery — can be called from Settings to test.</summary>
    public async Task<bool> DiscoverAsync()
    {
        _discovered = false;
        _controlUrl = null;

        // Try SSDP multicast first
        var location = await SsdpSearchAsync();
        if (location != null)
            _controlUrl = await GetControlUrlAsync(location);

        // Fallback: probe gateway directly on known UPnP ports
        if (_controlUrl == null)
            _controlUrl = await TryDirectDiscoveryAsync();

        _discovered = _controlUrl != null;
        return _discovered;
    }

    // ── Discovery ─────────────────────────────────────────────────────────────

    private async Task EnsureDiscoveredAsync()
    {
        if (_discovered) return;
        await _lock.WaitAsync();
        try   { if (!_discovered) await DiscoverAsync(); }
        finally { _lock.Release(); }
    }

    private static string? GetGatewayIp()
    {
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                foreach (var gw in ni.GetIPProperties().GatewayAddresses)
                    if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                        return gw.Address.ToString();
            }
        }
        catch { }
        return null;
    }

    private async Task<string?> TryDirectDiscoveryAsync()
    {
        var gw = GetGatewayIp();
        if (gw == null) return null;

        int[] ports = [1780, 5000, 49000, 49152, 2869];
        string[] paths = ["/InternetGatewayDevice.xml", "/rootDesc.xml", "/gatedesc.xml"];

        var tasks = (from port in ports
                     from path in paths
                     select TryFetchDescriptionAsync($"http://{gw}:{port}{path}")).ToList();

        var results = await Task.WhenAll(tasks);
        return results.FirstOrDefault(r => r != null);
    }

    private async Task<string?> TryFetchDescriptionAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var xml = await _http.GetStringAsync(url, cts.Token);
            if (xml.Contains("WANIPConnection") || xml.Contains("WANPPPConnection") || xml.Contains("InternetGatewayDevice"))
                return await GetControlUrlAsync(url);
        }
        catch { }
        return null;
    }

    private static async Task<string?> SsdpSearchAsync()
    {
        const string MulticastAddr = "239.255.255.250";
        const int    MulticastPort = 1900;

        string[] targets =
        [
            "urn:schemas-upnp-org:service:WANIPConnection:1",
            "urn:schemas-upnp-org:service:WANIPConnection:2",
            "urn:schemas-upnp-org:service:WANPPPConnection:1",
            "urn:schemas-upnp-org:service:WANPPPConnection:2",
            "urn:schemas-upnp-org:device:InternetGatewayDevice:1",
            "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
            "ssdp:all",
        ];

        foreach (var st in targets)
        {
            var req = "M-SEARCH * HTTP/1.1\r\n" +
                      $"HOST: {MulticastAddr}:{MulticastPort}\r\n" +
                      "MAN: \"ssdp:discover\"\r\n" +
                      "MX: 3\r\n" +
                      $"ST: {st}\r\n\r\n";
            try
            {
                using var udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                udp.Client.ReceiveTimeout = 5000;
                udp.EnableBroadcast = true;
                var bytes = Encoding.ASCII.GetBytes(req);
                var ep    = new IPEndPoint(IPAddress.Parse(MulticastAddr), MulticastPort);
                await udp.SendAsync(bytes, bytes.Length, ep);
                await udp.SendAsync(bytes, bytes.Length, ep); // send twice for reliability

                // Collect responses for up to 5 seconds — router may send multiple
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline)
                {
                    IPEndPoint? remote = null;
                    var result = await Task.Run(() =>
                    {
                        try { return udp.Receive(ref remote); } catch { return null; }
                    });
                    if (result == null) break;

                    var response = Encoding.ASCII.GetString(result);
                    var loc = response.Split('\n')
                        .FirstOrDefault(l => l.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase));
                    if (loc != null)
                        return loc.Split(' ', 2).ElementAtOrDefault(1)?.Trim();
                }
            }
            catch { }
        }
        return null;
    }

    private async Task<string?> GetControlUrlAsync(string location)
    {
        try
        {
            var xml     = await _http.GetStringAsync(location);
            var doc     = XDocument.Parse(xml);
            var baseUri = new Uri(location);
            XNamespace ns = "urn:schemas-upnp-org:device-1-0";

            var controlPath = doc.Descendants(ns + "service")
                .Where(s =>
                {
                    var t = (string?)s.Element(ns + "serviceType") ?? "";
                    return t.Contains("WANIPConnection") || t.Contains("WANPPPConnection");
                })
                .Select(s => (string?)s.Element(ns + "controlURL"))
                .FirstOrDefault(u => u != null);

            if (controlPath == null) return null;

            return controlPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? controlPath
                : $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}{controlPath}";
        }
        catch { return null; }
    }

    // ── Port mappings ─────────────────────────────────────────────────────────

    private static IEnumerable<(int Port, string Proto)> GetPorts(GameServer server)
    {
        var seen = new HashSet<(int, string)>();
        void Add(int p, string proto) { if (p > 0) seen.Add((p, proto)); }

        Add(server.ServerPort,  "UDP");
        Add(server.ServerPort,  "TCP");
        if (server.QueryPort > 0 && server.QueryPort != server.ServerPort)
            Add(server.QueryPort, "UDP");
        if (server.SteamPort > 0 && server.SteamPort != server.ServerPort)
            Add(server.SteamPort, "UDP");

        return seen;
    }

    private async Task AddMappingAsync(int port, string protocol, string description)
    {
        var soap = BuildSoapEnvelope("AddPortMapping",
            $"<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            $"<NewProtocol>{protocol}</NewProtocol>" +
            $"<NewInternalPort>{port}</NewInternalPort>" +
            $"<NewInternalClient>{GetLocalIp()}</NewInternalClient>" +
            $"<NewEnabled>1</NewEnabled>" +
            $"<NewPortMappingDescription>{description}</NewPortMappingDescription>" +
            $"<NewLeaseDuration>0</NewLeaseDuration>");
        await SendSoapAsync("AddPortMapping", soap);
    }

    private async Task RemoveMappingAsync(int port, string protocol)
    {
        var soap = BuildSoapEnvelope("DeletePortMapping",
            $"<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            $"<NewProtocol>{protocol}</NewProtocol>");
        await SendSoapAsync("DeletePortMapping", soap);
    }

    private static string BuildSoapEnvelope(string action, string innerXml) =>
        "<?xml version=\"1.0\"?>" +
        "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
        "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
        "<s:Body>" +
        $"<u:{action} xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
        innerXml +
        $"</u:{action}>" +
        "</s:Body>" +
        "</s:Envelope>";

    private async Task SendSoapAsync(string action, string body)
    {
        try
        {
            var content = new StringContent(body, Encoding.UTF8, "text/xml");
            content.Headers.Remove("Content-Type");
            content.Headers.TryAddWithoutValidation("Content-Type", "text/xml; charset=\"utf-8\"");
            content.Headers.TryAddWithoutValidation("SOAPAction",
                $"\"urn:schemas-upnp-org:service:WANIPConnection:1#{action}\"");
            await _http.PostAsync(_controlUrl, content);
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetLocalIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }
}
