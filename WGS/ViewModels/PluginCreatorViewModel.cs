using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WGS.Games;
using WGS.Models;

namespace WGS.ViewModels;

public partial class PluginCreatorViewModel : BaseViewModel
{
    public static readonly string PluginsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "custom_plugins.json");

    public static IReadOnlyList<string> Categories { get; } =
        ["Survival", "Racing", "Simulation", "FPS", "Strategy", "Other"];

    public ObservableCollection<CustomGameDefinition> ExistingCustomGames { get; } = [];

    public PluginCreatorViewModel() => RefreshExistingCustomGames();

    private void RefreshExistingCustomGames()
    {
        ExistingCustomGames.Clear();
        foreach (var def in GameRegistry.ListCustomPlugins(PluginsPath))
            ExistingCustomGames.Add(def);
    }

    [RelayCommand]
    private void DeleteCustomGame(CustomGameDefinition? def)
    {
        if (def == null) return;
        GameRegistry.RemoveCustomPlugin(def.GameId, PluginsPath);
        RefreshExistingCustomGames();
    }

    [ObservableProperty] private string _gameName        = string.Empty;
    [ObservableProperty] private string _gameId          = string.Empty;
    [ObservableProperty] private string _description     = string.Empty;
    [ObservableProperty] private string _category        = "Other";
    [ObservableProperty] private string _executable      = string.Empty;
    [ObservableProperty] private int    _steamAppId;
    [ObservableProperty] private int    _defaultPort       = 27015;
    [ObservableProperty] private int    _defaultQueryPort  = 27016;
    [ObservableProperty] private int    _defaultMaxPlayers = 16;
    [ObservableProperty] private bool   _requiresSteamLogin;
    [ObservableProperty] private bool   _hasRcon;

    [ObservableProperty] private string _errorMessage = string.Empty;

    public event Action? PluginCreated;
    public event Action? Cancelled;

    partial void OnGameNameChanged(string value)
    {
        GameId = value.ToLowerInvariant().Replace(' ', '_');
    }

    [RelayCommand]
    private void Create()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(GameName))   { ErrorMessage = "Game name is required."; return; }
        if (string.IsNullOrWhiteSpace(GameId))     { ErrorMessage = "Game ID is required."; return; }
        if (string.IsNullOrWhiteSpace(Executable)) { ErrorMessage = "Executable is required."; return; }

        var isExistingCustom = ExistingCustomGames.Any(d => d.GameId == GameId.Trim());
        if (!isExistingCustom && GameRegistry.Get(GameId.Trim()) != null)
        {
            ErrorMessage = $"\"{GameId}\" already exists as a built-in game — pick a different name/ID to avoid replacing it.";
            return;
        }

        var def = new CustomGameDefinition
        {
            GameId            = GameId.Trim(),
            GameName          = GameName.Trim(),
            Description       = Description.Trim(),
            Category          = Category,
            Executable        = Executable.Trim(),
            SteamAppId        = SteamAppId,
            DefaultPort       = DefaultPort,
            DefaultQueryPort  = DefaultQueryPort,
            DefaultMaxPlayers = DefaultMaxPlayers,
            RequiresSteamLogin = RequiresSteamLogin,
            HasRcon           = HasRcon,
        };

        try
        {
            GameRegistry.SaveCustomPlugin(def, PluginsPath);
            GameRegistry.LoadCustomPlugins(PluginsPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save module: {ex.Message}";
            return;
        }

        RefreshExistingCustomGames();
        PluginCreated?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
