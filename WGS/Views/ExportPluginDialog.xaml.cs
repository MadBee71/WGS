using System.Collections.Generic;
using System.Windows;
using WGS.Games;

namespace WGS.Views;

public partial class ExportPluginDialog : Window
{
    public IGamePlugin? SelectedPlugin { get; private set; }

    public ExportPluginDialog(IEnumerable<IGamePlugin> plugins)
    {
        InitializeComponent();
        PluginCombo.ItemsSource = plugins;
        PluginCombo.SelectedIndex = 0;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SelectedPlugin = PluginCombo.SelectedItem as IGamePlugin;
        if (SelectedPlugin == null) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
