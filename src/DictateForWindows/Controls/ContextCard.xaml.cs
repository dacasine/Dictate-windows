using DictateForWindows.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Windows.Input;

namespace DictateForWindows.Controls;

public sealed partial class ContextCard : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ContextSource), typeof(ContextCard),
            new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ContextCard),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ContextCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(ContextCard),
            new PropertyMetadata(null));

    public ContextSource? Source
    {
        get => (ContextSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
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

    public ContextCard()
    {
        InitializeComponent();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (ContextCard)d;
        var source = e.NewValue as ContextSource;
        card.UpdateVisuals(source);
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (ContextCard)d;
        card.UpdateBorderColor();
    }

    private void UpdateVisuals(ContextSource? source)
    {
        if (source == null)
        {
            HeaderText.Text = "Empty";
            PreviewText.Text = "";
            CardIcon.Glyph = "\uE8A7";
            LoadingRing.IsActive = false;
            return;
        }

        CardIcon.Glyph = source.Type == ContextSourceType.Clipboard ? "\uE8C8" : "\uE722";
        HeaderText.Text = source.Type == ContextSourceType.Clipboard ? "Clipboard" : "Screenshot";
        LoadingRing.IsActive = source.IsLoading;

        if (source.IsLoading)
        {
            PreviewText.Text = "Loading...";
        }
        else if (source.HasContent)
        {
            var text = source.Text;
            PreviewText.Text = text.Length > 50 ? text[..50] + "..." : text;
        }
        else
        {
            PreviewText.Text = source.Type == ContextSourceType.Clipboard ? "No text copied" : "No capture";
        }

        UpdateBorderColor();
    }

    private void UpdateBorderColor()
    {
        var source = Source;
        if (source == null)
        {
            CardBorder.BorderBrush = new SolidColorBrush(Colors.Gray);
            return;
        }

        if (IsSelected)
        {
            CardBorder.BorderBrush = new SolidColorBrush(
                source.IsActive
                    ? ColorHelper.FromArgb(255, 16, 185, 129) // green
                    : ColorHelper.FromArgb(255, 239, 68, 68)); // red
        }
        else
        {
            CardBorder.BorderBrush = new SolidColorBrush(
                source.HasContent
                    ? ColorHelper.FromArgb(100, 16, 185, 129) // subtle green
                    : Colors.Gray);
        }
    }

    private void OnCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}
