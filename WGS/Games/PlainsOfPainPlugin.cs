using System.Collections.Generic;
using System.IO;
using System.Linq;
using WGS.Models;

namespace WGS.Games;

public class PlainsOfPainPlugin : GamePluginBase
{
    public override string GameId          => "plainsofpain";
    public override string GameName        => "Plains of Pain";
    public override string Description     => "Top-down survival sandbox";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2227360;
    public override string Executable      => "PlainsOfPain.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 16;

    public override Task PreStartAsync(GameServer s)
    {
        // The game's own *.json configs hardcode port/queryPort/serverName — strip those lines so
        // the command-line values below (which WGS actually manages) take effect instead.
        foreach (var file in Directory.GetFiles(s.InstallPath, "*.json"))
        {
            var lines = File.ReadAllLines(file)
                .Where(l => !l.Contains("\"port\"") && !l.Contains("\"queryPort\"") && !l.Contains("\"serverName\""))
                .ToList();
            File.WriteAllLines(file, lines);
        }
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
        => $"-ignorecompilererrors -config configs/default.json -userdir ./save-dir " +
           $"-port={s.ServerPort} -queryPort={s.QueryPort} -serverName={s.ServerName} -nographics -batchmode";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
