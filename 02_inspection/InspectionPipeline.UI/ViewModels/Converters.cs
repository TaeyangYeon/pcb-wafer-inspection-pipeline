using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InspectionPipeline.UI.ViewModels;

public class BoolToStartStopConverter : IValueConverter
{
    public static readonly BoolToStartStopConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? "Stop Watcher" : "Start Watcher";
        }
        return "Start Watcher";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToWatcherBrushConverter : IValueConverter
{
    public static readonly BoolToWatcherBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) : new SolidColorBrush(Color.FromRgb(34, 197, 94));
        }
        return new SolidColorBrush(Color.FromRgb(34, 197, 94));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToWatcherStatusConverter : IValueConverter
{
    public static readonly BoolToWatcherStatusConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? "Watching for files..." : "Not watching";
        }
        return "Not watching";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToWatcherStatusBrushConverter : IValueConverter
{
    public static readonly BoolToWatcherStatusBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(156, 163, 175));
        }
        return new SolidColorBrush(Color.FromRgb(156, 163, 175));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}