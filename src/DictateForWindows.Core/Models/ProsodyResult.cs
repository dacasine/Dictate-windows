namespace DictateForWindows.Core.Models;

/// <summary>
/// Result of prosodic analysis on an audio recording.
/// Contains per-window features (pitch, energy, speech rate) and detected pauses.
/// </summary>
public class ProsodyResult
{
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Per-window prosodic features (50ms windows, 25ms overlap).
    /// </summary>
    public List<ProsodySegment> Segments { get; set; } = new();

    /// <summary>
    /// Speaker's median pitch across all voiced segments.
    /// </summary>
    public float BaselinePitchHz { get; set; }

    /// <summary>
    /// Speaker's median energy across all voiced segments.
    /// </summary>
    public float BaselineEnergyDb { get; set; }

    /// <summary>
    /// Detected pauses (silence gaps above threshold).
    /// </summary>
    public List<PauseEvent> Pauses { get; set; } = new();

    public string? Error { get; set; }

    public static ProsodyResult Success(List<ProsodySegment> segments, List<PauseEvent> pauses,
        float baselinePitch, float baselineEnergy) => new()
    {
        IsSuccess = true,
        Segments = segments,
        Pauses = pauses,
        BaselinePitchHz = baselinePitch,
        BaselineEnergyDb = baselineEnergy
    };

    public static ProsodyResult Failure(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}

/// <summary>
/// Prosodic features for a single analysis window of audio.
/// </summary>
public class ProsodySegment
{
    /// <summary>Start time in seconds.</summary>
    public double StartTime { get; set; }

    /// <summary>End time in seconds.</summary>
    public double EndTime { get; set; }

    /// <summary>Fundamental frequency in Hz (0 if unvoiced/silence).</summary>
    public float PitchHz { get; set; }

    /// <summary>Pitch change vs. speaker baseline (-1 to +1 normalized).</summary>
    public float PitchDelta { get; set; }

    /// <summary>RMS energy in dB (relative to full scale).</summary>
    public float EnergyDb { get; set; }

    /// <summary>Energy change vs. speaker baseline in dB.</summary>
    public float EnergyDelta { get; set; }

    /// <summary>Whether this window is below the silence threshold.</summary>
    public bool IsSilence { get; set; }

    /// <summary>Whether this window is voiced but very low energy (whisper).</summary>
    public bool IsWhisper { get; set; }
}

/// <summary>
/// A detected pause (silence gap) in the audio.
/// </summary>
public class PauseEvent
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }

    /// <summary>Pause duration in milliseconds.</summary>
    public double DurationMs => (EndTime - StartTime) * 1000;
}
