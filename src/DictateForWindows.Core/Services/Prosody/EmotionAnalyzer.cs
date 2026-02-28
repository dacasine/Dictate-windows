using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Prosody;

/// <summary>
/// Analyzes prosodic features to detect emotional state per speech segment.
/// Uses a dimensional model (valence + arousal) derived from pitch, energy,
/// and speech rate, then maps to discrete emotion labels.
///
/// Prosody→Emotion mapping (from research):
/// - Angry: high pitch + high energy + fast rate
/// - Confident: steady pitch + moderate energy + steady rate
/// - Uncertain: low energy + variable pitch + slow rate
/// - Excited: high energy + rising pitch + fast rate
/// - Calm: low energy + steady pitch + slow rate
/// - Sad: very low energy + falling pitch + slow rate
/// - Stressed: high pitch variability + high energy + irregular rate
/// </summary>
public class EmotionAnalyzer
{
    // Thresholds tuned from speech emotion research
    private const float HighEnergyDelta = 4f;     // dB above baseline
    private const float LowEnergyDelta = -4f;     // dB below baseline
    private const float VeryLowEnergyDelta = -8f;  // dB far below baseline
    private const float HighPitchDelta = 0.20f;    // 20% above baseline
    private const float LowPitchDelta = -0.15f;    // 15% below baseline
    private const float HighPitchVariability = 0.25f; // high std dev in pitch
    private const float SteadyPitchVariability = 0.08f; // low std dev = steady

    // Warning thresholds
    private const float AngryWarningConfidence = 0.6f;
    private const float StressedWarningConfidence = 0.7f;

    /// <summary>
    /// Analyze emotions from prosody data aligned to transcription segments.
    /// </summary>
    public EmotionResult Analyze(
        List<TranscriptionSegment>? textSegments,
        ProsodyResult? prosody)
    {
        var result = new EmotionResult();

        if (textSegments == null || textSegments.Count == 0 || prosody is not { IsSuccess: true })
        {
            result.DominantEmotion = EmotionTag.Neutral;
            result.DominantConfidence = 0.5f;
            return result;
        }

        // Analyze each text segment
        foreach (var seg in textSegments)
        {
            var overlapping = prosody.Segments
                .Where(p => p.StartTime < seg.End && p.EndTime > seg.Start && !p.IsSilence)
                .ToList();

            if (overlapping.Count == 0)
            {
                result.Segments.Add(new EmotionSegment
                {
                    StartTime = seg.Start,
                    EndTime = seg.End,
                    Emotion = EmotionTag.Neutral,
                    Confidence = 0.3f,
                    Text = seg.Text
                });
                continue;
            }

            var emotion = ClassifyEmotion(overlapping);
            emotion.StartTime = seg.Start;
            emotion.EndTime = seg.End;
            emotion.Text = seg.Text;
            result.Segments.Add(emotion);
        }

        // Compute session-level aggregates
        ComputeSessionMetrics(result);

        return result;
    }

    private EmotionSegment ClassifyEmotion(List<ProsodySegment> windows)
    {
        float avgEnergyDelta = windows.Average(w => w.EnergyDelta);
        float avgPitchDelta = windows.Average(w => w.PitchDelta);

        // Pitch variability: std dev of pitch delta across windows
        float pitchMean = avgPitchDelta;
        float pitchVariance = windows.Count > 1
            ? windows.Average(w => (w.PitchDelta - pitchMean) * (w.PitchDelta - pitchMean))
            : 0f;
        float pitchStdDev = MathF.Sqrt(pitchVariance);

        // Pitch direction: is it generally rising or falling?
        float pitchTrend = 0f;
        if (windows.Count >= 3)
        {
            var firstHalf = windows.Take(windows.Count / 2).Average(w => w.PitchDelta);
            var secondHalf = windows.Skip(windows.Count / 2).Average(w => w.PitchDelta);
            pitchTrend = secondHalf - firstHalf;
        }

        // Classify using decision tree approach
        var (emotion, confidence) = Classify(avgEnergyDelta, avgPitchDelta, pitchStdDev, pitchTrend);

        return new EmotionSegment
        {
            Emotion = emotion,
            Confidence = confidence
        };
    }

