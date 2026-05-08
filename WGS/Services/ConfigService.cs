using System.IO;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Services;

public class ConfigService
{
    public string AppDataPath { get; }
    public string ServersFile { get; }
    public string SettingsFile { get; }
    public string DefaultInstallRoot { get; set; }
    public string SteamLogin    { get; set; } = string.Empty;
    public string SteamPassword { get; set; } = string.Empty;

    public ConfigService()
    {
        AppDataPath        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WGS");
        ServersFile        = Path.Combine(AppDataPath, "servers.json");
        SettingsFile       = Path.Combine(AppDataPath, "settings.json");
        DefaultInstallRoot = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory,
            "servers");
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(DefaultInstallRoot);
        LoadSettings();
    }

    // SteamPassword is stored encrypted on disk; the record holds the ciphertext
    private record SettingsData(string DefaultInstallRoot, string SteamLogin, string SteamPasswordEncrypted);

    private void LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return;
        try
        {
            var d = JsonConvert.DeserializeObject<SettingsData>(File.ReadAllText(SettingsFile));
            if (d == null) return;
            if (!string.IsNullOrEmpty(d.DefaultInstallRoot)) DefaultInstallRoot = d.DefaultInstallRoot;
            SteamLogin    = d.SteamLogin;
            // Decrypt on load — falls back to empty string if decryption fails
            SteamPassword = string.IsNullOrEmpty(d.SteamPasswordEncrypted)
                ? string.Empty
                : EncryptionService.Decrypt(d.SteamPasswordEncrypted);
        }
        catch { }
    }

    public void Save()
    {
        var encryptedPassword = string.IsNullOrEmpty(SteamPassword)
            ? string.Empty
            : EncryptionService.Encrypt(SteamPassword);
        var d = new SettingsData(DefaultInstallRoot, SteamLogin, encryptedPassword);
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(d, Formatting.Indented));
    }

    public List<GameServer> LoadServers()
    {
        if (!File.Exists(ServersFile)) return [];
        try
        {
            var json = File.ReadAllText(ServersFile);
            return JsonConvert.DeserializeObject<List<GameServer>>(json) ?? [];
        }
        catch { return []; }
    }

    public void SaveServers(IEnumerable<GameServer> servers)
    {
        var json = JsonConvert.SerializeObject(servers, Formatting.Indented);
        File.WriteAllText(ServersFile, json);
    }
}
