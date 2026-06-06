namespace WGS.Models;

public class OnlinePlayer
{
    public string Name             { get; set; } = string.Empty;
    public string SteamId          { get; set; } = string.Empty;   // Steam64 tai UUID
    public int    Ping             { get; set; }
    public int    ConnectedSeconds { get; set; }

    public string ConnectedText => ConnectedSeconds switch
    {
        < 60    => $"{ConnectedSeconds}s",
        < 3600  => $"{ConnectedSeconds / 60}m {ConnectedSeconds % 60}s",
        _       => $"{ConnectedSeconds / 3600}h {ConnectedSeconds % 3600 / 60}m",
    };

    public string PingText  => Ping > 0 ? $"{Ping} ms" : "—";
    public string SteamIdShort => SteamId.Length > 10
        ? "…" + SteamId[^8..]
        : SteamId;
}
