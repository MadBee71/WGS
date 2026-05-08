namespace WGS.Models;

public class CustomGameDefinition
{
    public string GameId            { get; set; } = string.Empty;
    public string GameName          { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public string Category          { get; set; } = string.Empty;
    public string Executable        { get; set; } = string.Empty;
    public int    SteamAppId        { get; set; }
    public int    DefaultPort       { get; set; } = 27015;
    public int    DefaultQueryPort  { get; set; } = 27016;
    public int    DefaultSteamPort  { get; set; } = 0;
    public int    DefaultMaxPlayers { get; set; } = 16;
    public bool   RequiresSteamLogin { get; set; }
    public bool   HasRcon           { get; set; }
}
