using System.IO;
using System.Text;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Generates a .cs source file from any registered IGamePlugin so admins
/// can share, modify and re-import it via PluginCompilerService.
/// </summary>
public static class PluginExporterService
{
    public static string GenerateSource(IGamePlugin p)
    {
        var sb = new StringBuilder();
        var cls = SanitizeId(p.GameId);

        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using WGS.Games;");
        sb.AppendLine("using WGS.Models;");
        sb.AppendLine();
        sb.AppendLine($"// Auto-exported from WGS — edit as needed, then import with \"⬆ Import .cs plugin\"");
        sb.AppendLine();
        sb.AppendLine($"public class {cls}Plugin : GamePluginBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public override string GameId         => {Q(p.GameId)};");
        sb.AppendLine($"    public override string GameName       => {Q(p.GameName)};");
        sb.AppendLine($"    public override string Description    => {Q(p.Description)};");
        sb.AppendLine($"    public override string Category       => {Q(p.Category)};");
        sb.AppendLine($"    public override int    SteamAppId     => {p.SteamAppId};");

        if (p.SteamClientAppId != 0)
            sb.AppendLine($"    public override int    SteamClientAppId => {p.SteamClientAppId};");
        if (p.GameStoreAppId != 0)
            sb.AppendLine($"    public override int    GameStoreAppId   => {p.GameStoreAppId};");

        sb.AppendLine($"    public override string Executable     => {Q(p.Executable)};");
        sb.AppendLine($"    public override int    DefaultPort    => {p.DefaultPort};");
        sb.AppendLine($"    public override int    DefaultQueryPort => {p.DefaultQueryPort};");

        if (p.DefaultSteamPort != 0)
            sb.AppendLine($"    public override int    DefaultSteamPort => {p.DefaultSteamPort};");

        sb.AppendLine($"    public override int    DefaultMaxPlayers => {p.DefaultMaxPlayers};");

        if (p.RequiresSteamLogin)
            sb.AppendLine($"    public override bool   RequiresSteamLogin => true;");
        if (p.HasRcon)
            sb.AppendLine($"    public override bool   HasRcon => true;");
        if (p.UseNativeConsole)
            sb.AppendLine($"    public override bool   UseNativeConsole => true;");

        sb.AppendLine();

        // BuildStartArguments — try to get a sample, otherwise placeholder
        sb.AppendLine("    public override string BuildStartArguments(GameServer s)");
        sb.AppendLine("    {");
        try
        {
            // Build a dummy server so we can capture the actual arg string as a reference
            var dummy = new GameServer
            {
                GameId               = p.GameId,
                DisplayName          = "ExportedServer",
                ServerName           = "ExportedServer",
                InstallPath          = @"C:\GameServers\" + p.GameId,
                ServerPort           = p.DefaultPort,
                QueryPort            = p.DefaultQueryPort,
                SteamPort            = p.DefaultSteamPort,
                MaxPlayers           = p.DefaultMaxPlayers,
                GameSpecificSettings = p.GetDefaultSettings(),
            };
            var sample = p.BuildStartArguments(dummy);
            sb.AppendLine($"        // Sample output: {sample}");
        }
        catch { /* ignore — keep placeholder */ }
        sb.AppendLine("        // TODO: customise for your game");
        sb.AppendLine("        return $\"-port {s.ServerPort} -queryport {s.QueryPort} -maxplayers {s.MaxPlayers}\";");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetDefaultSettings
        var defaults = p.GetDefaultSettings();
        sb.AppendLine("    public override Dictionary<string, string> GetDefaultSettings() => new()");
        sb.AppendLine("    {");
        foreach (var kv in defaults)
            sb.AppendLine($"        [{Q(kv.Key)}] = {Q(kv.Value)},");
        sb.AppendLine("    };");
        sb.AppendLine();

        // GetConfigFields
        var fields = p.GetConfigFields();
        sb.AppendLine("    public override List<ConfigField> GetConfigFields()");
        sb.AppendLine("    {");
        sb.AppendLine("        var fields = BaseFields();");
        // Skip base fields (serverName, maxPlayers, serverPass) — they come from BaseFields()
        var baseKeys = new HashSet<string> { "serverName", "maxPlayers", "serverPass" };
        var extra = fields.Where(f => !baseKeys.Contains(f.Key)).ToList();
        if (extra.Count > 0)
        {
            sb.AppendLine("        fields.AddRange([");
            foreach (var f in extra)
            {
                var opts = f.Options == null ? "null"
                    : "new[] { " + string.Join(", ", f.Options.Select(o => Q(o))) + " }";
                sb.Append($"            new() {{ Key = {Q(f.Key)}, Label = {Q(f.Label)}, FieldType = ConfigFieldType.{f.FieldType}, DefaultValue = {Q(f.DefaultValue)}");
                if (f.Options != null) sb.Append($", Options = {opts}");
                if (f.Min != 0 || f.Max != 0) sb.Append($", Min = {f.Min}, Max = {f.Max}");
                sb.AppendLine(" },");
            }
            sb.AppendLine("        ]);");
        }
        sb.AppendLine("        return fields;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string ExportToFile(IGamePlugin plugin, string targetPath)
    {
        var source = GenerateSource(plugin);
        File.WriteAllText(targetPath, source, Encoding.UTF8);
        return source;
    }

    private static string Q(string s) => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string SanitizeId(string id)
    {
        // Turn "seven-days" → "Sevendays"
        var parts = id.Split(['-', '_', ' ']);
        return string.Concat(parts.Select(p =>
            p.Length == 0 ? "" : char.ToUpper(p[0]) + p[1..]));
    }
}
