using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace DictateForWindows.Controls;

/// <summary>
/// A circular prompt button displayed in the arc above the orb.
/// </summary>
public sealed partial class PromptLens : UserControl
{
    public static readonly DependencyProperty PromptNameProperty =
        DependencyProperty.Register(nameof(PromptName), typeof(string), typeof(PromptLens),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TooltipProperty =
        DependencyProperty.Register(nameof(Tooltip), typeof(string), typeof(PromptLens),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(PromptLens),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(PromptLens),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(PromptLens),
            new PropertyMetadata(null));

    public string PromptName
    {
        get => (string)GetValue(PromptNameProperty);
        set => SetValue(PromptNameProperty, value);
    }

    public string Tooltip
    {
        get => (string)GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

    public PromptLens()
    {
        InitializeComponent();
    }

    private void OnLensClick(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var lens = (PromptLens)d;
        var selected = (bool)e.NewValue;

        if (selected)
        {
            // Green background + white text for active prompt
            lens.LensButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 16, 185, 129)); // #10B981
            lens.LensButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 255, 255));
            lens.LensButton.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 16, 185, 129));
            lens.LensButton.BorderThickness = new Thickness(1);
            lens.LensText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }
        else
        {
            // Default dark background
            lens.LensButton.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PromptLensBackgroundBrush"];
            lens.LensButton.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["OrbTextPrimaryBrush"];
            lens.LensButton.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(25, 255, 255, 255));
            lens.LensButton.BorderThickness = new Thickness(1);
            lens.LensText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["OrbTextPrimaryBrush"];
        }
    }
}
