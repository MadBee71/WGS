using System.Windows;
using System.Windows.Input;
using WGS.ViewModels;

namespace WGS.Views;

public partial class PluginCreatorView : Window
{
    public PluginCreatorView(PluginCreatorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PluginCreated += () => Dispatcher.Invoke(Close);
        vm.Cancelled     += () => Dispatcher.Invoke(Close);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
