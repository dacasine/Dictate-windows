using DictateForWindows.Core.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DictateForWindows.Controls;

/// <summary>
/// Renders text along a circular arc path.
/// </summary>
public sealed class CurvedTextBlock : Canvas
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(CurvedTextBlock),
            new PropertyMetadata(string.Empty, OnPropertyChanged));

    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(CurvedTextBlock),
            new PropertyMetadata(50.0, OnPropertyChanged));

    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(CurvedTextBlock),
            new PropertyMetadata(-90.0, OnPropertyChanged));

    public static readonly DependencyProperty IsClockwiseProperty =
        DependencyProperty.Register(nameof(IsClockwise), typeof(bool), typeof(CurvedTextBlock),
            new PropertyMetadata(true, OnPropertyChanged));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(CurvedTextBlock),
            new PropertyMetadata(12.0, OnPropertyChanged));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(CurvedTextBlock),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty LetterSpacingProperty =
        DependencyProperty.Register(nameof(LetterSpacing), typeof(double), typeof(CurvedTextBlock),
            new PropertyMetadata(0.55, OnPropertyChanged));

    public static readonly DependencyProperty FontFamilyNameProperty =
        DependencyProperty.Register(nameof(FontFamilyName), typeof(string), typeof(CurvedTextBlock),
            new PropertyMetadata(null, OnPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>
    /// Center angle of the text arc in degrees. 0 = right, -90 = top, 90 = bottom.
    /// </summary>
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public bool IsClockwise
    {
        get => (bool)GetValue(IsClockwiseProperty);
        set => SetValue(IsClockwiseProperty, value);
    }

    public new double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Angular spacing multiplier per character. Higher = more spread.
    /// </summary>
    public double LetterSpacing
    {
        get => (double)GetValue(LetterSpacingProperty);
        set => SetValue(LetterSpacingProperty, value);
    }

    public string FontFamilyName
    {
        get => (string)GetValue(FontFamilyNameProperty);
        set => SetValue(FontFamilyNameProperty, value);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CurvedTextBlock)d).Render();
    }

    private void Render()
    {
        Children.Clear();

        var text = Text;
        if (string.IsNullOrEmpty(text)) return;

        var foreground = Foreground ?? new SolidColorBrush(Microsoft.UI.Colors.White);

        // Approximate character angular width based on font size and radius
        double charAngle = (FontSize * LetterSpacing) / Radius * (180.0 / Math.PI);

        // Total angular span of the text
        double totalAngle = charAngle * (text.Length - 1);

        // Center the text on StartAngle
        double startDeg = StartAngle - totalAngle / 2.0;

        // Top text (around -90°/270°): characters face outward → +90
        // Bottom text (around 90°): characters face outward → -90
        double normalizedAngle = ((StartAngle % 360) + 360) % 360;
        double rotationOffset = (normalizedAngle > 0 && normalizedAngle < 180) ? -90 : 90;

        // Bottom arc renders characters right-to-left visually when placed clockwise,
        // so reverse the character order to display them correctly.
        bool isBottomArc = normalizedAngle > 0 && normalizedAngle < 180;

        for (int i = 0; i < text.Length; i++)
        {
            double angleDeg = IsClockwise
                ? startDeg + i * charAngle
                : startDeg - i * charAngle;
            double angleRad = angleDeg * Math.PI / 180.0;

            // Position on circle (canvas center = 0,0 so we use Width/Height / 2)
            double cx = Width / 2.0;
            double cy = Height / 2.0;

            double x = cx + Radius * Math.Cos(angleRad);
            double y = cy + Radius * Math.Sin(angleRad);

            int charIndex = isBottomArc ? text.Length - 1 - i : i;

            var tb = new TextBlock
            {
                Text = text[charIndex].ToString(),
                FontSize = FontSize,
                FontFamily = new FontFamily(FontFamilyName ?? GetOrbFont()),
                Foreground = foreground,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
                Width = FontSize,
                RenderTransform = new RotateTransform { Angle = angleDeg + rotationOffset }
            };

            Canvas.SetLeft(tb, x - FontSize / 2.0);
            Canvas.SetTop(tb, y - FontSize / 2.0);

            Children.Add(tb);
        }
    }

    private static string GetOrbFont()
    {
        try
        {
            var settings = App.Current.Services.GetService<ISettingsService>();
            return settings?.OrbFont ?? Core.Constants.SettingsDefaults.OrbFont;
        }
        catch
        {
            return Core.Constants.SettingsDefaults.OrbFont;
        }
    }
}
