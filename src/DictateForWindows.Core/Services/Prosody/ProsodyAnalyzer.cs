using DictateForWindows.Core.Models;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace DictateForWindows.Core.Services.Prosody;

/// <summary>
/// Analyzes prosodic features from audio using windowed analysis with YIN pitch detection.
/// Processes 50ms windows with 25ms overlap to extract pitch (F0), energy (RMS/dB),
/// silence detection, whisper detection, and pause events.
/// </summary>
public class ProsodyAnalyzer : IProsodyAnalyzer
{
    private readonly ILogger<ProsodyAnalyzer>? _logger;

    // Analysis window parameters
    private const int WindowMs = 50;
    private const int HopMs = 25;

    // Silence / whisper thresholds (in dB relative to full scale)
    private const float SilenceThresholdDb = -40f;
    private const float WhisperThresholdDb = -28f;

    // Pitch detection range for human speech
    private const float MinPitchHz = 60f;
    private const float MaxPitchHz = 500f;

    // YIN algorithm threshold (lower = stricter, 0.1-0.2 typical)
    private const float YinThreshold = 0.15f;

    // Pause detection
    private const double MinPauseDurationMs = 200;

    public ProsodyAnalyzer(ILogger<ProsodyAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ProsodyResult> AnalyzeAsync(string wavFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Analyzing prosody from WAV file: {Path}", wavFilePath);

            if (!File.Exists(wavFilePath))
                return ProsodyResult.Failure($"WAV file not found: {wavFilePath}");

            // Read WAV file into PCM samples
            using var reader = new WaveFileReader(wavFilePath);
            var format = reader.WaveFormat;

            if (format.BitsPerSample != 16 || format.Channels != 1)
                return ProsodyResult.Failure($"Expected 16-bit mono WAV, got {format.BitsPerSample}-bit {format.Channels}-channel");

            var totalBytes = (int)reader.Length;
            var pcmData = new byte[totalBytes];
            var bytesRead = await Task.Run(() => reader.Read(pcmData, 0, totalBytes), cancellationToken);

            if (bytesRead == 0)
                return ProsodyResult.Failure("WAV file is empty");

            return await AnalyzeAsync(pcmData[..bytesRead], format.SampleRate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ProsodyResult.Failure("Analysis cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Prosody analysis failed");
            return ProsodyResult.Failure(ex.Message);
        }
    }

    public Task<ProsodyResult> AnalyzeAsync(byte[] pcmData, int sampleRate, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => AnalyzeCore(pcmData, sampleRate, cancellationToken), cancellationToken);
    }

    private ProsodyResult AnalyzeCore(byte[] pcmData, int sampleRate, CancellationToken ct)
    {
        // Convert bytes to float samples normalized to [-1, 1]
        int sampleCount = pcmData.Length / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short raw = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            samples[i] = raw / 32768f;
        }

        int windowSamples = sampleRate * WindowMs / 1000;
        int hopSamples = sampleRate * HopMs / 1000;

        var segments = new List<ProsodySegment>();

        // Windowed analysis
        for (int offset = 0; offset + windowSamples <= sampleCount; offset += hopSamples)
        {
            ct.ThrowIfCancellationRequested();

            double startTime = (double)offset / sampleRate;
            double endTime = (double)(offset + windowSamples) / sampleRate;

            var window = new ReadOnlySpan<float>(samples, offset, windowSamples);

            float energyDb = ComputeEnergyDb(window);
            bool isSilence = energyDb < SilenceThresholdDb;

            float pitchHz = 0f;
            bool isWhisper = false;

            if (!isSilence)
            {
                pitchHz = DetectPitchYin(window, sampleRate);
                isWhisper = energyDb < WhisperThresholdDb && pitchHz > 0;
            }

            segments.Add(new ProsodySegment
            {
                StartTime = startTime,
                EndTime = endTime,
                PitchHz = pitchHz,
                EnergyDb = energyDb,
                IsSilence = isSilence,
                IsWhisper = isWhisper
            });
        }

        if (segments.Count == 0)
            return ProsodyResult.Failure("Audio too short for analysis");

        // Compute baselines from voiced (non-silence) segments
        var voicedSegments = segments.Where(s => !s.IsSilence && s.PitchHz > 0).ToList();

        float baselinePitch = 0f;
        float baselineEnergy = 0f;

        if (voicedSegments.Count > 0)
        {
            baselinePitch = Median(voicedSegments.Select(s => s.PitchHz));
            baselineEnergy = Median(voicedSegments.Select(s => s.EnergyDb));

            // Compute deltas relative to baseline
            foreach (var seg in segments)
            {
                if (!seg.IsSilence && seg.PitchHz > 0)
                {
                    // Pitch delta: normalized so +1 = doubled pitch, -1 = halved
                    seg.PitchDelta = baselinePitch > 0
                        ? Math.Clamp((seg.PitchHz - baselinePitch) / baselinePitch, -1f, 1f)
                        : 0f;
                }
                seg.EnergyDelta = seg.EnergyDb - baselineEnergy;
            }
        }

