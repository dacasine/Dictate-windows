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

        // Scale up when selected
        if (selected)
        {
            lens.LensButton.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform
            {
                ScaleX = 1.15,
                ScaleY = 1.15,
                CenterX = lens.ActualWidth / 2,
                CenterY = lens.ActualHeight / 2
            };
        }
        else
        {
            lens.LensButton.RenderTransform = null;
        }
    }
}
