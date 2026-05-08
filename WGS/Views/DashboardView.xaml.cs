using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WGS.Views;

public partial class DashboardView : System.Windows.Controls.UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }
}

/// <summary>Converts a 0-100 percent value to a WPF SolidColorBrush: green / yellow / red.</summary>
public class PercentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = 0;
        try { pct = System.Convert.ToDouble(value); } catch { }

        var color = pct >= 90 ? System.Windows.Media.Color.FromRgb(0xF8, 0x51, 0x49)   // red
                  : pct >= 70 ? System.Windows.Media.Color.FromRgb(0xD2, 0x99, 0x22)   // yellow
                              : System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50);  // green

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