    private static (EmotionTag emotion, float confidence) Classify(
        float energyDelta, float pitchDelta, float pitchVariability, float pitchTrend)
    {
        // Angry: high energy + high pitch + any rate
        if (energyDelta > HighEnergyDelta && pitchDelta > HighPitchDelta)
        {
            float conf = Math.Min(1f, (energyDelta / 10f + pitchDelta) * 0.8f);
            return (EmotionTag.Angry, Math.Clamp(conf, 0.4f, 0.95f));
        }

        // Stressed: high pitch variability + elevated energy
        if (pitchVariability > HighPitchVariability && energyDelta > 0)
        {
            float conf = Math.Min(1f, pitchVariability * 2f);
            return (EmotionTag.Stressed, Math.Clamp(conf, 0.4f, 0.9f));
        }

        // Excited: high energy + rising pitch
        if (energyDelta > HighEnergyDelta && pitchTrend > 0.1f)
        {
            float conf = Math.Min(1f, (energyDelta / 8f + pitchTrend) * 0.7f);
            return (EmotionTag.Excited, Math.Clamp(conf, 0.4f, 0.9f));
        }

        // Sad: very low energy + falling pitch
        if (energyDelta < VeryLowEnergyDelta && pitchTrend < -0.05f)
        {
            float conf = Math.Min(1f, (Math.Abs(energyDelta) / 12f) * 0.8f);
            return (EmotionTag.Sad, Math.Clamp(conf, 0.35f, 0.85f));
        }

        // Uncertain: low energy + variable pitch
        if (energyDelta < LowEnergyDelta && pitchVariability > SteadyPitchVariability * 2)
        {
            float conf = Math.Min(1f, (Math.Abs(energyDelta) / 8f + pitchVariability) * 0.6f);
            return (EmotionTag.Uncertain, Math.Clamp(conf, 0.35f, 0.85f));
        }

        // Confident: moderate energy + steady pitch
        if (energyDelta > -2f && energyDelta < HighEnergyDelta &&
            pitchVariability < SteadyPitchVariability)
        {
            float conf = Math.Min(1f, (1f - pitchVariability / SteadyPitchVariability) * 0.7f);
            return (EmotionTag.Confident, Math.Clamp(conf, 0.4f, 0.85f));
        }

        // Calm: low energy + steady pitch
        if (energyDelta < 0 && pitchVariability < SteadyPitchVariability * 1.5f)
        {
            return (EmotionTag.Calm, 0.5f);
        }

        // Default: neutral
        return (EmotionTag.Neutral, 0.4f);
    }

    private void ComputeSessionMetrics(EmotionResult result)
    {
        if (result.Segments.Count == 0)
        {
            result.DominantEmotion = EmotionTag.Neutral;
            result.DominantConfidence = 0.5f;
            return;
        }

        // Find dominant emotion (weighted by confidence)
        var emotionScores = result.Segments
            .GroupBy(s => s.Emotion)
            .Select(g => new
            {
                Emotion = g.Key,
                Score = g.Sum(s => s.Confidence),
                Count = g.Count()
            })
            .OrderByDescending(e => e.Score)
            .ToList();

        var dominant = emotionScores.First();
        result.DominantEmotion = dominant.Emotion;
        result.DominantConfidence = dominant.Score / result.Segments.Count;

        // Compute valence (-1 to +1)
        result.Valence = ComputeValence(result.Segments);

        // Compute arousal (0 to 1)
        result.Arousal = ComputeArousal(result.Segments);

        // Generate warning for negative emotions
        if (result.DominantEmotion == EmotionTag.Angry && result.DominantConfidence > AngryWarningConfidence)
        {
            result.ShouldWarn = true;
            result.WarningMessage = "You may have dictated this while frustrated — review before sending?";
        }
        else if (result.DominantEmotion == EmotionTag.Stressed && result.DominantConfidence > StressedWarningConfidence)
        {
            result.ShouldWarn = true;
            result.WarningMessage = "Elevated stress detected in your speech — consider reviewing the tone.";
        }
    }

    private static float ComputeValence(List<EmotionSegment> segments)
    {
        // Valence mapping: positive emotions > 0, negative < 0
        float sum = 0;
        foreach (var seg in segments)
        {
            float v = seg.Emotion switch
            {
                EmotionTag.Excited => 0.7f,
                EmotionTag.Confident => 0.5f,
                EmotionTag.Calm => 0.3f,
                EmotionTag.Neutral => 0f,
                EmotionTag.Uncertain => -0.3f,
                EmotionTag.Sad => -0.6f,
                EmotionTag.Stressed => -0.5f,
                EmotionTag.Angry => -0.8f,
                _ => 0f
            };
            sum += v * seg.Confidence;
        }
        return Math.Clamp(sum / segments.Count, -1f, 1f);
    }

    private static float ComputeArousal(List<EmotionSegment> segments)
    {
        // Arousal mapping: high-energy emotions → high arousal
        float sum = 0;
        foreach (var seg in segments)
        {
            float a = seg.Emotion switch
            {
                EmotionTag.Angry => 0.9f,
                EmotionTag.Excited => 0.85f,
                EmotionTag.Stressed => 0.8f,
                EmotionTag.Confident => 0.5f,
                EmotionTag.Uncertain => 0.4f,
                EmotionTag.Neutral => 0.3f,
                EmotionTag.Calm => 0.15f,
                EmotionTag.Sad => 0.2f,
                _ => 0.3f
            };
            sum += a * seg.Confidence;
        }
        return Math.Clamp(sum / segments.Count, 0f, 1f);
    }
}
