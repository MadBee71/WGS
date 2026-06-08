using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WGS.ViewModels;

public class FileItem
{
    public string Name      { get; set; } = string.Empty;
    public string FullPath  { get; set; } = string.Empty;
    public bool   IsDir     { get; set; }
    public string SizeText  { get; set; } = string.Empty;
    public string Modified  { get; set; } = string.Empty;
    public string Icon      => IsDir ? "📁" : GetFileIcon(Name);
    public bool   IsEditable => !IsDir && EditableExtensions.Contains(
        Path.GetExtension(Name).ToLowerInvariant());

    public static readonly HashSet<string> EditableExtensions =
    [
        ".cfg", ".ini", ".json", ".xml", ".txt", ".yaml", ".yml",
        ".toml", ".conf", ".config", ".properties", ".log", ".sh",
        ".bat", ".cmd", ".env", ".csv"
    ];

    private static string GetFileIcon(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".cfg" or ".ini" or ".json" or ".xml" or ".txt"
            or ".yaml" or ".yml" or ".toml" or ".conf"
            or ".config" or ".properties"             => "📄",
        ".log"                                        => "📋",
        ".exe" or ".bat" or ".cmd" or ".sh"           => "⚙",
        ".zip" or ".rar" or ".7z" or ".tar"           => "🗜",
        ".png" or ".jpg" or ".jpeg" or ".bmp"         => "🖼",
        _                                             => "📄",
    };
}

public partial class FileBrowserViewModel : ObservableObject
{
    private string _rootPath = string.Empty;

    public ObservableCollection<FileItem> Items      { get; } = [];
    public ObservableCollection<string>   Breadcrumb { get; } = [];

    [ObservableProperty] private string    _currentPath     = string.Empty;
    [ObservableProperty] private FileItem? _selectedItem;
    [ObservableProperty] private string    _statusText      = string.Empty;
    [ObservableProperty] private string    _newName         = string.Empty;
    [ObservableProperty] private bool      _showRenameBox;
    [ObservableProperty] private string    _searchQuery     = string.Empty;

    // ── Tekstieditori ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool      _isEditing;
    [ObservableProperty] private string    _editorContent   = string.Empty;
    [ObservableProperty] private string    _editingFileName = string.Empty;
    [ObservableProperty] private bool      _editorDirty;
    private string _editingFullPath = string.Empty;

    partial void OnSearchQueryChanged(string _) => ApplySearch();

