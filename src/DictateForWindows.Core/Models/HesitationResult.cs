namespace DictateForWindows.Core.Models;

/// <summary>
/// Result of hesitation/fluency analysis on transcribed speech.
/// Flags uncertainty, self-corrections, fatigue, and topic changes.
/// </summary>
public class HesitationResult
{
    public List<HesitationAnnotation> Annotations { get; set; } = new();

    /// <summary>Overall fluency score: 0 = constant hesitation, 1 = perfectly fluent.</summary>
    public float OverallFluencyScore { get; set; }

    /// <summary>Fatigue level: 0 = fresh, 1 = significant rate decline detected.</summary>
    public float FatigueLevel { get; set; }

    /// <summary>Total filler words detected across all languages.</summary>
    public int FillerCount { get; set; }

    /// <summary>Number of self-correction patterns detected.</summary>
    public int SelfCorrectionCount { get; set; }

    /// <summary>
    /// Build a compact summary footer suitable for appending to transcription output.
    /// </summary>
    public string BuildSummaryFooter()
    {
        var parts = new List<string>();

        parts.Add($"Fluency: {OverallFluencyScore:P0}");

        if (FillerCount > 0)
            parts.Add($"Fillers: {FillerCount}");

        if (SelfCorrectionCount > 0)
            parts.Add($"Self-corrections: {SelfCorrectionCount}");

        if (FatigueLevel > 0.3f)
            parts.Add($"Fatigue: {FatigueLevel:P0}");

        var uncertainCount = Annotations.Count(a => a.Type == HesitationType.Uncertainty);
        if (uncertainCount > 0)
            parts.Add($"Uncertain phrases: {uncertainCount}");

        var topicChanges = Annotations.Count(a => a.Type == HesitationType.TopicChange);
        if (topicChanges > 0)
            parts.Add($"Topic shifts: {topicChanges}");

        return $"\n---\n[Hesitation Analysis] {string.Join(" | ", parts)}";
    }
}

/// <summary>
/// A single hesitation annotation attached to a span of transcribed text.
/// </summary>
public class HesitationAnnotation
{
    public HesitationType Type { get; set; }

    /// <summary>Start time in seconds within the audio.</summary>
    public double StartTime { get; set; }

    /// <summary>End time in seconds within the audio.</summary>
    public double EndTime { get; set; }

    /// <summary>The affected text span.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional suggestion (e.g. "You rephrased 3 times — which version?").</summary>
    public string? Suggestion { get; set; }

    /// <summary>Confidence of this annotation: 0 = low, 1 = certain.</summary>
    public float Confidence { get; set; }
}

/// <summary>
/// Categories of hesitation/fluency events.
/// </summary>
public enum HesitationType
{
    /// <summary>"um", "uh", "euh", "genre" — detected filler word.</summary>
    FillerWord,

    /// <summary>High filler density + low energy before a phrase → uncertain content.</summary>
    Uncertainty,

    /// <summary>"I mean", "no wait", "en fait" — speaker corrected themselves.</summary>
    SelfCorrection,

    /// <summary>Sudden prosody shift suggesting a new topic or thought.</summary>
    TopicChange,

    /// <summary>Speech rate declining over session — fatigue warning.</summary>
    FatigueWarning
}
