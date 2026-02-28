using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Prosody;

/// <summary>
/// Analyzes prosodic features (pitch, energy, pauses) from audio recordings.
/// Shared foundation for prosody→typography, hesitation intelligence, emotional watermarking.
/// </summary>
public interface IProsodyAnalyzer
{
    /// <summary>
    /// Analyze prosody from a WAV file (16-bit PCM, mono).
    /// </summary>
    Task<ProsodyResult> AnalyzeAsync(string wavFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze prosody from raw PCM samples (16-bit signed, mono).
    /// </summary>
    Task<ProsodyResult> AnalyzeAsync(byte[] pcmData, int sampleRate, CancellationToken cancellationToken = default);
}