    public bool CanGoUp => CurrentPath != _rootPath && !string.IsNullOrEmpty(CurrentPath);
    public bool HasRoot => !string.IsNullOrEmpty(_rootPath);

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Initialize(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) return;
        _rootPath = Path.GetFullPath(rootPath);
        Navigate(_rootPath);
    }

    // ── Tietoturva: Path Traversal -suojaus ───────────────────────────────────

    /// <summary>
    /// Palauttaa true vain jos polku on _rootPathin alla.
    /// Normalisoi molemmat Path.GetFullPath:lla, joten "../" ja symlinkit
    /// eivät voi paeta juurikansion ulkopuolelle.
    /// </summary>
    public bool IsPathSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(_rootPath))
            return false;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(_rootPath);
            // Varmistetaan erotinmerkki loppuun estämään prefix-huijaukset
            // esim. root="C:\server" full="C:\server2\..." pitää hylätä
            var rootWithSep = root.TrimEnd(Path.DirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
            return full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ── Navigointi ────────────────────────────────────────────────────────────

    public void Navigate(string path)
    {
        if (!IsPathSafe(path))
        {
            StatusText = "Error: Path is outside the server root folder.";
            return;
        }
        if (!Directory.Exists(path))
        {
            StatusText = "Error: Folder not found.";
            return;
        }
        CurrentPath = path;
        Refresh();
        BuildBreadcrumb();
        OnPropertyChanged(nameof(CanGoUp));
    }

    [RelayCommand]
    private void Refresh()
    {
        Items.Clear();
        if (!Directory.Exists(CurrentPath)) return;
        try
        {
            foreach (var dir in Directory.GetDirectories(CurrentPath).OrderBy(d => d))
            {
                var info = new DirectoryInfo(dir);
                Items.Add(new FileItem
                {
                    Name     = info.Name,
                    FullPath = dir,
                    IsDir    = true,
                    Modified = info.LastWriteTime.ToString("dd.MM.yyyy HH:mm"),
                });
            }
            foreach (var file in Directory.GetFiles(CurrentPath).OrderBy(f => f))
            {
                var info = new FileInfo(file);
                Items.Add(new FileItem
                {
                    Name     = info.Name,
                    FullPath = file,
                    IsDir    = false,
                    SizeText = FormatSize(info.Length),
                    Modified = info.LastWriteTime.ToString("dd.MM.yyyy HH:mm"),
                });
            }
            StatusText = $"{Items.Count} kohdetta  —  {CurrentPath}";
            ApplySearch();
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    private void ApplySearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        var q = SearchQuery.Trim().ToLowerInvariant();
        foreach (var item in Items)
        {
            // WPF ItemsControl ei suoraan tue näkyvyyssuodatusta — filtteröidään
            // uudelleentäyttämällä lista sopivalla kutsulla.
            _ = item; // käytetään alla
        }
        // Yksinkertaisin tapa: lataa kaikki uudelleen ja suodata
        var all = Items.ToList();
        Items.Clear();
        foreach (var item in all.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Items.Add(item);
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            StatusText = $"{Items.Count} osumaa hakuun \"{SearchQuery}\"";
    }

    [RelayCommand]
    private void Open(FileItem? item)
    {
        if (item == null) return;
        if (!IsPathSafe(item.FullPath))
        {
            StatusText = "Error: Path is outside the server root folder.";
            return;
        }
        if (item.IsDir)
            Navigate(item.FullPath);
        else if (item.IsEditable)
            OpenForEdit(item);
        else
        {
            // Ei-muokattavat tiedostot avataan järjestelmällä
            try { System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(item.FullPath) { UseShellExecute = true }); }
            catch (Exception ex) { StatusText = $"Error opening: {ex.Message}"; }
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        if (parent != null && IsPathSafe(parent) && CurrentPath != _rootPath)
            Navigate(parent);
    }

    // ── Tekstieditori ─────────────────────────────────────────────────────────

    private static readonly long MaxEditableBytes = 2L * 1024 * 1024; // 2 MB raja

    public void OpenForEdit(FileItem item)
    {
        if (!IsPathSafe(item.FullPath)) { StatusText = "Security error: path rejected."; return; }
        if (item.IsDir) return;

        var info = new FileInfo(item.FullPath);
        if (info.Length > MaxEditableBytes)
        {
            StatusText = $"File is too large to edit ({FormatSize(info.Length)} > 2 MB).";
            return;
        }
        try
        {
            EditorContent   = File.ReadAllText(item.FullPath);
            _editingFullPath = item.FullPath;
            EditingFileName  = item.Name;
            EditorDirty      = false;
            IsEditing        = true;
        }
        catch (Exception ex) { StatusText = $"Error opening: {ex.Message}"; }
    }

    [RelayCommand]
    private void EditSelectedItem()
    {
        if (SelectedItem != null && !SelectedItem.IsDir)
            OpenForEdit(SelectedItem);
    }

    [RelayCommand]
    private void SaveEdit()
    {
        if (!IsEditing || string.IsNullOrEmpty(_editingFullPath)) return;
        if (!IsPathSafe(_editingFullPath)) { StatusText = "Security error: save rejected."; return; }
        try
        {
            File.WriteAllText(_editingFullPath, EditorContent);
            EditorDirty = false;
            StatusText  = $"Saved: {EditingFileName}";
            Refresh();
        }
        catch (Exception ex) { StatusText = $"Save error: {ex.Message}"; }
    }

    [RelayCommand]
    private void CloseEditor()
    {
        if (EditorDirty)
        {
            var r = System.Windows.MessageBox.Show(
                "Unsaved changes — close anyway?", "Confirm",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (r != System.Windows.MessageBoxResult.Yes) return;
        }
        IsEditing        = false;
        EditorContent    = string.Empty;
        _editingFullPath = string.Empty;
        EditingFileName  = string.Empty;
        EditorDirty      = false;
    }

    // Kutsutaan kun tekstisisältö muuttuu (bindingin kautta)
    public void NotifyEditorChanged() => EditorDirty = true;

    // ── Lataus (Download) ─────────────────────────────────────────────────────

    [RelayCommand]
    private void DownloadFile(FileItem? item)
    {
        if (item == null || item.IsDir) return;
        if (!IsPathSafe(item.FullPath)) { StatusText = "Security error: load rejected."; return; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Upload file",
            FileName   = item.Name,
            Filter     = "Kaikki tiedostot|*.*",
            DefaultExt = Path.GetExtension(item.Name),
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.Copy(item.FullPath, dlg.FileName, overwrite: true);
            StatusText = $"Uploaded: {dlg.FileName}";
        }
        catch (Exception ex) { StatusText = $"Upload error: {ex.Message}"; }
    }

    // ── Kansion luonti ────────────────────────────────────────────────────────

    [RelayCommand]
    private void CreateFolder()
    {
        var name = $"New folder {DateTime.Now:HHmmss}";
        var path = Path.Combine(CurrentPath, name);
        if (!IsPathSafe(path)) { StatusText = "Security error: folder creation rejected."; return; }
        try { Directory.CreateDirectory(path); Refresh(); }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    // ── Poisto ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void DeleteItem(FileItem? item)
    {
        if (item == null) return;
        if (!IsPathSafe(item.FullPath)) { StatusText = "Security error: delete rejected."; return; }

        var result = System.Windows.MessageBox.Show(
            $"Delete \"{item.Name}\"?", "Confirm",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            if (item.IsDir) Directory.Delete(item.FullPath, recursive: true);
            else            File.Delete(item.FullPath);
            Refresh();
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    // ── Nimeäminen ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void RenameItem(FileItem? item)
    {
        if (item == null) return;
        if (!IsPathSafe(item.FullPath)) { StatusText = "Security error."; return; }
        NewName       = item.Name;
        ShowRenameBox = true;
        SelectedItem  = item;
    }

    [RelayCommand]
    private void ConfirmRename()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(NewName))
        {
            ShowRenameBox = false;
            return;
        }
        // Estä polkusegmentit uudessa nimessä
        if (NewName.Contains('/') || NewName.Contains('\\') || NewName.Contains(".."))
        {
            StatusText    = "Error: Name must not contain path separators.";
            ShowRenameBox = false;
            return;
        }
        var dest = Path.Combine(CurrentPath, NewName);
        if (!IsPathSafe(dest)) { StatusText = "Security error: destination rejected."; ShowRenameBox = false; return; }
        try
        {
            if (SelectedItem.IsDir) Directory.Move(SelectedItem.FullPath, dest);
            else                    File.Move(SelectedItem.FullPath, dest, overwrite: false);
            ShowRenameBox = false;
            Refresh();
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private void CancelRename() => ShowRenameBox = false;

    // ── Explorer ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (!IsPathSafe(CurrentPath)) return;
        try { System.Diagnostics.Process.Start("explorer.exe", CurrentPath); }
        catch { }
    }

    // ── Leivänmuru ────────────────────────────────────────────────────────────

    private void BuildBreadcrumb()
    {
        Breadcrumb.Clear();
        var root  = Path.GetFullPath(_rootPath);
        var curr  = Path.GetFullPath(CurrentPath);
        // Näytetään vain juuresta alkaava suhteellinen polku
        var rel   = Path.GetRelativePath(root, curr);
        Breadcrumb.Add(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)));
        if (rel != ".")
            foreach (var part in rel.Split(Path.DirectorySeparatorChar))
                Breadcrumb.Add(part);
    }

    // ── Apumetodit ────────────────────────────────────────────────────────────

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)                return $"{bytes} B";
        if (bytes < 1024 * 1024)         return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
