namespace WGS.Models;

/// <summary>One Thunderstore package installed by WGS (persisted in BepInEx\wgs-thunderstore.json).</summary>
public class ThunderstoreMod
{
    /// <summary>"Namespace-Name".</summary>
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Enabled { get; set; } = true;
    /// <summary>True when the user asked for this package; false when it only came in as a dependency.</summary>
    public bool Requested { get; set; }
    public bool IsLoader { get; set; }
    /// <summary>Ids ("Namespace-Name") of the packages this one depends on.</summary>
    public List<string> Dependencies { get; set; } = [];
    /// <summary>Files WGS wrote for this package, relative to the server folder (without the ".old" a disabled mod has).</summary>
    public List<string> Files { get; set; } = [];
    /// <summary>Filled by an update check only.</summary>
    public string? LatestVersion { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasUpdate => !string.IsNullOrEmpty(LatestVersion) && LatestVersion != Version;
    [System.Text.Json.Serialization.JsonIgnore]
    public string StatusText => (Enabled ? "" : "Disabled · ")
        + (HasUpdate ? $"{Version} → {LatestVersion} available" : Version)
        + (Requested || IsLoader ? "" : " · dependency");
    [System.Text.Json.Serialization.JsonIgnore]
    public string ToggleText => Enabled ? "Disable" : "Enable";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanToggle => !IsLoader;
}
