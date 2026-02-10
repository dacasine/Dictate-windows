namespace DictateForWindows.Core.Models;

/// <summary>
/// Result of a transcription API call.
/// </summary>
public class TranscriptionResult
{
    /// <summary>
    /// Whether the transcription was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The transcribed text (null if failed).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Error type if transcription failed.
    /// </summary>
    public TranscriptionError? ErrorType { get; set; }

    /// <summary>
    /// Error message (null if successful).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Duration of the audio in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Detected language (if auto-detect was used).
    /// </summary>
    public string? DetectedLanguage { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TranscriptionResult Success(string text, long durationMs, string? language = null) => new()
    {
        IsSuccess = true,
        Text = text,
        DurationMs = durationMs,
        DetectedLanguage = language
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TranscriptionResult Failure(string error, TranscriptionError? errorType = null) => new()
    {
        IsSuccess = false,
        Error = error,
        ErrorType = errorType
    };

    // Compatibility aliases
    public static TranscriptionResult FromSuccess(string text, double durationSeconds, string? language = null) =>
        Success(text, (long)(durationSeconds * 1000), language);

    public static TranscriptionResult FromError(TranscriptionError error, string? message = null) =>
        Failure(message ?? error.ToString(), error);
}

/// <summary>
/// Types of transcription errors.
/// </summary>
public enum TranscriptionError
{
    /// <summary>
    /// Invalid or missing API key.
    /// </summary>
    InvalidApiKey,

    /// <summary>
    /// API quota exceeded.
    /// </summary>
    QuotaExceeded,

    /// <summary>
    /// Audio file too large or too long.
    /// </summary>
    ContentSizeLimit,

    /// <summary>
    /// Unsupported audio format.
    /// </summary>
    FormatNotSupported,

    /// <summary>
    /// Request timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Network or connectivity error.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Unknown or unclassified error.
    /// </summary>
    Unknown
}
