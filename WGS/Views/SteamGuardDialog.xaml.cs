using System.Windows;
using System.Windows.Input;

namespace WGS.Views;

public partial class SteamGuardDialog : Window
{
    public string Code { get; private set; } = string.Empty;

    public SteamGuardDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => CodeBox.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Code = CodeBox.Text.Trim();
        DialogResult = true;   // empty code = no guard needed, proceed anyway
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;  // null = user cancelled, abort install
    }

    private void CodeBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Confirm_Click(sender, e);
        if (e.Key == Key.Escape) Cancel_Click(sender, e);
    }
}
