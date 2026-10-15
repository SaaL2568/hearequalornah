namespace HearEqualOrNah.UI.Converters;

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return (b ^ Inverse) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isEmpty = value == null || (value is string s && s.Length == 0);
        return isEmpty ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class DoubleToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"{d:P0}";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class DoubleToDbConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (double.IsNegativeInfinity(d))
                return "-- dB";
            return $"{d:F1} dB";
        }
        return "-- dB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class TestStateToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HearEqualOrNah.Core.Models.TestState state)
        {
            return state switch
            {
                HearEqualOrNah.Core.Models.TestState.Idle => "Ready to test",
                HearEqualOrNah.Core.Models.TestState.Preparing => "Preparing...",
                HearEqualOrNah.Core.Models.TestState.MeasuringNoise => "Measuring background noise...",
                HearEqualOrNah.Core.Models.TestState.TestingLeft => "Testing LEFT channel...",
                HearEqualOrNah.Core.Models.TestState.TestingRight => "Testing RIGHT channel...",
                HearEqualOrNah.Core.Models.TestState.Completed => "Test completed",
                HearEqualOrNah.Core.Models.TestState.Error => "Error occurred",
                _ => "Unknown"
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class MeasurementQualityToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HearEqualOrNah.Core.Models.MeasurementQuality quality)
        {
            return quality switch
            {
                HearEqualOrNah.Core.Models.MeasurementQuality.Good => "Good",
                HearEqualOrNah.Core.Models.MeasurementQuality.Noisy => "Noisy",
                HearEqualOrNah.Core.Models.MeasurementQuality.Clipping => "Clipping",
                HearEqualOrNah.Core.Models.MeasurementQuality.Unstable => "Unstable",
                _ => "Unknown"
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class TestStateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HearEqualOrNah.Core.Models.TestState state && parameter is string states)
        {
            foreach (var name in states.Split('|'))
            {
                if (Enum.TryParse<HearEqualOrNah.Core.Models.TestState>(name.Trim(), out var expected) && expected == state)
                    return System.Windows.Visibility.Visible;
            }
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class ClassificationToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string classification)
        {
            if (classification.Contains("Excellent", StringComparison.OrdinalIgnoreCase) ||
                classification.Contains("Good", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF3EC"));
            if (classification.Contains("Slight", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBF3DB"));
            if (classification.Contains("Significant", StringComparison.OrdinalIgnoreCase) ||
                classification.Contains("Strong", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEBEC"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1F3FE"));
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class ClassificationToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string classification)
        {
            if (classification.Contains("Excellent", StringComparison.OrdinalIgnoreCase) ||
                classification.Contains("Good", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#346538"));
            if (classification.Contains("Slight", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#956400"));
            if (classification.Contains("Significant", StringComparison.OrdinalIgnoreCase) ||
                classification.Contains("Strong", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9F2F2D"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F6C9F"));
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class UtcToLocalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class UnsupportedBalanceToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HearEqualOrNah.Core.Models.WindowsChannelBalance wb && !wb.IsSupported)
        {
            return System.Windows.Visibility.Visible;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}