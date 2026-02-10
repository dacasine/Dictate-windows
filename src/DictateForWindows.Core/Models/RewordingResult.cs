namespace DictateForWindows.Core.Models;

/// <summary>
/// Result of a rewording API call.
/// </summary>
public class RewordingResult
{
    /// <summary>
    /// Whether the rewording was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The reworded text (null if failed).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Error type if rewording failed.
    /// </summary>
    public RewordingError? ErrorType { get; set; }

    /// <summary>
    /// Error message (null if successful).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Number of input tokens used.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Number of output tokens generated.
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static RewordingResult Success(string text, int inputTokens, int outputTokens) => new()
    {
        IsSuccess = true,
        Text = text,
        InputTokens = inputTokens,
        OutputTokens = outputTokens
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static RewordingResult Failure(string error, RewordingError? errorType = null) => new()
    {
        IsSuccess = false,
        Error = error,
        ErrorType = errorType
    };

    // Compatibility aliases
    public static RewordingResult FromSuccess(string text, int inputTokens, int outputTokens) =>
        Success(text, inputTokens, outputTokens);

    public static RewordingResult FromError(RewordingError error, string? message = null) =>
        Failure(message ?? error.ToString(), error);
}

/// <summary>
/// Types of rewording errors.
/// </summary>
public enum RewordingError
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
    /// Content too long for the model.
    /// </summary>
    ContentTooLong,

    /// <summary>
    /// Content policy violation.
    /// </summary>
    ContentFiltered,

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
