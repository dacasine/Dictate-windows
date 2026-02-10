namespace DictateForWindows.Core.Models;

/// <summary>
/// Represents API usage statistics for a specific model.
/// </summary>
public class UsageModel
{
    /// <summary>
    /// The name/identifier of the AI model.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Total audio time in milliseconds (for transcription models).
    /// </summary>
    public long AudioTimeMs { get; set; }

    /// <summary>
    /// Total input tokens used (for chat/rewording models).
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Total output tokens generated (for chat/rewording models).
    /// </summary>
    public long OutputTokens { get; set; }

    /// <summary>
    /// The API provider for this model.
    /// </summary>
    public ApiProvider Provider { get; set; }

    /// <summary>
    /// Calculated cost based on usage and model pricing.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Total input + output tokens.
    /// </summary>
    public long TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Audio time formatted as MM:SS or H:MM:SS.
    /// </summary>
    public string AudioTimeFormatted
    {
        get
        {
            var span = TimeSpan.FromMilliseconds(AudioTimeMs);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
                : $"{span.Minutes}:{span.Seconds:D2}";
        }
    }
}

/// <summary>
/// Supported API providers.
/// </summary>
public enum ApiProvider
{
    OpenAI = 0,
    Groq = 1,
    Custom = 2
}

/// <summary>
/// Types of API models.
/// </summary>
public enum ModelType
{
    Transcription,
    Rewording
}
