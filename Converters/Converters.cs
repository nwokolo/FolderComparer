using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FolderComparer.Models;

namespace FolderComparer.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ComparisonStatus status)
        {
            return status switch
            {
                ComparisonStatus.Match => new SolidColorBrush(Color.FromRgb(34, 139, 34)),       // Green
                ComparisonStatus.SourceOnly => new SolidColorBrush(Color.FromRgb(30, 144, 255)), // Blue
                ComparisonStatus.DestinationOnly => new SolidColorBrush(Color.FromRgb(255, 140, 0)), // Orange
                ComparisonStatus.SizeMismatch or
                ComparisonStatus.DateMismatch or
                ComparisonStatus.SizeAndDateMismatch => new SolidColorBrush(Color.FromRgb(220, 20, 60)), // Red
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long size)
        {
            return size switch
            {
                < 1024 => $"{size} B",
                < 1024 * 1024 => $"{size / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{size / (1024.0 * 1024):F1} MB",
                _ => $"{size / (1024.0 * 1024 * 1024):F2} GB"
            };
        }
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
