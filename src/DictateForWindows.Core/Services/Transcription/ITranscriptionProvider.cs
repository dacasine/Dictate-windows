using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Transcription;

/// <summary>
/// Interface for transcription API providers.
/// </summary>
public interface ITranscriptionProvider
{
    /// <summary>
    /// Provider name (e.g., "OpenAI", "Groq", "Custom").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// API provider type.
    /// </summary>
    ApiProvider Provider { get; }

    /// <summary>
    /// Available transcription models for this provider.
    /// </summary>
    IReadOnlyList<string> AvailableModels { get; }

    /// <summary>
    /// Transcribe an audio file.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file.</param>
    /// <param name="model">Model to use for transcription.</param>
    /// <param name="language">Language code (or "detect" for auto-detection).</param>
    /// <param name="prompt">Style prompt for formatting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result.</returns>
    Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string model,
        string language,
        string? prompt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate the API key for this provider.
    /// </summary>
    /// <param name="apiKey">API key to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    Task<bool> ValidateApiKeyAsync(string apiKey);
}