        // Detect pauses
        var pauses = DetectPauses(segments);

        _logger?.LogInformation(
            "Prosody analysis complete: {SegmentCount} segments, {VoicedCount} voiced, {PauseCount} pauses, baseline pitch={BaselinePitch:F1}Hz, baseline energy={BaselineEnergy:F1}dB",
            segments.Count, voicedSegments.Count, pauses.Count, baselinePitch, baselineEnergy);

        return ProsodyResult.Success(segments, pauses, baselinePitch, baselineEnergy);
    }

    /// <summary>
    /// Compute RMS energy in dB (relative to full scale).
    /// </summary>
    private static float ComputeEnergyDb(ReadOnlySpan<float> window)
    {
        double sum = 0;
        for (int i = 0; i < window.Length; i++)
        {
            sum += window[i] * window[i];
        }

        double rms = Math.Sqrt(sum / window.Length);
        if (rms < 1e-10) return -100f; // effectively silence
        return (float)(20.0 * Math.Log10(rms));
    }

    /// <summary>
    /// YIN pitch detection algorithm.
    /// Returns fundamental frequency in Hz, or 0 if no pitch detected (unvoiced).
    /// Reference: de Cheveigné & Kawahara (2002), "YIN, a fundamental frequency estimator for speech and music"
    /// </summary>
    private static float DetectPitchYin(ReadOnlySpan<float> window, int sampleRate)
    {
        int minLag = (int)(sampleRate / MaxPitchHz);
        int maxLag = (int)(sampleRate / MinPitchHz);

        if (maxLag >= window.Length / 2)
            maxLag = window.Length / 2 - 1;
        if (minLag >= maxLag)
            return 0f;

        int length = maxLag + 1;

        // Step 1 & 2: Difference function + cumulative mean normalized difference
        Span<float> diff = stackalloc float[length];
        diff[0] = 1f; // d'[0] = 1 by convention

        double runningSum = 0;

        for (int tau = 1; tau < length; tau++)
        {
            double sum = 0;
            for (int j = 0; j < window.Length - maxLag; j++)
            {
                double delta = window[j] - window[j + tau];
                sum += delta * delta;
            }

            diff[tau] = (float)sum;
            runningSum += sum;

            // Cumulative mean normalized difference function (CMND)
            diff[tau] = runningSum > 0
                ? (float)(diff[tau] * tau / runningSum)
                : 1f;
        }

        // Step 3: Absolute threshold — find first dip below threshold in valid range
        int bestTau = -1;
        for (int tau = minLag; tau < length - 1; tau++)
        {
            if (diff[tau] < YinThreshold)
            {
                // Find the local minimum
                while (tau + 1 < length && diff[tau + 1] < diff[tau])
                {
                    tau++;
                }
                bestTau = tau;
                break;
            }
        }

        if (bestTau < 1)
            return 0f; // No pitch found (unvoiced)

        // Step 4: Parabolic interpolation for sub-sample accuracy
        float refined = bestTau;
        if (bestTau > 0 && bestTau < length - 1)
        {
            float a = diff[bestTau - 1];
            float b = diff[bestTau];
            float c = diff[bestTau + 1];
            float denom = 2f * (2f * b - a - c);
            if (Math.Abs(denom) > 1e-6f)
            {
                refined = bestTau + (a - c) / denom;
            }
        }

        if (refined < 1f) return 0f;

        float frequency = sampleRate / refined;

        // Sanity check
        return frequency >= MinPitchHz && frequency <= MaxPitchHz ? frequency : 0f;
    }

    /// <summary>
    /// Detect pauses (consecutive silence windows above minimum duration).
    /// </summary>
    private static List<PauseEvent> DetectPauses(List<ProsodySegment> segments)
    {
        var pauses = new List<PauseEvent>();
        double? pauseStart = null;

        foreach (var seg in segments)
        {
            if (seg.IsSilence)
            {
                pauseStart ??= seg.StartTime;
            }
            else
            {
                if (pauseStart.HasValue)
                {
                    double durationMs = (seg.StartTime - pauseStart.Value) * 1000;
                    if (durationMs >= MinPauseDurationMs)
                    {
                        pauses.Add(new PauseEvent
                        {
                            StartTime = pauseStart.Value,
                            EndTime = seg.StartTime
                        });
                    }
                    pauseStart = null;
                }
            }
        }

        // Handle trailing silence
        if (pauseStart.HasValue && segments.Count > 0)
        {
            var last = segments[^1];
            double durationMs = (last.EndTime - pauseStart.Value) * 1000;
            if (durationMs >= MinPauseDurationMs)
            {
                pauses.Add(new PauseEvent
                {
                    StartTime = pauseStart.Value,
                    EndTime = last.EndTime
                });
            }
        }

        return pauses;
    }

    /// <summary>
    /// Compute median of a sequence of floats.
    /// </summary>
    private static float Median(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0f;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2f
            : sorted[mid];
    }
}
