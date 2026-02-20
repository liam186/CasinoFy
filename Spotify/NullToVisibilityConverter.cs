using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Spotify
{
    /// <summary>
    /// Returns Collapsed when the value is non-null, Visible when null.
    /// Used to show a placeholder icon when no album cover exists.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
