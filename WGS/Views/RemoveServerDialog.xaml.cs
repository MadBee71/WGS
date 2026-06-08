namespace WGS.Views;

public enum RemoveServerResult { Cancel, RemoveOnly, RemoveWithFiles }

public partial class RemoveServerDialog : System.Windows.Window
{
    public RemoveServerResult Result { get; private set; } = RemoveServerResult.Cancel;

    public RemoveServerDialog(string serverName)
    {
        InitializeComponent();
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
        Result = RemoveServerResult.RemoveWithFiles;
        Close();
    }
}
