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
    /// <summary>BattlEye RCON (UDP) used by Arma Reforger, DayZ, Arma 3.
    /// Login and commands are framed as BE + CRC32 + 0xFF + type + payload packets.</summary>
    BattlEyeUdp,
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

    // BattlEye state
    private byte _beSeq;
    private CancellationTokenSource? _beReceiveCts;
    private readonly Dictionary<byte, BeInflight> _beInflight = new();
    private readonly object _beInflightLock = new();

    /// <summary>Diagnostic callback — set by the owner to receive raw BE protocol events.</summary>
    public Action<string>? DiagnosticLog { get; set; }

    private sealed class BeInflight
    {
        public readonly TaskCompletionSource<string> Tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int TotalParts = 1;
        public readonly SortedDictionary<int, byte[]> Parts = new();
    }

    public RconService(RconProtocol protocol = RconProtocol.SourceTcp) => _protocol = protocol;

    public bool IsConnected => _protocol switch
    {
        RconProtocol.LegacyUdp    => _udp != null && _authenticated,
        RconProtocol.BattlEyeUdp  => _udp != null && _authenticated,
        _                          => _client?.Connected == true && _authenticated,
    };

    public async Task<bool> ConnectAsync(string host, int port, string password)
    {
        if (_protocol == RconProtocol.LegacyUdp)
            return await ConnectLegacyUdpAsync(host, port, password);
        if (_protocol == RconProtocol.BattlEyeUdp)
            return await ConnectBattlEyeAsync(host, port, password);

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
        if (_protocol == RconProtocol.BattlEyeUdp)
            return await SendBattlEyeCommandAsync(command);

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

    // ── BattlEye RCON (UDP) ──────────────────────────────────────────────────
    // Protocol used by Arma Reforger, DayZ, Arma 3.
    // Packet format: "BE" (2) + CRC32 of rest (4 LE) + 0xFF + type (1) + payload
    // Types: 0x00 = login, 0x01 = command, 0x02 = server message ack

    private async Task<bool> ConnectBattlEyeAsync(string host, int port, string password)
    {
        try
        {
            _udp?.Dispose();
            _udp = new UdpClient();
            _udp.Connect(host, port);
            _udpPassword = password;
            _beSeq = 0;

            var loginPacket = BuildBePacket(0x00, Encoding.UTF8.GetBytes(password));
            await _udp.SendAsync(loginPacket, loginPacket.Length);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            UdpReceiveResult result;
            try { result = await _udp.ReceiveAsync(cts.Token); }
            catch { _authenticated = false; return false; }

            var buf = result.Buffer;
            // Response: BE(2) + CRC(4) + 0xFF + type(0x00) + result(1)
            if (buf.Length < 9 || buf[0] != 'B' || buf[1] != 'E' || buf[7] != 0x00)
            {
                _authenticated = false;
                return false;
            }
            _authenticated = buf[8] == 0x01;
            if (_authenticated)
            {
                // Start background receive loop — ACKs server messages immediately so the
                // server never considers us timed out between command polls.
                _beReceiveCts?.Cancel();
                _beReceiveCts = new CancellationTokenSource();
                _ = Task.Run(() => BattlEyeReceiveLoopAsync(_beReceiveCts.Token));
            }
            return _authenticated;
        }
        catch
        {
            _authenticated = false;
            return false;
        }
    }

    private async Task BattlEyeReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await _udp!.ReceiveAsync(ct); }
            catch { return; }

            // Wrap packet processing so a malformed packet can't kill the receive loop.
            try
            {
                var buf = result.Buffer;
                DiagnosticLog?.Invoke($"[BE RX] {buf.Length} bytes  hdr=[{(buf.Length > 0 ? buf[0] : 0):X2} {(buf.Length > 1 ? buf[1] : 0):X2}]  type={( buf.Length > 7 ? buf[7].ToString("X2") : "??")}  seq={( buf.Length > 8 ? buf[8].ToString() : "??")}");
                if (buf.Length < 8 || buf[0] != 'B' || buf[1] != 'E') continue;

                var type = buf[7];

                if (type == 0x02 && buf.Length > 8)
                {
                    DiagnosticLog?.Invoke($"[BE RX] Server keepalive seq={buf[8]} — ACKing");
                    var ack = BuildBePacket(0x02, new byte[] { buf[8] });
                    try { await _udp!.SendAsync(ack, ack.Length); } catch { }
                }
                else if (type == 0x01 && buf.Length >= 9)
                {
                    var responseSeq = buf[8];

                    byte inflightKey;
                    BeInflight? inf;
                    lock (_beInflightLock)
                    {
                        if (_beInflight.TryGetValue(responseSeq, out inf))
                        {
                            inflightKey = responseSeq;
                            DiagnosticLog?.Invoke($"[BE RX] Command response seq={responseSeq} — exact match, len={buf.Length}");
                        }
                        else if (_beInflight.Count > 0)
                        {
                            // AR Reforger echoes a different seq than what was sent — match oldest pending
                            inflightKey = _beInflight.Keys.Min();
                            inf = _beInflight[inflightKey];
                            DiagnosticLog?.Invoke($"[BE RX] Command response seq={responseSeq} (no exact match) — fallback to inflight seq={inflightKey}, len={buf.Length}");
                        }
                        else
                        {
                            DiagnosticLog?.Invoke($"[BE RX] Command response seq={responseSeq} — NO inflight entries, discarding");
                            continue;
                        }
                    }
                    if (inf == null) continue;

                    // Empty payload (length==9) = bare command ACK, no data yet — keep waiting
                    if (buf.Length == 9) continue;

                    bool complete;
                    if (buf[9] == 0x00 && buf.Length > 11)
                    {
                        // Multi-packet fragment
                        inf.TotalParts = buf[10];
                        int idx = buf[11];
                        var part = new byte[buf.Length - 12];
                        Buffer.BlockCopy(buf, 12, part, 0, part.Length);
                        inf.Parts[idx] = part;
                        complete = inf.Parts.Count >= inf.TotalParts;
                    }
                    else
                    {
                        // Single-packet response — data starts at byte 9
                        var part = new byte[buf.Length - 9];
                        Buffer.BlockCopy(buf, 9, part, 0, part.Length);
                        inf.Parts[0] = part;
                        complete = true;
                    }

                    if (complete)
                    {
                        lock (_beInflightLock) _beInflight.Remove(inflightKey);
                        var sb = new StringBuilder();
                        foreach (var p in inf.Parts.Values)
                            sb.Append(Encoding.UTF8.GetString(p));
                        inf.Tcs.TrySetResult(sb.ToString());
                    }
                }
            }
            catch { /* skip malformed packet — keep the loop alive */ }
        }
    }

    private async Task<string> SendBattlEyeCommandAsync(string command)
    {
        if (_udp == null || !_authenticated) return "[RCON] Not connected";
        try
        {
            var seq = _beSeq++;
            var inf = new BeInflight();
            lock (_beInflightLock) _beInflight[seq] = inf;

            var cmdBytes = Encoding.UTF8.GetBytes(command);
            var payload = new byte[1 + cmdBytes.Length];
            payload[0] = seq;
            Buffer.BlockCopy(cmdBytes, 0, payload, 1, cmdBytes.Length);
            var packet = BuildBePacket(0x01, payload);
            await _udp.SendAsync(packet, packet.Length);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                DiagnosticLog?.Invoke($"[BE TX] Sent command seq={seq}, waiting for response...");
                return await inf.Tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                lock (_beInflightLock) _beInflight.Remove(seq);
                DiagnosticLog?.Invoke($"[BE TX] TIMEOUT waiting for response to seq={seq}");
                return "";
            }
        }
        catch (Exception ex)
        {
            return $"[RCON] Error: {ex.Message}";
        }
    }

    private byte[] BuildBePacket(byte type, byte[] payload)
    {
        // CRC covers everything from 0xFF onward
        var content = new byte[2 + payload.Length];
        content[0] = 0xFF;
        content[1] = type;
        Buffer.BlockCopy(payload, 0, content, 2, payload.Length);

        var crc = ComputeCrc32(content, 0, content.Length);
        var packet = new byte[6 + content.Length];
        packet[0] = (byte)'B';
        packet[1] = (byte)'E';
        packet[2] = (byte)(crc        & 0xFF);
        packet[3] = (byte)((crc >> 8) & 0xFF);
        packet[4] = (byte)((crc >> 16)& 0xFF);
        packet[5] = (byte)((crc >> 24)& 0xFF);
        Buffer.BlockCopy(content, 0, packet, 6, content.Length);
        return packet;
    }

    private static uint ComputeCrc32(byte[] data, int offset, int length)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
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
        _beReceiveCts?.Cancel();
        _beReceiveCts = null;
        lock (_beInflightLock)
        {
            foreach (var inf in _beInflight.Values)
                inf.Tcs.TrySetCanceled();
            _beInflight.Clear();
        }
        _stream?.Dispose();
        _client?.Dispose();
        _udp?.Dispose();
        _udp = null;
    }

    public void Dispose() => Disconnect();
}
