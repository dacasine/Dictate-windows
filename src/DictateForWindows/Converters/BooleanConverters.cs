using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DictateForWindows.Converters;

/// <summary>
/// Converts a boolean to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            // If parameter is "Invert", reverse the logic
            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = !boolValue;
            }
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            var result = visibility == Visibility.Visible;
            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }
            return result;
        }
        return false;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Converts null to boolean (null = false, non-null = true).
/// </summary>
public class NullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNull = value == null;
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            return isNull;
        }
        return !isNull;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to Visibility (null = Collapsed, non-null = Visible).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNull = value == null;
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
