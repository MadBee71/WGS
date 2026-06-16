using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WGS.Games;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace WGS.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is not Visibility.Visible;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is not true;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is not true;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class NullToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v == null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class StringToColorBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        if (v is string hex)
        {
            try { return new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex)); }
            catch { }
        }
        return WpfBrushes.Gray;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class ZeroToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class FieldTypeToPasswordVisConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is ConfigFieldType ft && ft == ConfigFieldType.Password ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class FieldTypeToTextVisConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is ConfigFieldType ft && ft == ConfigFieldType.Password ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>
/// Palauttaa GridLength "*" kun bool on true, "0" kun false.
/// Käytetään editoripaneelin leveyden toggle-näyttöön ilman erillistä näkyvyys-saraketta.
/// </summary>
public class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
