using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FolderIconManager.GUI;

/// <summary>
/// Converts log entry text to appropriate color based on log level prefix
/// </summary>
public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
            return new SolidColorBrush(Color.FromRgb(204, 204, 204)); // Default gray

        if (text.Contains("[ERR]"))
            return new SolidColorBrush(Color.FromRgb(241, 76, 76));   // Red

        if (text.Contains("[WRN]"))
            return new SolidColorBrush(Color.FromRgb(220, 220, 170)); // Yellow

        if (text.Contains("[OK ]"))
            return new SolidColorBrush(Color.FromRgb(78, 201, 176));  // Green

        if (text.Contains("[DBG]"))
            return new SolidColorBrush(Color.FromRgb(133, 133, 133)); // Dim gray

        return new SolidColorBrush(Color.FromRgb(204, 204, 204));     // Default
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

