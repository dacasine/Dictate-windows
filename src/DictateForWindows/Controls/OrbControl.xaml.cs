using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DictateForWindows.Helpers;

namespace DictateForWindows.Controls;

/// <summary>
/// The Orb — a living circle that breathes with voice input.
/// </summary>
public sealed partial class OrbControl : UserControl
{
    private bool _isPulseRunning;

    #region Dependency Properties

    public static readonly DependencyProperty AudioLevelProperty =
        DependencyProperty.Register(nameof(AudioLevel), typeof(double), typeof(OrbControl),
            new PropertyMetadata(0.0, OnAudioLevelChanged));

    public static readonly DependencyProperty IsRecordingProperty =
        DependencyProperty.Register(nameof(IsRecording), typeof(bool), typeof(OrbControl),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty IsPausedProperty =
        DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(OrbControl),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty IsProcessingProperty =
        DependencyProperty.Register(nameof(IsProcessing), typeof(bool), typeof(OrbControl),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty TimerProperty =
        DependencyProperty.Register(nameof(Timer), typeof(string), typeof(OrbControl),
            new PropertyMetadata("00:00", OnTimerChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Windows.UI.Color), typeof(OrbControl),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 0, 120, 212), OnAccentColorChanged));

    public double AudioLevel
    {
        get => (double)GetValue(AudioLevelProperty);
        set => SetValue(AudioLevelProperty, value);
    }

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

    public bool IsProcessing
    {
        get => (bool)GetValue(IsProcessingProperty);
        set => SetValue(IsProcessingProperty, value);
    }

    public string Timer
    {
        get => (string)GetValue(TimerProperty);
        set => SetValue(TimerProperty, value);
    }

    public Windows.UI.Color AccentColor
    {
        get => (Windows.UI.Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    #endregion

    public OrbControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Play the appear animation when the orb becomes visible.
    /// </summary>
    public void PlayAppear()
    {
        CompositionHelper.ResetVisual(this);
        CompositionHelper.PlayAppear(this);
    }

    /// <summary>
    /// Play the implosion (confirm) animation.
    /// </summary>
    public void PlayImplosion(Action? onComplete = null)
    {
        StopPulse();
        CompositionHelper.PlayImplosion(this, ShockwaveRing, onComplete);
    }

    /// <summary>
    /// Play the dissolve (cancel) animation.
    /// </summary>
    public void PlayDissolve(Action? onComplete = null)
    {
        StopPulse();
        CompositionHelper.PlayDissolve(this, onComplete);
    }

    private static void OnAudioLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var orb = (OrbControl)d;
        var level = (double)e.NewValue;

        // Scale amplitude ring: 1.0 + level * 0.25 (range 1.0–1.25)
        var scale = 1.0 + level * 0.25;
        orb.AmplitudeScale.ScaleX = scale;
        orb.AmplitudeScale.ScaleY = scale;
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OrbControl)d).UpdateVisualState();
    }

    private static void OnTimerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var orb = (OrbControl)d;
        orb.TimerText.Text = (string)e.NewValue;
    }

    private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var orb = (OrbControl)d;
        var color = (Windows.UI.Color)e.NewValue;
        orb.ApplyAccentColor(color);
    }

    private void ApplyAccentColor(Windows.UI.Color color)
    {
        PulseRingStroke.Color = color;
        MainCircleStroke.Color = color;
        AmplitudeRingStroke.Color = color;
    }

    private void UpdateVisualState()
    {
        if (IsProcessing)
        {
            // Processing: show progress ring, hide icons
            MicIcon.Visibility = Visibility.Collapsed;
            StopIcon.Visibility = Visibility.Collapsed;
            TimerText.Visibility = Visibility.Collapsed;
            ProcessingRing.Visibility = Visibility.Visible;
            ProcessingRing.IsActive = true;
            AmplitudeRing.Opacity = 0;
            StopPulse();

            ApplyAccentColor(Windows.UI.Color.FromArgb(255, 255, 255, 255)); // White
        }
        else if (IsPaused)
        {
            // Paused: mic icon, timer, orange accent
            MicIcon.Visibility = Visibility.Visible;
            MicIcon.Glyph = "\uE768"; // Play icon
            StopIcon.Visibility = Visibility.Collapsed;
            TimerText.Visibility = Visibility.Visible;
            ProcessingRing.Visibility = Visibility.Collapsed;
            ProcessingRing.IsActive = false;
            AmplitudeRing.Opacity = 0.3;
            StopPulse();

            ApplyAccentColor(Windows.UI.Color.FromArgb(255, 255, 140, 0)); // Orange
        }
        else if (IsRecording)
        {
            // Recording: stop icon, timer, red accent, pulse
            MicIcon.Visibility = Visibility.Collapsed;
            StopIcon.Visibility = Visibility.Visible;
            TimerText.Visibility = Visibility.Visible;
            ProcessingRing.Visibility = Visibility.Collapsed;
            ProcessingRing.IsActive = false;
            AmplitudeRing.Opacity = 0.7;
            StartPulse();

            ApplyAccentColor(Windows.UI.Color.FromArgb(255, 232, 17, 35)); // Red
        }
        else
        {
            // Idle: mic icon, blue accent
            MicIcon.Visibility = Visibility.Visible;
            MicIcon.Glyph = "\uE720"; // Mic icon
            StopIcon.Visibility = Visibility.Collapsed;
            TimerText.Visibility = Visibility.Collapsed;
            ProcessingRing.Visibility = Visibility.Collapsed;
            ProcessingRing.IsActive = false;
            AmplitudeRing.Opacity = 0;
            StopPulse();

            ApplyAccentColor(Windows.UI.Color.FromArgb(255, 0, 120, 212)); // Blue
        }
    }

    private void StartPulse()
    {
        if (_isPulseRunning) return;
        _isPulseRunning = true;
        CompositionHelper.StartPulseLoop(PulseRing);
    }

    private void StopPulse()
    {
        if (!_isPulseRunning) return;
        _isPulseRunning = false;
        CompositionHelper.StopAnimations(PulseRing);
        PulseRing.Opacity = 0;
    }
}
