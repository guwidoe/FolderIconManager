using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FolderIconManager.GUI;

/// <summary>
/// Converts log entry text to appropriate color based on log level prefix
/// </summary>
public class LogLevelToBrushConverter : IValueConverter
{
    // Static frozen brushes for performance
    private static readonly Brush RedBrush;
    private static readonly Brush YellowBrush;
    private static readonly Brush GreenBrush;
    private static readonly Brush DimGrayBrush;
    private static readonly Brush DefaultBrush;

    static LogLevelToBrushConverter()
    {
        RedBrush = new SolidColorBrush(Color.FromRgb(241, 76, 76));
        RedBrush.Freeze();
        YellowBrush = new SolidColorBrush(Color.FromRgb(220, 220, 170));
        YellowBrush.Freeze();
        GreenBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176));
        GreenBrush.Freeze();
        DimGrayBrush = new SolidColorBrush(Color.FromRgb(133, 133, 133));
        DimGrayBrush.Freeze();
        DefaultBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
        DefaultBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
            return DefaultBrush;

        if (text.Contains("[ERR]"))
            return RedBrush;

        if (text.Contains("[WRN]"))
            return YellowBrush;

        if (text.Contains("[OK ]"))
            return GreenBrush;

        if (text.Contains("[DBG]"))
            return DimGrayBrush;

        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to Visibility
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}

/// <summary>
/// Converts depth limit (int?) to display text
/// </summary>
public class DepthLimitConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int depth)
            return $"{depth} levels";
        return "Unlimited";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts boolean value
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}
