using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DictateForWindows.Converters;

/// <summary>
/// Converts string to Visibility (empty/null = Collapsed, non-empty = Visible).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isEmpty = string.IsNullOrWhiteSpace(value as string);
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            return isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string to boolean (empty/null = false, non-empty = true).
/// </summary>
public class StringToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isEmpty = string.IsNullOrWhiteSpace(value as string);
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            return isEmpty;
        }
        return !isEmpty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Formats a TimeSpan as MM:SS or HH:MM:SS.
/// </summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
        }
        return "0:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Formats milliseconds as MM:SS or HH:MM:SS.
/// </summary>
public class MillisecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        long ms = 0;
        if (value is long longValue)
        {
            ms = longValue;
        }
        else if (value is int intValue)
        {
            ms = intValue;
        }
        else if (value is double doubleValue)
        {
            ms = (long)doubleValue;
        }

        var timeSpan = TimeSpan.FromMilliseconds(ms);
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Formats a decimal as currency.
/// </summary>
public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is decimal decimalValue)
        {
            // Use parameter to specify decimal places (default 4)
            var decimals = 4;
            if (parameter is string paramStr && int.TryParse(paramStr, out var parsed))
            {
                decimals = parsed;
            }
            return $"${decimalValue.ToString($"F{decimals}")}";
        }
        return "$0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
