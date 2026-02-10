using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace DictateForWindows.Controls;

/// <summary>
/// Custom record button with pulsing animation.
/// </summary>
public sealed partial class RecordButton : UserControl
{
    public static readonly DependencyProperty IsRecordingProperty =
        DependencyProperty.Register(nameof(IsRecording), typeof(bool), typeof(RecordButton),
            new PropertyMetadata(false, OnIsRecordingChanged));

    public static readonly DependencyProperty IsPausedProperty =
        DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(RecordButton),
            new PropertyMetadata(false, OnIsPausedChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(RecordButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(RecordButton),
            new PropertyMetadata(null));

    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    public bool IsPaused
    {
        get => (bool)GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler? Click;

    public RecordButton()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, EventArgs.Empty);

        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((RecordButton)d).UpdateVisualState();
    }

    private static void OnIsPausedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((RecordButton)d).UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (IsPaused)
        {
            VisualStateManager.GoToState(this, "Paused", true);
        }
        else if (IsRecording)
        {
            VisualStateManager.GoToState(this, "Recording", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "Idle", true);
        }
    }
}
