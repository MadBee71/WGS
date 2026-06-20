using System;
using System.Threading.Tasks;

namespace WGS.Views;

public enum BuildChannelResult { Cancel, Recommended, Latest }

public partial class BuildChannelDialog : System.Windows.Window
{
    public BuildChannelResult Result { get; private set; } = BuildChannelResult.Cancel;

    private readonly string _gameName;
    private readonly Func<Task<(string? Recommended, string? Latest)>> _refresh;

    public BuildChannelDialog(string gameName, string? recommendedBuild, string? latestBuild,
        Func<Task<(string? Recommended, string? Latest)>> refresh)
    {
        InitializeComponent();
        _gameName = gameName;
        _refresh = refresh;
        TitleText.Text = $"Install {gameName}";
        SetBuilds(recommendedBuild, latestBuild);
    }

    private void SetBuilds(string? recommendedBuild, string? latestBuild)
    {
        RecommendedButton.Content = recommendedBuild != null ? $"Recommended ({recommendedBuild})" : "Recommended";
        LatestButton.Content = latestBuild != null ? $"Latest ({latestBuild})" : "Latest";
    }

    private async void RefreshClick(object s, System.Windows.RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        RefreshButton.Content = "🔄 Checking...";
        var (recommended, latest) = await _refresh();
        SetBuilds(recommended, latest);
        RefreshButton.Content = "🔄 Check updates";
        RefreshButton.IsEnabled = true;
    }

    private void CancelClick(object s, System.Windows.RoutedEventArgs e)
    {
        Result = BuildChannelResult.Cancel;
        Close();
    }

    private void RecommendedClick(object s, System.Windows.RoutedEventArgs e)
    {
        Result = BuildChannelResult.Recommended;
        Close();
    }

    private void LatestClick(object s, System.Windows.RoutedEventArgs e)
    {
        Result = BuildChannelResult.Latest;
        Close();
    }
}
