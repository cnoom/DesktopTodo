using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopTodo.Converters;

/// <summary>
/// null 或空字符串 → Collapsed，否则 Visible
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
