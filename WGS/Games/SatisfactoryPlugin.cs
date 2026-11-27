using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

// FactoryServer.exe in the install root is only a launcher: it starts the real server as a child process that opens its
// own console window. WGS starts that server directly, with the same "FactoryGame" argument the launcher passes.
// It must not get redirected standard handles: with pipes it stops responding during startup and can leave a process
// behind that only a reboot removes (tested against the real server, build 24656085, 27.11.2026).
public class SatisfactoryPlugin : GamePluginBase, IWorkshopPlugin, IRestPlayersPlugin, IConsoleLogPlugin, IHealthProbePlugin
{
    // The server does not answer Steam A2S queries; health and player count come from its HTTPS API on the game port.
    // The certificate is the server's self-signed one, and only 127.0.0.1 is contacted.
    private static readonly HttpClient Api = new(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
        { Timeout = TimeSpan.FromSeconds(5) };
    private readonly ConcurrentDictionary<string, string> _tokens = new(); // server id -> API token

    public string GetRestApiBaseUrl(GameServer s) => $"https://127.0.0.1:{s.ServerPort}/api/v1";
    public string? LastRestApiError { get; private set; }

    private async Task<HttpResponseMessage> CallAsync(GameServer s, string function, object data, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GetRestApiBaseUrl(s))
        {
            Content = new StringContent(JsonSerializer.Serialize(new { function, data }), System.Text.Encoding.UTF8, "application/json")
        };
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Api.SendAsync(request);
    }

    public async Task<bool> IsAliveAsync(GameServer s)
    {
        try
        {
            using var response = await CallAsync(s, "HealthCheck", new { clientCustomData = "" });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
    }

    /// <summary>
    /// Logs in with the lowest ("Client") privilege: without a password when the server has no join password,
    /// otherwise with the join password from this server's Password field.
    /// </summary>
    private async Task<string?> LoginAsync(GameServer s)
    {
        using (var open = await CallAsync(s, "PasswordlessLogin", new { minimumPrivilegeLevel = "Client" }))
            if (await ReadTokenAsync(open) is { } token) return token;

        if (string.IsNullOrEmpty(s.ServerPassword)) return null;
        using var withPassword = await CallAsync(s, "PasswordLogin", new { minimumPrivilegeLevel = "Client", password = s.ServerPassword });
        return await ReadTokenAsync(withPassword);
    }

    private static async Task<string?> ReadTokenAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("authenticationToken", out var t)
            ? t.GetString() : null;
    }

    /// <summary>The API gives the number of players only, so each one is listed as "?".</summary>
    public async Task<List<OnlinePlayer>> GetPlayersAsync(GameServer s)
    {
        try
        {
            for (var attempt = 0; attempt < 2; attempt++) // a cached token stops working when the server restarts
            {
                if (!_tokens.TryGetValue(s.Id, out var token))
                {
                    token = await LoginAsync(s);
                    if (token == null)
                    {
                        LastRestApiError = "Cannot read the player count. If the server has a join password, enter it in this server's Password field.";
                        return [];
                    }
                    _tokens[s.Id] = token;
                }

                using var response = await CallAsync(s, "QueryServerState", new { }, token);
                if (!response.IsSuccessStatusCode) { _tokens.TryRemove(s.Id, out _); continue; }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var count = doc.RootElement.GetProperty("data").GetProperty("serverGameState").GetProperty("numConnectedPlayers").GetInt32();
                LastRestApiError = null;
                return Enumerable.Range(0, count).Select(_ => new OnlinePlayer { Name = "?" }).ToList();
            }
            LastRestApiError = "The server refused the player count query.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            LastRestApiError = $"Cannot read the player count: {ex.Message}";
        }
        return [];
    }

    public override string GameId          => "satisfactory";
    public override string GameName        => "Satisfactory";
    public override string Description     => "Factory building open-world game";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 1690800;
    public override int    GameStoreAppId  => 526870;
    public override int    WorkshopAppId   => 526870;

    public string ModTargetDirectory => @"FactoryGame/Content/Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Engine\Binaries\Win64\FactoryServer-Win64-Shipping-Cmd.exe";
    public override string? GetWorkingDirectory(GameServer s) => s.InstallPath; // as when started through the launcher

    public bool OwnHiddenConsole => true;
    public string? LegacyProcessName => "FactoryServer";

    public (string Folder, string Pattern) GetConsoleLog(GameServer s) =>
        (Path.Combine(s.InstallPath, "FactoryGame", "Saved", "Logs"), "FactoryGame*.log");

    // The engine renames the previous log to *-backup-* and Windows can give the new file the old creation time,
    // so the run's log is recognised by having been written to since the start.
    public bool IsRunLog(FileInfo file, DateTime startedUtc) =>
        !file.Name.Contains("-backup-", StringComparison.OrdinalIgnoreCase) && file.LastWriteTimeUtc >= startedUtc;

