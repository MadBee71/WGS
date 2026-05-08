using System.IO;
using System.Net.Sockets;
using System.Text;

namespace WGS.Services;

/// <summary>
/// Source RCON protocol implementation (used by Rust, Valheim, Minecraft + RCON mods, etc.)
/// </summary>
public class RconService : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _requestId = 1;
    private bool _authenticated;

    public bool IsConnected => _client?.Connected == true && _authenticated;

    public async Task<bool> ConnectAsync(string host, int port, string password)
    {
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
        if (!IsConnected) return "[RCON] Not connected";
        var id = _requestId++;
        await SendPacketAsync(2, command, id);
        var resp = await ReadPacketAsync();
        return resp.body;
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
    }

    public void Dispose() => Disconnect();
}
