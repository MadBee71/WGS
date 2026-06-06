using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace WGS.Services;

public enum PortMappingProtocol { TCP, UDP }

public class UPnPService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private string? _controlUrl;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action<string>? Log;

    // ── Discovery ─────────────────────────────────────────────────────────────

    public async Task<bool> DiscoverAsync(CancellationToken ct = default)
    {
        try
        {
            var ssdpMsg = Encoding.UTF8.GetBytes(
                "M-SEARCH * HTTP/1.1\r\n" +
                "HOST: 239.255.255.250:1900\r\n" +
                "MAN: \"ssdp:discover\"\r\n" +
                "MX: 3\r\n" +
                "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n\r\n");

            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 4000;
            var ep = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
            await udp.SendAsync(ssdpMsg, ssdpMsg.Length, ep);

            while (true)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var response = Encoding.UTF8.GetString(result.Buffer);
                    var locationLine = response.Split('\n')
                        .FirstOrDefault(l => l.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase));
                    if (locationLine == null) continue;

                    var locationUrl = locationLine.Split(':', 2)[1].Trim();
                    _controlUrl = await FetchControlUrlAsync(locationUrl, ct);
                    if (_controlUrl != null)
                    {
                        Log?.Invoke($"[UPnP] Router discovered: {locationUrl}");
                        return true;
                    }
                }
                catch (SocketException) { break; }
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[UPnP] Discovery failed: {ex.Message}");
        }
        return false;
    }

    private static async Task<string?> FetchControlUrlAsync(string location, CancellationToken ct)
    {
        try
        {
            var xml = await _http.GetStringAsync(location, ct);
            var doc = XDocument.Parse(xml);
            var ns  = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var wanService = doc.Descendants(ns + "service")
                .FirstOrDefault(s => (s.Element(ns + "serviceType")?.Value ?? "")
                    .Contains("WANIPConnection", StringComparison.OrdinalIgnoreCase));
            if (wanService == null) return null;
            var controlUrl = wanService.Element(ns + "controlURL")?.Value;
            if (string.IsNullOrEmpty(controlUrl)) return null;
            var baseUri = new Uri(location);
            return new Uri(baseUri, controlUrl).ToString();
        }
        catch { return null; }
    }

    // ── Add / Remove mappings ─────────────────────────────────────────────────

    public async Task<bool> AddPortMappingAsync(int externalPort, int internalPort, PortMappingProtocol proto,
        string description = "WGS", int leaseDurationSec = 0, CancellationToken ct = default)
    {
        if (_controlUrl == null) return false;
        var localIp = GetLocalIp();
        if (localIp == null) return false;

        var protoStr = proto == PortMappingProtocol.UDP ? "UDP" : "TCP";
        var body = BuildSoapBody("AddPortMapping",
            ("NewRemoteHost", ""),
            ("NewExternalPort", externalPort.ToString()),
            ("NewProtocol", protoStr),
            ("NewInternalPort", internalPort.ToString()),
            ("NewInternalClient", localIp),
            ("NewEnabled", "1"),
            ("NewPortMappingDescription", description),
            ("NewLeaseDuration", leaseDurationSec.ToString()));

        try
        {
            await _lock.WaitAsync(ct);
            var resp = await PostSoapAsync("AddPortMapping", body, ct);
            Log?.Invoke($"[UPnP] Mapped {protoStr} {externalPort}→{internalPort} ({description})");
            return resp.Contains("200");
        }
        catch (Exception ex) { Log?.Invoke($"[UPnP] AddPortMapping failed: {ex.Message}"); return false; }
        finally { _lock.Release(); }
    }

    public async Task<bool> RemovePortMappingAsync(int externalPort, PortMappingProtocol proto,
        CancellationToken ct = default)
    {
        if (_controlUrl == null) return false;
        var protoStr = proto == PortMappingProtocol.UDP ? "UDP" : "TCP";
        var body = BuildSoapBody("DeletePortMapping",
            ("NewRemoteHost", ""),
            ("NewExternalPort", externalPort.ToString()),
            ("NewProtocol", protoStr));
        try
        {
            await _lock.WaitAsync(ct);
            var resp = await PostSoapAsync("DeletePortMapping", body, ct);
            Log?.Invoke($"[UPnP] Removed mapping {protoStr} {externalPort}");
            return resp.Contains("200");
        }
        catch (Exception ex) { Log?.Invoke($"[UPnP] RemovePortMapping failed: {ex.Message}"); return false; }
        finally { _lock.Release(); }
    }

    // ── Server port helpers ───────────────────────────────────────────────────

    public async Task MapServerPortsAsync(Models.GameServer server, CancellationToken ct = default)
    {
        if (_controlUrl == null && !await DiscoverAsync(ct)) return;

        var name = server.DisplayName;
        await AddPortMappingAsync(server.ServerPort, server.ServerPort, PortMappingProtocol.UDP, $"{name} Game", ct: ct);
        await AddPortMappingAsync(server.ServerPort, server.ServerPort, PortMappingProtocol.TCP, $"{name} Game", ct: ct);
        if (server.QueryPort > 0)
            await AddPortMappingAsync(server.QueryPort, server.QueryPort, PortMappingProtocol.UDP, $"{name} Query", ct: ct);
        if (server.RconPort > 0)
            await AddPortMappingAsync(server.RconPort, server.RconPort, PortMappingProtocol.TCP, $"{name} RCON", ct: ct);
    }

    public async Task RemoveServerPortsAsync(Models.GameServer server, CancellationToken ct = default)
    {
        if (_controlUrl == null && !await DiscoverAsync(ct)) return;

        await RemovePortMappingAsync(server.ServerPort, PortMappingProtocol.UDP, ct);
        await RemovePortMappingAsync(server.ServerPort, PortMappingProtocol.TCP, ct);
        if (server.QueryPort > 0)
            await RemovePortMappingAsync(server.QueryPort, PortMappingProtocol.UDP, ct);
        if (server.RconPort > 0)
            await RemovePortMappingAsync(server.RconPort, PortMappingProtocol.TCP, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetLocalIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            return (s.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch { return null; }
    }

    private static string BuildSoapBody(string action, params (string name, string value)[] args)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\"?>");
        sb.Append("<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
        sb.Append("<s:Body>");
        sb.Append($"<u:{action} xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">");
        foreach (var (name, value) in args)
            sb.Append($"<{name}>{value}</{name}>");
        sb.Append($"</u:{action}>");
        sb.Append("</s:Body></s:Envelope>");
        return sb.ToString();
    }

    private async Task<string> PostSoapAsync(string action, string body, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, _controlUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        };
        req.Headers.Add("SOAPAction", $"\"urn:schemas-upnp-org:service:WANIPConnection:1#{action}\"");
        var resp = await _http.SendAsync(req, ct);
        return $"{(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync(ct)}";
    }
}
