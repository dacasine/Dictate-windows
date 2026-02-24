using DictateForWindows.Core.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace DictateForWindows.Controls;

/// <summary>
/// Renders four annular (ring) sectors around the orb with curved text labels.
/// Each sector corresponds to a navigation direction: Up=Prompts, Right=Actions, Down=Context, Left=Settings.
/// </summary>
public sealed partial class AnnularHints : UserControl
{
    private const double CanvasSize = 280;
    private const double Center = CanvasSize / 2; // 140
    private const double InnerRadius = 65;   // 60 (orb radius) + 5px gap
    private const double OuterRadius = 88;

    // === Continuous ring (no gaps between sectors) ===
    private const double GapDeg = 0;
    private const double SegmentSpan = 90;
    private const double TextRadius = 76.5; // true midpoint for centered text

    // === Previous version: separated sectors ===
    // private const double GapDeg = 6;        // ~7px at inner edge, matching the orb gap
    // private const double SegmentSpan = 90 - GapDeg; // 84°
    // private const double TextRadius = 78;   // slightly outward from midpoint (76.5)

    // Brushes — match the main orb circle (#0A0A0A at 85%, text #F5F5F5)
    private static readonly SolidColorBrush DefaultFill = new(Windows.UI.Color.FromArgb(0xD9, 0x0A, 0x0A, 0x0A));
    private static readonly SolidColorBrush SegmentStroke = new(Windows.UI.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush SubtleText = new(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5));

    // Segment definitions: label, center angle (0=right, 90=down, 180=left, 270=up), isPromptSector
    private record SegmentDef(string Label, double CenterAngle, bool IsPrompt);
    private static readonly SegmentDef[] Segments =
    [
        new("Default", 270, true),    // Up
        new("Actions", 0, false),      // Right
        new("Context", 90, false),     // Down
        new("Settings", 180, false),   // Left
    ];

    public static readonly DependencyProperty PromptLabelProperty =
        DependencyProperty.Register(nameof(PromptLabel), typeof(string), typeof(AnnularHints),
            new PropertyMetadata("Default", OnPromptLabelChanged));

    public string PromptLabel
    {
        get => (string)GetValue(PromptLabelProperty);
        set => SetValue(PromptLabelProperty, value);
    }

    private static void OnPromptLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AnnularHints)d).Rebuild();
    }

    public AnnularHints()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        RootCanvas.Children.Clear();

        foreach (var seg in Segments)
        {
            double halfSpan = SegmentSpan / 2.0;
            double startAngle = seg.CenterAngle - halfSpan;
            double endAngle = seg.CenterAngle + halfSpan;

            var fill = DefaultFill;
            var textBrush = SubtleText;
            var label = seg.IsPrompt ? PromptLabel : seg.Label;

            // Draw the annular sector path
            var path = CreateAnnularSector(startAngle, endAngle, InnerRadius, OuterRadius, fill);
            RootCanvas.Children.Add(path);

            // Place curved text along the mid-radius arc
            PlaceCurvedText(label, seg.CenterAngle, TextRadius, textBrush, 10);
        }
    }

    /// <summary>
    /// Creates a Path representing an annular sector (ring slice) between two angles.
    /// </summary>
    private static Microsoft.UI.Xaml.Shapes.Path CreateAnnularSector(double startDeg, double endDeg, double innerR, double outerR, Brush fill)
    {
        double startRad = startDeg * Math.PI / 180;
        double endRad = endDeg * Math.PI / 180;

        var outerStart = new Point(Center + outerR * Math.Cos(startRad), Center + outerR * Math.Sin(startRad));
        var outerEnd = new Point(Center + outerR * Math.Cos(endRad), Center + outerR * Math.Sin(endRad));
        var innerEnd = new Point(Center + innerR * Math.Cos(endRad), Center + innerR * Math.Sin(endRad));
        var innerStart = new Point(Center + innerR * Math.Cos(startRad), Center + innerR * Math.Sin(startRad));

        bool isLargeArc = Math.Abs(endDeg - startDeg) > 180;

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
        figure.Segments.Add(new ArcSegment
        {
            Point = outerEnd,
            Size = new Size(outerR, outerR),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = isLargeArc
        });
        figure.Segments.Add(new LineSegment { Point = innerEnd });
        figure.Segments.Add(new ArcSegment
        {
            Point = innerStart,
            Size = new Size(innerR, innerR),
            SweepDirection = SweepDirection.Counterclockwise,
            IsLargeArc = isLargeArc
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Fill = fill,
            Stroke = SegmentStroke,
            StrokeThickness = 1
        };
    }

    /// <summary>
    /// Places individual characters along a circular arc, matching CurvedTextBlock's orientation logic.
    /// Top-half text reads left-to-right with character tops pointing outward.
    /// Bottom-half text reads left-to-right when viewed from outside the circle.
    /// </summary>
    private void PlaceCurvedText(string text, double centerAngleDeg, double radius, Brush foreground, double fontSize)
    {
        if (string.IsNullOrEmpty(text)) return;

        double letterSpacing = 0.55;
        double charAngle = (fontSize * letterSpacing) / radius * (180.0 / Math.PI);
        double totalAngle = charAngle * (text.Length - 1);
        double startAngle = centerAngleDeg - totalAngle / 2.0;

        // Same orientation logic as CurvedTextBlock
        double normalizedAngle = ((centerAngleDeg % 360) + 360) % 360;
        bool isBottomArc = normalizedAngle > 0 && normalizedAngle < 180;
        double rotOffset = isBottomArc ? -90 : 90;

        for (int i = 0; i < text.Length; i++)
        {
            double angleDeg = startAngle + i * charAngle;
            double angleRad = angleDeg * Math.PI / 180.0;

            double x = Center + radius * Math.Cos(angleRad);
            double y = Center + radius * Math.Sin(angleRad);

            // Reverse character order for bottom arc (so text reads L-R from outside)
            int charIdx = isBottomArc ? text.Length - 1 - i : i;

            var tb = new TextBlock
            {
                Text = text[charIdx].ToString(),
                FontSize = fontSize,
                Foreground = foreground,
                FontFamily = new FontFamily(GetOrbFont()),
                FontWeight = FontWeights.SemiBold,
                RenderTransformOrigin = new Point(0.5, 0.5),
                TextAlignment = TextAlignment.Center,
                Width = fontSize,
                RenderTransform = new RotateTransform { Angle = angleDeg + rotOffset }
            };

            Canvas.SetLeft(tb, x - fontSize / 2.0);
            Canvas.SetTop(tb, y - fontSize / 2.0);

            RootCanvas.Children.Add(tb);
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
