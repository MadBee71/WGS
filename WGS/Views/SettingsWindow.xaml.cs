using WGS.ViewModels;

namespace WGS.Views;

public partial class SettingsWindow : System.Windows.Window
{
    public SettingsWindow(SettingsViewModel vm, object mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel;
        SettingsViewControl.DataContext = vm;
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseClick(object sender, System.Windows.RoutedEventArgs e) => Close();
}
