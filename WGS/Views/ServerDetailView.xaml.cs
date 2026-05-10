using System.Windows;
using WGS.Services;
using WGS.ViewModels;

namespace WGS.Views;

public partial class ServerDetailView : System.Windows.Controls.UserControl
{
    public ServerDetailView()
    {
        InitializeComponent();
    }

    private void AddScheduleTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ServerViewModel vm) return;

        var dlg = new AddScheduleTaskDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;

        vm.AddScheduledTaskCommand.Execute(dlg.Result);
    }
}
