using System.Text.RegularExpressions;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Parsii RCON-vastauksen strukturoiduiksi OnlinePlayer-olioiksi.
/// Kukin pelimoottori tuottaa oman formaatin — jokainen tarvitsee oman parserin.
/// </summary>
public static class PlayerParserService
{
    // ── Source Engine: "status"-komento ──────────────────────────────────────
    // Rivi: #  2 "PlayerName"  STEAM_0:1:12345678  00:14:22  42  0  active  ...
    // tai:  #  2 "PlayerName"  [U:1:12345678]       00:14:22  42  ...
    private static readonly Regex SourceStatusLine = new(
        @"^#\s+\d+\s+""(?<name>[^""]+)""\s+(?<steam>\S+)\s+(?<time>\d+:\d+:\d+)\s+(?<ping>\d+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static List<OnlinePlayer> ParseSourceStatus(string response)
    {
        var result = new List<OnlinePlayer>();
        foreach (Match m in SourceStatusLine.Matches(response))
        {
            result.Add(new OnlinePlayer
            {
                Name             = m.Groups["name"].Value,
                SteamId          = m.Groups["steam"].Value,
                Ping             = int.TryParse(m.Groups["ping"].Value, out var p) ? p : 0,
                ConnectedSeconds = ParseHms(m.Groups["time"].Value),
            });
        }
        return result;
    }

    // ── Rust: "playerlist"-komento ────────────────────────────────────────────
    // JSON-taulukko: [{"DisplayName":"X","SteamID":123,"Ping":45,"ConnectedSeconds":120}, ...]
    public static List<OnlinePlayer> ParseRustPlayerList(string response)
    {
        var result = new List<OnlinePlayer>();
        try
        {
            // Yksinkertainen regex-pohjainen parsija — ei tarvita Newtonsoft tässä
            var entries = Regex.Matches(response,
                @"\{[^}]*""DisplayName""\s*:\s*""(?<name>[^""]*)""\s*,\s*" +
                @"""SteamID""\s*:\s*(?<steam>\d+)\s*,\s*" +
                @"""Ping""\s*:\s*(?<ping>\d+)\s*,\s*" +
                @"""ConnectedSeconds""\s*:\s*(?<time>\d+)");
            foreach (Match m in entries)
            {
                result.Add(new OnlinePlayer
                {
                    Name             = m.Groups["name"].Value,
                    SteamId          = m.Groups["steam"].Value,
                    Ping             = int.TryParse(m.Groups["ping"].Value, out var p) ? p : 0,
                    ConnectedSeconds = int.TryParse(m.Groups["time"].Value, out var t) ? t : 0,
                });
            }
        }
        catch { }
        return result;
    }

    // ── Minecraft: "list"-komento ─────────────────────────────────────────────
    // "There are 2 of a max of 20 players online: Player1, Player2"
    public static List<OnlinePlayer> ParseMinecraftList(string response)
    {
        var result = new List<OnlinePlayer>();
        var m = Regex.Match(response, @"players online:\s*(.+)$", RegexOptions.Multiline);
        if (!m.Success) return result;
        foreach (var name in m.Groups[1].Value.Split(','))
        {
            var trimmed = name.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                result.Add(new OnlinePlayer { Name = trimmed });
        }
        return result;
    }

    // ── ARK: "ListPlayers"-komento ────────────────────────────────────────────
    // "0. PlayerName, SteamID\n1. ..."
    private static readonly Regex ArkLine = new(
        @"^\d+\.\s+(?<name>.+?),\s+(?<steam>\d+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static List<OnlinePlayer> ParseArkPlayers(string response)
    {
        var result = new List<OnlinePlayer>();
        foreach (Match m in ArkLine.Matches(response))
            result.Add(new OnlinePlayer
            {
                Name    = m.Groups["name"].Value.Trim(),
                SteamId = m.Groups["steam"].Value,
            });
        return result;
    }

    // ── Unreal (Mordhau/KF2): "PlayerList"-komento ───────────────────────────
    // "ID: 0 | SteamID: 76561198... | Name: PlayerName | Ping: 45"
    private static readonly Regex UnrealLine = new(
        @"SteamID:\s*(?<steam>\S+).*?Name:\s*(?<name>[^|]+).*?Ping:\s*(?<ping>\d+)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static List<OnlinePlayer> ParseUnrealPlayers(string response)
    {
        var result = new List<OnlinePlayer>();
        foreach (Match m in Regex.Matches(response,
            @"ID:\s*\d+\s*\|[^\n]+", RegexOptions.Multiline))
        {
            var line = m.Value;
            var name  = Regex.Match(line, @"Name:\s*([^|]+)").Groups[1].Value.Trim();
            var steam = Regex.Match(line, @"SteamID:\s*(\S+)").Groups[1].Value;
            var ping  = int.TryParse(Regex.Match(line, @"Ping:\s*(\d+)").Groups[1].Value, out var p) ? p : 0;
            if (!string.IsNullOrEmpty(name))
                result.Add(new OnlinePlayer { Name = name, SteamId = steam, Ping = ping });
        }
        return result;
    }

    // ── Dispatch: valitse parseri pelimoottorityypin mukaan ───────────────────
    public static List<OnlinePlayer> Parse(string engineFamily, string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return [];
        return engineFamily.ToLowerInvariant() switch
        {
            "source"   => ParseSourceStatus(response),
            "rust"     => ParseRustPlayerList(response),
            "minecraft"=> ParseMinecraftList(response),
            "ark"      => ParseArkPlayers(response),
            "unreal"   => ParseUnrealPlayers(response),
            _          => ParseGeneric(response),
        };
    }

    // ── Generinen fallback: yksi nimi per rivi ────────────────────────────────
    public static List<OnlinePlayer> ParseGeneric(string response)
    {
        var result = new List<OnlinePlayer>();
        foreach (var line in response.Split('\n'))
        {
            var trimmed = line.Trim().TrimStart('#').Trim();
            if (trimmed.Length < 2 || trimmed.StartsWith("Players") ||
                trimmed.StartsWith("There are") || trimmed.All(c => c == '-'))
                continue;
            result.Add(new OnlinePlayer { Name = trimmed });
        }
        return result;
    }

    // ── Apumetodi: "HH:MM:SS" → sekunnit ────────────────────────────────────
    private static int ParseHms(string hms)
    {
        var parts = hms.Split(':');
        if (parts.Length != 3) return 0;
        return int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2]);
    }
}
