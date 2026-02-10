using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Windows.Input;

namespace DictateForWindows.Controls;

public sealed partial class TargetAppCard : UserControl
{
    public static readonly DependencyProperty AppNameProperty =
        DependencyProperty.Register(nameof(AppName), typeof(string), typeof(TargetAppCard),
            new PropertyMetadata(string.Empty, OnAppNameChanged));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(TargetAppCard),
            new PropertyMetadata("\uE71E", OnIconGlyphChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(TargetAppCard),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(TargetAppCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(TargetAppCard),
            new PropertyMetadata(null));

    public string AppName
    {
        get => (string)GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
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

    public TargetAppCard()
    {
        InitializeComponent();
    }

    private static void OnAppNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TargetAppCard)d).AppNameText.Text = (string)e.NewValue;
    }

    private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TargetAppCard)d).AppIcon.Glyph = (string)e.NewValue;
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (TargetAppCard)d;
        var selected = (bool)e.NewValue;

        if (selected)
        {
            card.AppButton.RenderTransform = new ScaleTransform
            {
                ScaleX = 1.1,
                ScaleY = 1.1,
                CenterX = 32,
                CenterY = 32
            };
            card.Opacity = 1.0;
        }
        else
        {
            card.AppButton.RenderTransform = null;
            card.Opacity = 0.7;
        }
    }

    private void OnAppClick(object sender, RoutedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}
