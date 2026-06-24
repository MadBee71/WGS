using System.Net;
using System.Net.Sockets;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Steam A2S_PLAYER query — used by games that expose Steam Query protocol
/// but have no RCON (e.g. ARMA Reforger with -a2sPort).
/// </summary>
public static class A2SQueryService
{
    private static readonly byte[] ChallengeRequest =
        [0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF];

    public static async Task<List<OnlinePlayer>> QueryPlayersAsync(
        string host, int port, int timeoutMs = 3000)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = timeoutMs;
        udp.Client.SendTimeout    = timeoutMs;

        var endpoint = new IPEndPoint(IPAddress.Parse(host), port);

        try
        {
            // Step 1: send challenge request
            await udp.SendAsync(ChallengeRequest, ChallengeRequest.Length, endpoint);

            var cts = new CancellationTokenSource(timeoutMs);
            var result = await udp.ReceiveAsync(cts.Token);
            var data   = result.Buffer;

            // Response header: FF FF FF FF 41 = A2S_INFO reply (some games),
            //                  FF FF FF FF 44 = A2S_PLAYER reply (no challenge needed),
            //                  FF FF FF FF 41 + 4 bytes challenge
            byte[] challenge;

            if (data.Length >= 9 && data[4] == 0x41)
            {
                // Server sent a challenge number — use it
                challenge = data[5..9];
            }
            else if (data.Length >= 5 && data[4] == 0x44)
            {
                // Server replied with players directly (no challenge)
                return ParsePlayers(data);
            }
            else
            {
                return [];
            }

            // Step 2: send A2S_PLAYER with challenge
            byte[] playerReq = [0xFF, 0xFF, 0xFF, 0xFF, 0x55, .. challenge];
            await udp.SendAsync(playerReq, playerReq.Length, endpoint);

            var cts2   = new CancellationTokenSource(timeoutMs);
            var result2 = await udp.ReceiveAsync(cts2.Token);
            return ParsePlayers(result2.Buffer);
        }
        catch
        {
            return [];
        }
    }

    private static List<OnlinePlayer> ParsePlayers(byte[] data)
    {
        // A2S_PLAYER response: FF FF FF FF 44 [count] [entries...]
        // Each entry: [index byte] [name null-terminated] [score int32] [duration float]
        if (data.Length < 6 || data[4] != 0x44)
            return [];

        var players = new List<OnlinePlayer>();
        int count = data[5];
        int pos   = 6;

        for (int i = 0; i < count && pos < data.Length; i++)
        {
            pos++; // skip index byte

            // read null-terminated name
            int nameStart = pos;
            while (pos < data.Length && data[pos] != 0) pos++;
            var name = System.Text.Encoding.UTF8.GetString(data, nameStart, pos - nameStart);
            pos++; // skip null terminator

            pos += 8; // skip score (int32) + duration (float)

            if (!string.IsNullOrWhiteSpace(name))
                players.Add(new OnlinePlayer { Name = name });
        }

        return players;
    }
}
