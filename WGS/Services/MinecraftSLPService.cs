using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WGS.Services;

/// <summary>
/// Minecraft modern Server List Ping (SLP, 1.7+).
/// Returns (online, max) player counts without requiring RCON or query ports.
/// Used as a fallback when RCON is not connected on Minecraft-family servers.
/// </summary>
public static class MinecraftSLPService
{
    public static async Task<(int Online, int Max)?> QueryAsync(string host, int port, int timeoutMs = 3000)
    {
        try
        {
            using var tcp = new TcpClient();
            var connect = tcp.ConnectAsync(host, port);
            if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect || !tcp.Connected)
                return null;

            await using var stream = tcp.GetStream();
            stream.ReadTimeout  = timeoutMs;
            stream.WriteTimeout = timeoutMs;

            // ── Handshake (0x00) ────────────────────────────────────────────────
            // Fields: protocol version (-1), host, port (ushort), next state (1 = status)
            var hostBytes  = Encoding.UTF8.GetBytes(host);
            var handshake  = new List<byte>();
            handshake.Add(0x00);                        // packet id
            WriteVarInt(handshake, -1);                 // protocol version (any)
            WriteVarInt(handshake, hostBytes.Length);   // host length
            handshake.AddRange(hostBytes);              // host
            handshake.Add((byte)(port >> 8));           // port high byte
            handshake.Add((byte)(port & 0xFF));         // port low byte
            WriteVarInt(handshake, 1);                  // next state: status
            await WritePacketAsync(stream, handshake.ToArray());

            // ── Status request (0x00, empty) ────────────────────────────────────
            await WritePacketAsync(stream, [0x00]);

            // ── Read status response ─────────────────────────────────────────────
            var length = await ReadVarIntAsync(stream);
            if (length <= 0) return null;

            var data = new byte[length];
            var read = 0;
            while (read < length)
            {
                var chunk = await stream.ReadAsync(data.AsMemory(read, length - read));
                if (chunk == 0) break;
                read += chunk;
            }

            // data[0] is packet id (0x00), then a VarInt string length, then JSON
            var jsonOffset = 1; // skip packet id byte
            var jsonLen = ReadVarInt(data, ref jsonOffset);
            var json = Encoding.UTF8.GetString(data, jsonOffset, jsonLen);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("players", out var players)) return null;
            var online = players.GetProperty("online").GetInt32();
            var max    = players.GetProperty("max").GetInt32();
            return (online, max);
        }
        catch { return null; }
    }

    private static async Task WritePacketAsync(NetworkStream stream, byte[] payload)
    {
        var lengthBuf = new List<byte>();
        WriteVarInt(lengthBuf, payload.Length);
        await stream.WriteAsync(lengthBuf.ToArray().AsMemory());
        await stream.WriteAsync(payload.AsMemory());
    }

    private static void WriteVarInt(List<byte> buf, int value)
    {
        var uval = (uint)value;
        do
        {
            var b = (byte)(uval & 0x7F);
            uval >>= 7;
            if (uval != 0) b |= 0x80;
            buf.Add(b);
        } while (uval != 0);
    }

    private static async Task<int> ReadVarIntAsync(NetworkStream stream)
    {
        var result = 0;
        var shift  = 0;
        var buf    = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(buf.AsMemory(0, 1)) == 0) return -1;
            var b = buf[0];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift >= 35) return -1;
        }
    }

    private static int ReadVarInt(byte[] data, ref int offset)
    {
        var result = 0;
        var shift  = 0;
        while (offset < data.Length)
        {
            var b = data[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        return result;
    }
}
