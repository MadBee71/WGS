using System.Windows;
using WGS.Models;
using WGS.Services;

namespace WGS.Views;

public partial class AddScheduleTaskDialog : Window
{
    public ScheduledTask? Result { get; private set; }

    public AddScheduleTaskDialog(IEnumerable<QuickCommand>? quickCommands = null)
    {
        InitializeComponent();
        if (quickCommands != null)
            foreach (var qc in quickCommands) CbQuickCommand.Items.Add(qc);
    }

    private void CbAction_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CommandPanel != null)
            CommandPanel.Visibility = CbAction.SelectedIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CbQuickCommand_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CbQuickCommand.SelectedItem is QuickCommand qc)
            TbCommand.Text = qc.Command;
    }

    private void CbFreq_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DayPanel == null || IntervalPanel == null || TimePanel == null) return;
        DayPanel.Visibility      = CbFreq.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        IntervalPanel.Visibility = CbFreq.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        TimePanel.Visibility     = CbFreq.SelectedIndex == 2 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var action = (ScheduledActionType)CbAction.SelectedIndex;

        if (action == ScheduledActionType.QuickCommand && string.IsNullOrWhiteSpace(TbCommand.Text))
        {
            System.Windows.MessageBox.Show("Enter a console command to send.", "Error");
            return;
        }

        var freq = CbFreq.SelectedIndex switch
        {
            1 => ScheduleFrequency.Weekly,
            2 => ScheduleFrequency.Interval,
            _ => ScheduleFrequency.Daily,
        };

        var time = TimeSpan.Zero;
        if (freq != ScheduleFrequency.Interval && !TimeSpan.TryParse(TbTime.Text, out time))
        {
            System.Windows.MessageBox.Show("Invalid time format. Use HH:mm", "Error");
            return;
        }

        var intervalMinutes = 60;
        if (freq == ScheduleFrequency.Interval)
        {
            if (!int.TryParse(TbInterval.Text, out intervalMinutes) || intervalMinutes <= 0)
            {
                System.Windows.MessageBox.Show("Enter a positive number for the repeat interval.", "Error");
                return;
            }
            if (CbIntervalUnit.SelectedIndex == 1) intervalMinutes *= 60; // Hours -> minutes
        }

        var day = (DayOfWeek)(CbDay.SelectedIndex + 1); // Monday=1

        Result = new ScheduledTask
        {
            Action          = action,
            Command         = action == ScheduledActionType.QuickCommand ? TbCommand.Text.Trim() : string.Empty,
            Frequency       = freq,
            TimeOfDay       = time,
            DayOfWeek       = freq == ScheduleFrequency.Weekly ? day : DayOfWeek.Monday,
            IntervalMinutes = intervalMinutes,
            IsEnabled       = true,
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
