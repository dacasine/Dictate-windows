namespace DictateForWindows.Core.Models;

/// <summary>
/// Result of emotional analysis on speech, derived from prosodic features.
/// Detects per-segment emotions and provides session-level emotional summary.
/// </summary>
public class EmotionResult
{
    /// <summary>Per-segment emotion tags aligned to transcription segments.</summary>
    public List<EmotionSegment> Segments { get; set; } = new();

    /// <summary>The dominant emotion across the entire session.</summary>
    public EmotionTag DominantEmotion { get; set; } = EmotionTag.Neutral;

    /// <summary>Confidence in the dominant emotion (0-1).</summary>
    public float DominantConfidence { get; set; }

    /// <summary>Overall emotional valence: -1 (negative) to +1 (positive).</summary>
    public float Valence { get; set; }

    /// <summary>Overall arousal level: 0 (calm) to 1 (intense).</summary>
    public float Arousal { get; set; }

    /// <summary>
    /// True if a strong negative emotion was detected and the user should be warned
    /// before sending (e.g., "You dictated this while frustrated — review before sending?").
    /// </summary>
    public bool ShouldWarn { get; set; }

    /// <summary>Warning message if ShouldWarn is true.</summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// Build a compact emotion summary for metadata/footer.
    /// </summary>
    public string BuildSummaryFooter()
    {
        var parts = new List<string>
        {
            $"Mood: {DominantEmotion} ({DominantConfidence:P0})"
        };

        if (Valence < -0.3f)
            parts.Add($"Valence: negative ({Valence:F2})");
        else if (Valence > 0.3f)
            parts.Add($"Valence: positive ({Valence:F2})");

        if (Arousal > 0.7f)
            parts.Add("Energy: high");

        var emotionCounts = Segments
            .GroupBy(s => s.Emotion)
            .Where(g => g.Key != EmotionTag.Neutral)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}({g.Count()})");

        var arc = string.Join(" → ", emotionCounts);
        if (!string.IsNullOrEmpty(arc))
            parts.Add($"Arc: {arc}");

        return $"\n[Emotion] {string.Join(" | ", parts)}";
    }
}

/// <summary>
/// Emotion tag for a single segment of speech.
/// </summary>
public class EmotionSegment
{
    /// <summary>Start time in seconds.</summary>
    public double StartTime { get; set; }

    /// <summary>End time in seconds.</summary>
    public double EndTime { get; set; }

    /// <summary>Detected emotion for this segment.</summary>
    public EmotionTag Emotion { get; set; }

    /// <summary>Confidence of the emotion classification (0-1).</summary>
    public float Confidence { get; set; }

    /// <summary>The text of this segment (if available).</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Basic emotion categories derived from prosodic features.
/// Based on dimensional model (valence + arousal) mapped to discrete labels.
/// </summary>
public enum EmotionTag
{
    /// <summary>Baseline — no strong emotional signal.</summary>
    Neutral,

    /// <summary>High pitch + high energy + fast rate → anger/frustration.</summary>
    Angry,

    /// <summary>Steady pitch + moderate energy + steady rate → confidence.</summary>
    Confident,

    /// <summary>Low energy + variable pitch + slow rate → uncertainty/doubt.</summary>
    Uncertain,

    /// <summary>High energy + rising pitch + fast rate → excitement/enthusiasm.</summary>
    Excited,

    /// <summary>Low energy + steady pitch + slow rate → calm/relaxed.</summary>
    Calm,

    /// <summary>Very low energy + falling pitch + slow rate → sadness/fatigue.</summary>
    Sad,

    /// <summary>High pitch variability + high energy + irregular rate → stress.</summary>
    Stressed
}
