using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DictateForWindows.Controls;

/// <summary>
/// Panel displaying the captured selection from the target window.
/// </summary>
public sealed partial class ContextPanel : UserControl
{
    public static readonly DependencyProperty ContextStringProperty =
        DependencyProperty.Register(nameof(ContextString), typeof(string), typeof(ContextPanel),
            new PropertyMetadata(string.Empty));

    public string ContextString
    {
        get => (string)GetValue(ContextStringProperty);
        set => SetValue(ContextStringProperty, value);
    }

    public ContextPanel()
    {
        InitializeComponent();
    }
}