    public string? FormatLogLine(string line)
    {
        // Engine start-up chatter that would bury everything else
        if (line.Contains("LogCSLocTools:") || line.Contains("Verbose:")) return null;
        var text = System.Text.RegularExpressions.Regex.Replace(line, @"^\[[\d.\-:]+\]\[[ \d]+\]", "");
        return text.Trim().Length == 0 ? null : text;
    }
    /// <summary>The server keeps its worlds in the profile of the Windows account that runs it.</summary>
    public static string SaveGamesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FactoryGame", "Saved", "SaveGames");

    /// <summary>
    /// One copy of all server worlds, made once before WGS first starts a Satisfactory server with this start method.
    /// Needs nothing from the user and is never overwritten. Returns a console message, or null if there was nothing to do.
    /// </summary>
    public static string? KeepSafetyCopy()
    {
        var target = Path.GetFullPath(Path.Combine(SaveGamesPath, "..", "WGS-safety-copy"));
        try
        {
            if (Directory.Exists(target) || !Directory.Exists(SaveGamesPath)) return null;
            var files = new[] { "server", "blueprints" }
                .Select(name => Path.Combine(SaveGamesPath, name))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                .ToList();
            if (files.Count == 0) return null; // no worlds yet
            files.AddRange(Directory.GetFiles(SaveGamesPath, "ServerSettings.*.sav"));

            // Copied aside first and renamed when complete, so an interrupted copy is made again on the next start
            var partial = target + ".partial";
            foreach (var file in files)
            {
                var copy = Path.Combine(partial, Path.GetRelativePath(SaveGamesPath, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(file, copy, overwrite: true);
            }
            Directory.Move(partial, target);
            return $"[WGS] A one-time safety copy of your Satisfactory worlds was saved to: {target}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"[WGS] Could not make a safety copy of your Satisfactory worlds, will try again on the next start: {ex.Message}";
        }
    }

    /// <summary>Where one restore keeps the saves it replaces. Outside SaveGames, so the server does not list them.</summary>
    public static string NewReplacedSavesFolder() => Path.GetFullPath(
        Path.Combine(SaveGamesPath, "..", "WGS-replaced-saves", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")));

    /// <summary>Copies a save that a restore is about to overwrite or delete. Throws if it cannot, so nothing gets lost.</summary>
    public static void KeepReplacedSave(string file, string asideFolder)
    {
        var full = Path.GetFullPath(file);
        var root = SaveGamesPath + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return;
        var copy = Path.Combine(asideFolder, Path.GetRelativePath(SaveGamesPath, full));
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(full, copy, overwrite: true);
    }

    /// <summary>
    /// Called after a backup has been restored. Saves of a restored world that were not part of the backup are moved
    /// (not deleted) to Saved\WGS-replaced-saves; otherwise the server would load the newest one instead of the restored one.
    /// </summary>
    public static void SetAsideOtherSaves(IReadOnlyCollection<string> restoredFiles, string asideFolder)
    {
        var folder = Path.GetFullPath(Path.Combine(SaveGamesPath, "server"));
        if (!Directory.Exists(folder)) return;

        var restored = new HashSet<string>(restoredFiles, StringComparer.OrdinalIgnoreCase);
        var worlds = restored.Where(f => f.EndsWith(".sav", StringComparison.OrdinalIgnoreCase)
                                         && string.Equals(Path.GetDirectoryName(f), folder, StringComparison.OrdinalIgnoreCase))
            .Select(ReadSessionName).Where(n => n != null).ToHashSet();
        if (worlds.Count == 0) return;

        var aside = Path.Combine(asideFolder, "server");
        foreach (var file in Directory.GetFiles(folder, "*.sav"))
        {
            if (restored.Contains(Path.GetFullPath(file)) || !worlds.Contains(ReadSessionName(file))) continue;
            Directory.CreateDirectory(aside);
            File.Move(file, Path.Combine(aside, Path.GetFileName(file)), overwrite: true);
        }
    }

    // Save header: header version, save version, build, [save name, from header version 14], map name, map options, session name.
    private static string? ReadSessionName(string file)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(file));
            var headerVersion = reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            if (headerVersion >= 14) ReadString(reader);
            ReadString(reader);
            ReadString(reader);
            return ReadString(reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    // Unreal string: length including the terminator; negative means UTF-16
    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length is > 65536 or < -65536) throw new InvalidDataException("Not a save header");
        var bytes = reader.ReadBytes(length < 0 ? -length * 2 : length);
        return (length < 0 ? System.Text.Encoding.Unicode : System.Text.Encoding.UTF8).GetString(bytes).TrimEnd('\0');
    }

    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 15777;
    public override int    DefaultMaxPlayers => 4;


    public override string BuildStartArguments(GameServer s)
        => $"FactoryGame -log -unattended -Port={s.ServerPort} -ServerQueryPort={s.QueryPort} -ReliablePort={s.ServerPort + 1111} -multihome=0.0.0.0";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["autosaveInterval"] = "300",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "autosaveInterval", Label = "Autosave (s)", FieldType = ConfigFieldType.Number, DefaultValue = "300", Min = 60, Max = 3600 },
        ]);
        return fields;
    }
}
