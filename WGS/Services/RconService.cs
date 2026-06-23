using System.IO;
using System.Net.Sockets;
using System.Text;

namespace WGS.Services;

public enum RconProtocol
{
    /// <summary>TCP, used by Rust, Valheim, Minecraft + RCON mods, etc.</summary>
    SourceTcp,
    /// <summary>FXServer (FiveM/RedM) doesn't speak Source RCON at all — it uses the older
    /// single-packet UDP "quake rcon" format on the SAME port as the game traffic, with no
    /// persistent connection/handshake. See https://docs.fivem.net/docs/server-manual/server-commands/#rcon</summary>
    LegacyUdp,
}

/// <summary>
/// Source RCON protocol implementation (used by Rust, Valheim, Minecraft + RCON mods, etc.),
/// plus FXServer's older UDP-based protocol selected via <see cref="RconProtocol.LegacyUdp"/>.
/// </summary>
public class RconService : IDisposable
{
    private readonly RconProtocol _protocol;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _requestId = 1;
    private bool _authenticated;

    private UdpClient? _udp;
    private string _udpPassword = "";

    public RconService(RconProtocol protocol = RconProtocol.SourceTcp) => _protocol = protocol;

    public bool IsConnected => _protocol == RconProtocol.LegacyUdp
        ? _udp != null && _authenticated
        : _client?.Connected == true && _authenticated;

    public async Task<bool> ConnectAsync(string host, int port, string password)
    {
        if (_protocol == RconProtocol.LegacyUdp)
            return await ConnectLegacyUdpAsync(host, port, password);

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            // AUTH packet
            await SendPacketAsync(3, password);
            var resp = await ReadPacketAsync();
            _authenticated = resp.id != -1;
            return _authenticated;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> SendCommandAsync(string command)
    {
        if (_protocol == RconProtocol.LegacyUdp)
            return await SendLegacyUdpCommandAsync(command);

        if (!IsConnected) return "[RCON] Not connected";
        var id = _requestId++;
        await SendPacketAsync(2, command, id);
        var resp = await ReadPacketAsync();
        return resp.body;
    }

    // ── FXServer legacy UDP rcon ─────────────────────────────────────────────
    // This protocol has no real handshake at all — every single packet carries the password,
    // and there's nothing to "connect" to since UDP has no connection state. Trying to validate
    // the password upfront with a probe command was fragile (no command is guaranteed to get a
    // reply, and error text varies). So "connect" just opens the local socket; whether the
    // password is actually right only shows up in the response text of real commands you send.

    private Task<bool> ConnectLegacyUdpAsync(string host, int port, string password)
    {
        try
        {
            _udp?.Dispose();
            _udp = new UdpClient();
            _udp.Connect(host, port);
            _udpPassword = password;
            _authenticated = true;
            return Task.FromResult(true);
        }
        catch
        {
            _authenticated = false;
            return Task.FromResult(false);
        }
    }

    private async Task<string> SendLegacyUdpCommandAsync(string command)
    {
        if (!IsConnected) return "[RCON] Not connected";
        try
        {
            var reply = await SendLegacyUdpRawAsync(command);
            return reply ?? "[RCON] No response from server (command may have been sent — FXServer doesn't always reply)";
        }
        catch (SocketException)
        {
            return "[RCON] No reply — is the server actually running?";
        }
    }

    private async Task<string?> SendLegacyUdpRawAsync(string command)
    {
        if (_udp == null) return null;

        var payload = Encoding.UTF8.GetBytes($"rcon {_udpPassword} {command}");
        var packet = new byte[4 + payload.Length];
        packet[0] = packet[1] = packet[2] = packet[3] = 0xFF;
        Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

        await _udp.SendAsync(packet, packet.Length);

        // Output can span several UDP packets — keep reading until a short gap with nothing more.
        var sb = new StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            while (true)
            {
                using var packetCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                packetCts.CancelAfter(TimeSpan.FromMilliseconds(300));
                UdpReceiveResult result;
                try { result = await _udp.ReceiveAsync(packetCts.Token); }
                catch (OperationCanceledException) { break; }

                var data = result.Buffer;
                // Reply framing: 0xFFFFFFFF + "print\n" + text
                if (data.Length > 8)
                {
                    var text = Encoding.UTF8.GetString(data, 4, data.Length - 4);
                    if (text.StartsWith("print\n")) text = text["print\n".Length..];
                    sb.Append(text);
                }
            }
        }
        catch (OperationCanceledException) { /* total timeout reached, return what we have */ }

        return sb.ToString();
    }

    private async Task SendPacketAsync(int type, string body, int id = 1)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var size = 4 + 4 + bodyBytes.Length + 2;
        var packet = new byte[4 + size];

        Write32(packet, 0, size);
        Write32(packet, 4, id);
        Write32(packet, 8, type);
        Buffer.BlockCopy(bodyBytes, 0, packet, 12, bodyBytes.Length);
        // two null terminators already zero-initialized

        await _stream!.WriteAsync(packet);
    }

    private async Task<(int id, string body)> ReadPacketAsync()
    {
        var header = new byte[12];
        await ReadExactAsync(header, 12);
        var size = Read32(header, 0);
        var id   = Read32(header, 4);
        // type = Read32(header, 8) - not needed
        var bodyLen = size - 4 - 4 - 2;
        if (bodyLen <= 0) return (id, string.Empty);

        var body = new byte[bodyLen];
        await ReadExactAsync(body, bodyLen);
        // skip 2 null terminators
        var tail = new byte[2];
        await ReadExactAsync(tail, 2);

        return (id, Encoding.UTF8.GetString(body));
    }

    private async Task ReadExactAsync(byte[] buf, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            var read = await _stream!.ReadAsync(buf.AsMemory(offset, count - offset));
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static void Write32(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static int Read32(byte[] buf, int offset)
        => buf[offset] | (buf[offset+1] << 8) | (buf[offset+2] << 16) | (buf[offset+3] << 24);

    public void Disconnect()
    {
        _authenticated = false;
        _stream?.Dispose();
        _client?.Dispose();
        _udp?.Dispose();
        _udp = null;
    }

    public void Dispose() => Disconnect();
}
