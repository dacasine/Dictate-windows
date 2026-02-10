namespace DictateForWindows.Core.Models;

/// <summary>
/// Represents the current state of the recording process.
/// </summary>
public enum RecordingState
{
    /// <summary>
    /// Not recording, ready to start.
    /// </summary>
    Idle,

    /// <summary>
    /// Initializing audio capture.
    /// </summary>
    Initializing,

    /// <summary>
    /// Actively recording audio.
    /// </summary>
    Recording,

    /// <summary>
    /// Recording is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// Processing/transcribing the recording.
    /// </summary>
    Processing,

    /// <summary>
    /// Transcribing audio to text.
    /// </summary>
    Transcribing,

    /// <summary>
    /// Applying rewording prompts.
    /// </summary>
    Rewording,

    /// <summary>
    /// Injecting text into the target application.
    /// </summary>
    Injecting,

    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    Done,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error,

    /// <summary>
    /// Operation was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Event args for recording state changes.
/// </summary>
public class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingState OldState { get; }
    public RecordingState NewState { get; }
    public string? ErrorMessage { get; }

    public RecordingStateChangedEventArgs(RecordingState oldState, RecordingState newState, string? errorMessage = null)
    {
        OldState = oldState;
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event args for recording progress updates.
/// </summary>
public class RecordingProgressEventArgs : EventArgs
{
    /// <summary>
    /// Duration of the recording so far.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Current audio level (0.0 to 1.0).
    /// </summary>
    public float AudioLevel { get; }

    public RecordingProgressEventArgs(TimeSpan duration, float audioLevel)
    {
        Duration = duration;
        AudioLevel = audioLevel;
    }
}
