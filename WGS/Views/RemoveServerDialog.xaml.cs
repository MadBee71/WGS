namespace WGS.Views;

public enum RemoveServerResult { Cancel, RemoveOnly, RemoveWithFiles }

public partial class RemoveServerDialog : System.Windows.Window
{
    public RemoveServerResult Result { get; private set; } = RemoveServerResult.Cancel;

    private readonly string _serverName;

    public RemoveServerDialog(string serverName)
    {
        InitializeComponent();
        _serverName = serverName;
        TitleText.Text = $"Remove \"{serverName}\"?";
    }

    private void CancelClick(object s, System.Windows.RoutedEventArgs e)
    {
        Result = RemoveServerResult.Cancel;
        Close();
    }

    private void RemoveOnlyClick(object s, System.Windows.RoutedEventArgs e)
    {
        Result = RemoveServerResult.RemoveOnly;
        Close();
    }

    private void RemoveWithFilesClick(object s, System.Windows.RoutedEventArgs e)
    {
        var confirmed = System.Windows.MessageBox.Show(
            $"This will permanently delete all installed files for \"{_serverName}\" from disk. This cannot be undone.\n\nAre you sure?",
            "Delete server files",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (confirmed != System.Windows.MessageBoxResult.Yes) return;

        Result = RemoveServerResult.RemoveWithFiles;
        Close();
    }
}
