using System.Collections.Generic;
using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional extension for games whose RCON player-list output needs a game-specific parser.
/// Keeps game-specific parsing inside the external plugin instead of WGS core.
/// </summary>
public interface IGamePlayerParserPlugin
{
    List<OnlinePlayer> ParsePlayers(string response);
}
