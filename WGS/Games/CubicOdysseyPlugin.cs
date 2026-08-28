using WGS.Models;

namespace WGS.Games;

public class CubicOdysseyPlugin : GamePluginBase
{
    public override string GameId            => "cubicodyssey";
    public override string GameName          => "Cubic Odyssey";
    public override string Description       => "Open-world survival and crafting game with cubic voxel building";
    public override string Category          => "Survival";
    public override int    SteamAppId        => 3858450;
    public override string Executable        => @"server\CubicOdysseyServer.exe";
    public override int    DefaultPort       => 27001;
    public override int    DefaultQueryPort  => 27002;
    public override int    DefaultMaxPlayers => 16;
    public override bool   RequiresSteamLogin => true;

    public override string BuildStartArguments(GameServer s)
    {
        var args = $"-MaxNumPlayers={s.MaxPlayers} -Port={s.ServerPort} -MaxPort={s.ServerPort + 5}";
        if (!string.IsNullOrEmpty(s.ServerPassword))
            args += $" -Password={s.ServerPassword}";
        return args;
    }

    public override Task PreStartAsync(GameServer server) => Task.CompletedTask;

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
