using System.Windows;
using WGS.Services;

namespace WGS.Views;

public partial class AddScheduleTaskDialog : Window
{
    public ScheduledTask? Result { get; private set; }

    public AddScheduleTaskDialog()
    {
        InitializeComponent();
    }

    private void CbFreq_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DayPanel != null)
            DayPanel.Visibility = CbFreq.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeSpan.TryParse(TbTime.Text, out var time))
        {
            System.Windows.MessageBox.Show("Invalid time format. Use HH:mm", "Error");
            return;
        }

        var action = (ScheduledActionType)CbAction.SelectedIndex;
        var freq   = CbFreq.SelectedIndex == 0 ? ScheduleFrequency.Daily : ScheduleFrequency.Weekly;
        var day    = (DayOfWeek)(CbDay.SelectedIndex + 1); // Monday=1

        Result = new ScheduledTask
        {
            Action    = action,
            Frequency = freq,
            TimeOfDay = time,
            DayOfWeek = freq == ScheduleFrequency.Weekly ? day : DayOfWeek.Monday,
            IsEnabled = true,
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
