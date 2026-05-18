using WGS.Models;

namespace WGS.Games;

public class PalworldPlugin : GamePluginBase
{
    public override string GameId          => "palworld";
    public override string GameName        => "Palworld";
    public override string Description     => "Survival crafting with creature collecting";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2394010;
    public override int    GameStoreAppId  => 1623730;
    public override string Executable      => @"Pal\Binaries\Win64\PalServer-Win64-Shipping-Cmd.exe";
    public override int    DefaultPort     => 8211;
    public override int    DefaultQueryPort => 8212;
    public override int    DefaultMaxPlayers => 32;
    public override string BuildStartArguments(GameServer s)
        => $"-port={s.ServerPort} -queryport={s.QueryPort} -players={s.MaxPlayers} " +
           $"EpicApp=PalServer -useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverDescription"] = "Palworld Server",
        ["adminPassword"]     = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverDescription", Label = "Kuvaus",          FieldType = ConfigFieldType.Text,     DefaultValue = "Palworld Server" },
            new() { Key = "adminPassword",      Label = "Admin-salasana",  FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
