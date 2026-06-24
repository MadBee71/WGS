using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Services;

public class TemplateService
{
    private readonly string _templatesFile;
    private readonly List<ServerTemplate> _templates = [];
    private readonly object _lock = new();

    public TemplateService(ConfigService config)
    {
        _templatesFile = Path.Combine(config.AppDataPath, "templates.json");
        Load();
    }

    public IReadOnlyList<ServerTemplate> All
    {
        get { lock (_lock) return _templates.ToList(); }
    }

    public IReadOnlyList<ServerTemplate> ForGame(string gameId)
    {
        lock (_lock) return _templates.Where(t => t.GameId == gameId).ToList();
    }

    public IReadOnlyList<string> AllCategories
    {
        get
        {
            lock (_lock)
                return _templates
                    .Select(t => t.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c)
                    .ToList();
        }
    }

    public void SaveFromServer(GameServer server, string templateName, string description = "",
        string category = "", List<string>? tags = null)
    {
        var template = new ServerTemplate
        {
            Name                 = templateName,
            GameId               = server.GameId,
            GameName             = server.DisplayName,
            Description          = description,
            Category             = category,
            Tags                 = tags ?? [],
            DefaultPort          = server.ServerPort,
            DefaultQueryPort     = server.QueryPort,
            DefaultSteamPort     = server.SteamPort,
            MaxPlayers           = server.MaxPlayers,
            AutoRestart          = server.AutoRestart,
            AutoUpdate           = server.AutoUpdate,
            BackupEnabled        = server.BackupEnabled,
            BackupRetention      = server.BackupRetention,
            CustomArgs           = server.CustomArgs,
            ProcessPriority      = server.ProcessPriority,
            GameSpecificSettings = new Dictionary<string, string>(server.GameSpecificSettings),
        };
        lock (_lock)
        {
            _templates.Add(template);
            Save();
        }
    }

    public void ApplyToServer(ServerTemplate template, GameServer server)
    {
        server.MaxPlayers      = template.MaxPlayers;
        server.AutoRestart     = template.AutoRestart;
        server.AutoUpdate      = template.AutoUpdate;
        server.BackupEnabled   = template.BackupEnabled;
        server.BackupRetention = template.BackupRetention;
        server.CustomArgs      = template.CustomArgs;
        server.ProcessPriority = template.ProcessPriority;
        foreach (var kv in template.GameSpecificSettings)
            server.GameSpecificSettings[kv.Key] = kv.Value;
    }

    // ── Clone ─────────────────────────────────────────────────────────────────

    public ServerTemplate Clone(string id)
    {
        lock (_lock)
        {
            var src = _templates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");

            var clone = new ServerTemplate
            {
                Name                 = src.Name + " (kopio)",
                GameId               = src.GameId,
                GameName             = src.GameName,
                Description          = src.Description,
                Category             = src.Category,
                Tags                 = [.. src.Tags],
                DefaultPort          = src.DefaultPort,
                DefaultQueryPort     = src.DefaultQueryPort,
                DefaultSteamPort     = src.DefaultSteamPort,
                MaxPlayers           = src.MaxPlayers,
                AutoRestart          = src.AutoRestart,
                AutoUpdate           = src.AutoUpdate,
                BackupEnabled        = src.BackupEnabled,
                BackupRetention      = src.BackupRetention,
                CustomArgs           = src.CustomArgs,
                ProcessPriority      = src.ProcessPriority,
                GameSpecificSettings = new Dictionary<string, string>(src.GameSpecificSettings),
            };
            _templates.Add(clone);
            Save();
            return clone;
        }
    }

    // ── Export / Import ───────────────────────────────────────────────────────

    /// <summary>Export a single template to .json or .wgst (ZIP with image).</summary>
    public void ExportSingle(string id, string filePath, string? customImagePath = null)
    {
        lock (_lock)
        {
            var t = _templates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");
            var json = JsonConvert.SerializeObject(t, Formatting.Indented);

            if (customImagePath != null && File.Exists(customImagePath))
            {
                using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);
                var jsonEntry = zip.CreateEntry("template.json");
                using (var w = new StreamWriter(jsonEntry.Open()))
                    w.Write(json);
                zip.CreateEntryFromFile(customImagePath, "server_image.png");
            }
            else
            {
                File.WriteAllText(filePath, json);
            }
        }
    }

    /// <summary>Vie kaikki templatet yhteen .json-tiedostoon.</summary>
    public void ExportAll(string filePath)
    {
        lock (_lock)
            File.WriteAllText(filePath,
                JsonConvert.SerializeObject(_templates, Formatting.Indented));
    }

    /// <summary>
    /// Import templates from a .json or .wgst file.
    /// Returns (count, extracted image path or null).
    /// </summary>
    public (int Count, string? ImagePath) Import(string filePath, string imageOutputDir)
    {
        string json;
        string? imagePath = null;

        if (Path.GetExtension(filePath).Equals(".wgst", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(filePath);
            var jsonEntry = zip.GetEntry("template.json")
                ?? throw new InvalidOperationException("Invalid .wgst file: missing template.json");
            using var sr  = new StreamReader(jsonEntry.Open());
            json = sr.ReadToEnd();

            var imgEntry = zip.GetEntry("server_image.png");
            if (imgEntry != null)
            {
                Directory.CreateDirectory(imageOutputDir);
                imagePath = Path.Combine(imageOutputDir, "_import_" + Guid.NewGuid() + ".png");
                imgEntry.ExtractToFile(imagePath, overwrite: true);
            }
        }
        else
        {
            json = File.ReadAllText(filePath);
        }

        List<ServerTemplate>? list;
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
            list = JsonConvert.DeserializeObject<List<ServerTemplate>>(json);
        else
        {
            var single = JsonConvert.DeserializeObject<ServerTemplate>(json);
            list = single != null ? [single] : null;
        }

        if (list == null || list.Count == 0) return (0, null);

        lock (_lock)
        {
            foreach (var t in list)
                t.Id = Guid.NewGuid().ToString();
            _templates.AddRange(list);
            Save();
        }
        return (list.Count, imagePath);
    }

    // ── Delete / Rename ───────────────────────────────────────────────────────

    public void Delete(string id)
    {
        lock (_lock)
        {
            _templates.RemoveAll(t => t.Id == id);
            Save();
        }
    }

    public void Rename(string id, string newName)
    {
        lock (_lock)
        {
            var t = _templates.FirstOrDefault(t => t.Id == id);
            if (t != null) { t.Name = newName; Save(); }
        }
    }

    public void UpdateMetadata(string id, string category, List<string> tags, string description)
    {
        lock (_lock)
        {
            var t = _templates.FirstOrDefault(t => t.Id == id);
            if (t == null) return;
            t.Category    = category;
            t.Tags        = tags;
            t.Description = description;
            Save();
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void Load()
    {
        if (!File.Exists(_templatesFile)) return;
        try
        {
            var list = JsonConvert.DeserializeObject<List<ServerTemplate>>(
                File.ReadAllText(_templatesFile));
            if (list != null)
                lock (_lock) { _templates.Clear(); _templates.AddRange(list); }
        }
        catch { }
    }

    private void Save()
        => File.WriteAllText(_templatesFile,
            JsonConvert.SerializeObject(_templates, Formatting.Indented));
}
