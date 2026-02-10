using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Windows.Input;

namespace DictateForWindows.Controls;

public sealed partial class ModelLens : UserControl
{
    public static readonly DependencyProperty ModelNameProperty =
        DependencyProperty.Register(nameof(ModelName), typeof(string), typeof(ModelLens),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSubtleProperty =
        DependencyProperty.Register(nameof(IsSubtle), typeof(bool), typeof(ModelLens),
            new PropertyMetadata(true, OnIsSubtleChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ModelLens),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ModelLens),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(ModelLens),
            new PropertyMetadata(null));

    public string ModelName
    {
        get => (string)GetValue(ModelNameProperty);
        set => SetValue(ModelNameProperty, value);
    }

    public bool IsSubtle
    {
        get => (bool)GetValue(IsSubtleProperty);
        set => SetValue(IsSubtleProperty, value);
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

    public ModelLens()
    {
        InitializeComponent();
        UpdateOpacity();
    }

    private void OnLensClick(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    private static void OnIsSubtleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ModelLens)d).UpdateOpacity();
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var lens = (ModelLens)d;
        var selected = (bool)e.NewValue;

        if (selected)
        {
            lens.LensButton.RenderTransform = new ScaleTransform
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

        lens.UpdateOpacity();
    }

    private void UpdateOpacity()
    {
        Opacity = IsSelected ? 1.0 : (IsSubtle ? 0.35 : 1.0);
    }
}
