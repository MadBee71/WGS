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
    private const string DefaultServiceType = "urn:schemas-upnp-org:service:WANIPConnection:1";

    /// <summary>A router's WAN connection service: where to send SOAP calls and the exact service type to name in them.</summary>
    private readonly record struct Igd(string ControlUrl, string ServiceType);

    private string?            _controlUrl;
    private string             _serviceType = DefaultServiceType;
    private bool               _discovered;

    /// <summary>The last port-mapping failure reported by the router (null when the last call succeeded) — for diagnostics.</summary>
    public string? LastError { get; private set; }
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

        // Try SSDP multicast first. Every answer is tried in turn: the first responder is not always the router.
        Igd? igd = null;
        foreach (var location in await SsdpSearchAsync())
        {
            igd = await GetControlAsync(location);
            if (igd != null) break;
        }

        // Fallback: probe gateway directly on known UPnP ports
        igd ??= await TryDirectDiscoveryAsync();

        _controlUrl  = igd?.ControlUrl;
        _serviceType = igd?.ServiceType ?? DefaultServiceType;
        _discovered  = _controlUrl != null;
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

    /// <summary>Default gateways of every active adapter (a PC with a VPN or virtual switch has several; the router is not always the first).</summary>
    private static List<string> GetGatewayIps()
    {
        var result = new List<string>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                foreach (var gw in ni.GetIPProperties().GatewayAddresses)
                    if (gw.Address.AddressFamily == AddressFamily.InterNetwork && !result.Contains(gw.Address.ToString()))
                        result.Add(gw.Address.ToString());
            }
        }
        catch { }
        return result;
    }

    /// <summary>Local IPv4 addresses of the active, multicast-capable adapters: SSDP is sent from each, since a multicast sent
    /// from "any" goes out of just one adapter and may never reach the router.</summary>
    private static List<IPAddress> GetLocalIPv4Addresses()
    {
        var result = new List<IPAddress>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up || !ni.SupportsMulticast) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) result.Add(ua.Address);
            }
        }
        catch { }
        if (result.Count == 0) result.Add(IPAddress.Any);
        return result;
    }

    private async Task<Igd?> TryDirectDiscoveryAsync()
    {
        var gateways = GetGatewayIps();
        if (gateways.Count == 0) return null;

        int[] ports = [1780, 5000, 49000, 49152, 2869];
        // /igddesc.xml is the AVM Fritz!Box description document (port 49000); the others are common on other routers.
        string[] paths = ["/InternetGatewayDevice.xml", "/rootDesc.xml", "/gatedesc.xml", "/igddesc.xml", "/upnp/IGD.xml", "/DeviceDescription.xml"];

        var tasks = (from gw in gateways
                     from port in ports
                     from path in paths
                     select TryFetchDescriptionAsync($"http://{gw}:{port}{path}")).ToList();

        var results = await Task.WhenAll(tasks);
        return results.FirstOrDefault(r => r != null);
    }

    private async Task<Igd?> TryFetchDescriptionAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var xml = await _http.GetStringAsync(url, cts.Token);
            if (xml.Contains("WANIPConnection") || xml.Contains("WANPPPConnection") || xml.Contains("InternetGatewayDevice"))
                return await GetControlAsync(url);
        }
        catch { }
        return null;
    }

    /// <summary>All SSDP answers (LOCATION urls) to a search for an internet gateway, sent from every local adapter.
    /// Stops at the first search target that gets any answer.</summary>
    private static async Task<List<string>> SsdpSearchAsync()
    {
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

        var found = new List<string>();
        foreach (var st in targets)
        {
            var perAdapter = GetLocalIPv4Addresses().Select(ip => SsdpSearchFromAsync(ip, st)).ToList();
            foreach (var locations in await Task.WhenAll(perAdapter))
                foreach (var l in locations)
                    if (!found.Contains(l)) found.Add(l);
            if (found.Count > 0) break;
        }
        return found;
    }

    private static async Task<List<string>> SsdpSearchFromAsync(IPAddress local, string st)
    {
        const string MulticastAddr = "239.255.255.250";
        const int    MulticastPort = 1900;

        var result = new List<string>();
        var req = "M-SEARCH * HTTP/1.1\r\n" +
                  $"HOST: {MulticastAddr}:{MulticastPort}\r\n" +
                  "MAN: \"ssdp:discover\"\r\n" +
                  "MX: 3\r\n" +
                  $"ST: {st}\r\n\r\n";
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(local, 0));
            if (!local.Equals(IPAddress.Any))
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
            udp.EnableBroadcast = true;
            var bytes = Encoding.ASCII.GetBytes(req);
            var ep    = new IPEndPoint(IPAddress.Parse(MulticastAddr), MulticastPort);
            await udp.SendAsync(bytes, bytes.Length, ep);
            await udp.SendAsync(bytes, bytes.Length, ep); // send twice for reliability

            // Wait up to 3 seconds (the MX value) for the first answer, then only briefly for more
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!cts.IsCancellationRequested)
            {
                UdpReceiveResult r;
                try { r = await udp.ReceiveAsync(cts.Token); } catch { break; }
                var loc = ParseLocation(Encoding.ASCII.GetString(r.Buffer));
                if (loc != null) { result.Add(loc); cts.CancelAfter(TimeSpan.FromMilliseconds(700)); }
            }
        }
        catch { }
        return result;
    }

    /// <summary>The LOCATION header of an SSDP answer, with or without a space after the colon.</summary>
    private static string? ParseLocation(string response)
    {
        var line = response.Split((char)10)
            .FirstOrDefault(l => l.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase));
        var value = line?["LOCATION:".Length..].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Reads a router's device description and returns its WAN connection service. The exact service type found
    /// there is kept (WANIPConnection:1/:2 or WANPPPConnection:1/:2), since a SOAP call must name the type the router
    /// actually offers; an IP connection is preferred over PPP when both exist.</summary>
    private async Task<Igd?> GetControlAsync(string location)
    {
        try
        {
            var xml     = await _http.GetStringAsync(location);
            var doc     = XDocument.Parse(xml);
            var baseUri = new Uri(location);
            XNamespace ns = "urn:schemas-upnp-org:device-1-0";

            // Some routers give the address that control URLs are relative to as <URLBase>, not the description's own address.
            var urlBase = doc.Descendants(ns + "URLBase").Select(e => e.Value.Trim()).FirstOrDefault(v => v.Length > 0);
            if (urlBase != null && Uri.TryCreate(urlBase, UriKind.Absolute, out var parsedBase)) baseUri = parsedBase;

            var services = doc.Descendants(ns + "service")
                .Select(s => (Type: ((string?)s.Element(ns + "serviceType") ?? "").Trim(),
                              Control: (string?)s.Element(ns + "controlURL")))
                .Where(x => x.Control != null && (x.Type.Contains("WANIPConnection") || x.Type.Contains("WANPPPConnection")))
                .OrderBy(x => x.Type.Contains("WANIPConnection") ? 0 : 1)
                .ToList();
            if (services.Count == 0) return null;

            var (type, control) = services[0];
            var url = control!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? control
                : $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}{(control.StartsWith('/') ? control : "/" + control)}";
            return new Igd(url, type);
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
        var client = GetLocalIp();

        Task<string?> Add(int lease) => SendSoapAsync("AddPortMapping", BuildSoapEnvelope("AddPortMapping",
            "<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            $"<NewProtocol>{protocol}</NewProtocol>" +
            $"<NewInternalPort>{port}</NewInternalPort>" +
            $"<NewInternalClient>{client}</NewInternalClient>" +
            "<NewEnabled>1</NewEnabled>" +
            $"<NewPortMappingDescription>{System.Security.SecurityElement.Escape(description)}</NewPortMappingDescription>" +
            $"<NewLeaseDuration>{lease}</NewLeaseDuration>"));

        var err = await Add(0);   // 0 = no expiry
        if (err != null && err.Contains("718"))
        {
            // 718 = a mapping for this port already exists (a stale one, e.g. from a previous run): replace it
            await RemoveMappingAsync(port, protocol);
            err = await Add(0);
        }
        // Some routers refuse a mapping without expiry: retry with a long lease (WGS adds the mapping again on every start)
        if (err != null && !err.Contains("606")) err = await Add(604800);

        LastError = err;
        if (err != null) System.Diagnostics.Trace.WriteLine($"[UPnP] {protocol} {port}: {err}");
    }

    private async Task RemoveMappingAsync(int port, string protocol)
    {
        var soap = BuildSoapEnvelope("DeletePortMapping",
            "<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            $"<NewProtocol>{protocol}</NewProtocol>");
        await SendSoapAsync("DeletePortMapping", soap);
    }

    private string BuildSoapEnvelope(string action, string innerXml) =>
        "<?xml version=\"1.0\"?>" +
        "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
        "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
        "<s:Body>" +
        $"<u:{action} xmlns:u=\"{_serviceType}\">" +
        innerXml +
        $"</u:{action}>" +
        "</s:Body>" +
        "</s:Envelope>";

    /// <summary>Sends one SOAP call; returns null on success, otherwise the router's error (HTTP status and UPnP error code).</summary>
    private async Task<string?> SendSoapAsync(string action, string body)
    {
        try
        {
            var content = new StringContent(body, Encoding.UTF8, "text/xml");
            content.Headers.Remove("Content-Type");
            content.Headers.TryAddWithoutValidation("Content-Type", "text/xml; charset=\"utf-8\"");
            content.Headers.TryAddWithoutValidation("SOAPAction", $"\"{_serviceType}#{action}\"");
            using var resp = await _http.PostAsync(_controlUrl, content);
            if (resp.IsSuccessStatusCode) return null;

            var text = await resp.Content.ReadAsStringAsync();
            var code = System.Text.RegularExpressions.Regex.Match(text, "<errorCode>([0-9]+)</errorCode>").Groups[1].Value;
            var desc = System.Text.RegularExpressions.Regex.Match(text, "<errorDescription>([^<]*)</errorDescription>").Groups[1].Value;
            return $"{action} refused: HTTP {(int)resp.StatusCode}, UPnP error {(code.Length > 0 ? code : "?")} {desc}".TrimEnd();
        }
        catch (Exception ex) { return $"{action} failed: {ex.Message}"; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>This PC's address on the network the router is on: the address a socket would use to reach the router
    /// (the right one even with a VPN or virtual adapters active), falling back to the one used to reach the internet.</summary>
    private string GetLocalIp()
    {
        string[] targets = [TryHost(_controlUrl), "8.8.8.8"];
        foreach (var target in targets)
        {
            if (string.IsNullOrEmpty(target)) continue;
            try
            {
                using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                s.Connect(target, 9);   // UDP connect: nothing is sent, it only picks the local address
                return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
            }
            catch { }
        }
        return "127.0.0.1";

        static string TryHost(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";
    }
}
