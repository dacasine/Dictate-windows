using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using DictateForWindows.Core.Models;

namespace DictateForWindows.Converters;

/// <summary>
/// Converts RecordingState to a visibility based on whether recording is active.
/// </summary>
public class RecordingStateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is RecordingState state)
        {
            var isRecording = state == RecordingState.Recording || state == RecordingState.Paused;

            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                return isRecording ? Visibility.Collapsed : Visibility.Visible;
            }
            return isRecording ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts RecordingState to boolean (true if recording/paused).
/// </summary>
public class RecordingStateToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is RecordingState state)
        {
            var result = state == RecordingState.Recording || state == RecordingState.Paused;

            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                return !result;
            }
            return result;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts RecordingState to a status string.
/// </summary>
public class RecordingStateToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is RecordingState state)
        {
            return state switch
            {
                RecordingState.Idle => "Ready",
                RecordingState.Recording => "Recording...",
                RecordingState.Paused => "Paused",
                RecordingState.Processing => "Processing...",
                RecordingState.Transcribing => "Transcribing...",
                RecordingState.Rewording => "Rewording...",
                RecordingState.Injecting => "Typing...",
                RecordingState.Done => "Done",
                RecordingState.Error => "Error",
                RecordingState.Cancelled => "Cancelled",
                _ => "Unknown"
            };
        }
        return "Ready";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Compares enum value to parameter for equality.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return enumValue?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Shows visibility only when enum matches parameter.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null)
        {
            return Visibility.Collapsed;
        }

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        var matches = enumValue?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false;
        return matches ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
