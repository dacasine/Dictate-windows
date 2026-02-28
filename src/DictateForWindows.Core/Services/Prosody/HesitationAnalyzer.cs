using System.Text.RegularExpressions;
using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Prosody;

/// <summary>
/// Analyzes transcribed speech for hesitation patterns: filler words, uncertainty,
/// self-corrections, fatigue, and topic changes. Correlates text patterns with
/// prosodic features for higher-confidence annotations.
/// </summary>
public class HesitationAnalyzer
{
    // --- Filler word dictionaries (multilingual) ---

    private static readonly Dictionary<string, HashSet<string>> FillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "um", "uh", "uh huh", "like", "you know", "I mean", "so", "well", "actually",
            "basically", "literally", "right", "okay so"
        },
        ["fr"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "euh", "heu", "genre", "en fait", "du coup", "voilà", "quoi", "bah",
            "bon", "ben", "tu vois", "en gros", "c'est-à-dire"
        },
        ["es"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "eh", "este", "bueno", "o sea", "pues", "digamos", "como que",
            "a ver", "entonces", "mira"
        },
        ["de"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "äh", "ähm", "also", "halt", "sozusagen", "quasi", "na ja",
            "irgendwie", "sag mal", "weißt du"
        },
        ["pt"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "é", "tipo", "então", "né", "bem", "quer dizer", "assim",
            "olha", "sabe", "entendeu"
        }
    };

    // --- Self-correction markers (multilingual) ---

    private static readonly Dictionary<string, string[]> SelfCorrectionMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new[] { "I mean", "no wait", "sorry", "actually no", "let me rephrase", "or rather", "well no" },
        ["fr"] = new[] { "en fait", "non attends", "pardon", "je veux dire", "non en fait", "ou plutôt", "enfin" },
        ["es"] = new[] { "o sea", "no espera", "perdón", "quiero decir", "mejor dicho", "bueno no" },
        ["de"] = new[] { "ich meine", "nein warte", "also nein", "beziehungsweise", "anders gesagt" },
        ["pt"] = new[] { "quer dizer", "não espera", "desculpa", "ou melhor", "na verdade não" }
    };

    // --- Thresholds ---

    /// <summary>Filler density above this in a window → uncertainty flag.</summary>
    private const float UncertaintyFillerDensity = 0.25f;

    /// <summary>Energy delta below baseline suggesting low confidence.</summary>
    private const float LowConfidenceEnergyDelta = -3f;

    /// <summary>Speech rate decline ratio (last quarter / first quarter) → fatigue.</summary>
    private const float FatigueRateThreshold = 0.70f;

    /// <summary>Pitch/energy baseline shift between adjacent segments → topic change.</summary>
    private const float TopicChangePitchShift = 0.30f;
    private const float TopicChangeEnergyShift = 8f;

    /// <summary>Minimum segment count to enable fatigue detection.</summary>
    private const int MinSegmentsForFatigue = 8;

    /// <summary>
    /// Analyze transcription + prosody for hesitation patterns.
    /// </summary>
    public HesitationResult Analyze(
        string text,
        List<TranscriptionSegment>? segments,
        ProsodyResult? prosody,
        string? detectedLanguage = null)
    {
        var result = new HesitationResult();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.OverallFluencyScore = 1f;
            return result;
        }

        // Determine which language dictionaries to use
        var languages = ResolveLanguages(detectedLanguage);

        // 1. Filler word detection
        DetectFillers(text, segments, languages, result);

        // 2. Self-correction detection
        DetectSelfCorrections(text, segments, languages, result);

        // 3. Uncertainty flagging (filler density + prosody correlation)
        DetectUncertainty(text, segments, prosody, languages, result);

        // 4. Fatigue tracking (speech rate decline over session)
        DetectFatigue(segments, prosody, result);

        // 5. Topic change detection (prosody shifts)
        DetectTopicChanges(segments, prosody, result);

        // 6. Compute overall fluency score
        result.OverallFluencyScore = ComputeFluencyScore(text, result);

        return result;
    }

    // ------- Detection methods -------

    private void DetectFillers(
        string text,
        List<TranscriptionSegment>? segments,
        List<string> languages,
        HesitationResult result)
    {
        var allFillers = GetCombinedSet(FillerWords, languages);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Check single words and bigrams
        for (int i = 0; i < words.Length; i++)
        {
            var word = CleanWord(words[i]);

            bool isFiller = allFillers.Contains(word);

            // Check bigram (e.g. "you know", "I mean", "en fait")
            if (!isFiller && i < words.Length - 1)
            {
                var bigram = $"{word} {CleanWord(words[i + 1])}";
                isFiller = allFillers.Contains(bigram);
            }

            if (isFiller)
            {
                var (start, end) = EstimateTimeForWordIndex(i, words.Length, segments);
                result.Annotations.Add(new HesitationAnnotation
                {
                    Type = HesitationType.FillerWord,
                    StartTime = start,
                    EndTime = end,
                    Text = word,
                    Confidence = 0.9f
                });
                result.FillerCount++;
            }
        }
    }

    private void DetectSelfCorrections(
        string text,
        List<TranscriptionSegment>? segments,
        List<string> languages,
        HesitationResult result)
    {
        var lowerText = text.ToLowerInvariant();

        foreach (var lang in languages)
        {
            if (!SelfCorrectionMarkers.TryGetValue(lang, out var markers)) continue;

            foreach (var marker in markers)
            {
                var lowerMarker = marker.ToLowerInvariant();
                int idx = 0;
                while ((idx = lowerText.IndexOf(lowerMarker, idx, StringComparison.Ordinal)) >= 0)
                {
                    // Verify it's at a word boundary
                    bool atBoundary = (idx == 0 || !char.IsLetter(lowerText[idx - 1]))
                                      && (idx + lowerMarker.Length >= lowerText.Length || !char.IsLetter(lowerText[idx + lowerMarker.Length]));

                    if (atBoundary)
                    {
                        var (start, end) = EstimateTimeForCharIndex(idx, marker.Length, text, segments);
                        result.Annotations.Add(new HesitationAnnotation
                        {
                            Type = HesitationType.SelfCorrection,
                            StartTime = start,
                            EndTime = end,
                            Text = text.Substring(idx, marker.Length),
                            Suggestion = BuildCorrectionSuggestion(text, idx, marker.Length),
                            Confidence = 0.8f
                        });
                        result.SelfCorrectionCount++;
                    }

                    idx += lowerMarker.Length;
                }
            }
        }
    }

    private void DetectUncertainty(
        string text,
        List<TranscriptionSegment>? segments,
        ProsodyResult? prosody,
        List<string> languages,
        HesitationResult result)
    {
        if (segments == null || segments.Count < 2) return;

        var allFillers = GetCombinedSet(FillerWords, languages);

        // Sliding window: check filler density in groups of 3 segments
        int windowSize = Math.Min(3, segments.Count);
        for (int i = 0; i <= segments.Count - windowSize; i++)
        {
            var windowSegments = segments.Skip(i).Take(windowSize).ToList();
            var windowText = string.Join(" ", windowSegments.Select(s => s.Text));
            var windowWords = windowText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (windowWords.Length == 0) continue;

            int fillerCount = windowWords.Count(w => allFillers.Contains(CleanWord(w)));
            float density = (float)fillerCount / windowWords.Length;

            if (density < UncertaintyFillerDensity) continue;

            // Boost confidence with prosody: low energy = more uncertain
            float confidence = Math.Min(1f, density * 2f);
            if (prosody is { IsSuccess: true })
            {
                var overlapping = prosody.Segments
                    .Where(p => p.StartTime < windowSegments.Last().End && p.EndTime > windowSegments.First().Start && !p.IsSilence)
                    .ToList();

                if (overlapping.Count > 0)
                {
                    float avgEnergyDelta = overlapping.Average(p => p.EnergyDelta);
                    if (avgEnergyDelta < LowConfidenceEnergyDelta)
                        confidence = Math.Min(1f, confidence + 0.2f);
                }
            }

            // Avoid duplicating if this window overlaps an existing uncertainty annotation
            bool alreadyFlagged = result.Annotations.Any(a =>
                a.Type == HesitationType.Uncertainty &&
                a.StartTime < windowSegments.Last().End &&
                a.EndTime > windowSegments.First().Start);

            if (!alreadyFlagged)
            {
                result.Annotations.Add(new HesitationAnnotation
                {
                    Type = HesitationType.Uncertainty,
                    StartTime = windowSegments.First().Start,
                    EndTime = windowSegments.Last().End,
                    Text = windowText.Trim(),
                    Suggestion = "[uncertain]",
                    Confidence = confidence
                });
            }
        }
    }

    private void DetectFatigue(
        List<TranscriptionSegment>? segments,
        ProsodyResult? prosody,
        HesitationResult result)
    {
        if (segments == null || segments.Count < MinSegmentsForFatigue) return;

        // Compute speech rate (words per second) for first quarter vs. last quarter
        int quarter = segments.Count / 4;
        if (quarter < 2) return;

        var firstQuarter = segments.Take(quarter).ToList();
        var lastQuarter = segments.Skip(segments.Count - quarter).ToList();

        float firstRate = ComputeSpeechRate(firstQuarter);
        float lastRate = ComputeSpeechRate(lastQuarter);

        if (firstRate <= 0) return;

        float ratio = lastRate / firstRate;
        if (ratio < FatigueRateThreshold)
        {
            result.FatigueLevel = Math.Clamp(1f - ratio, 0f, 1f);

            var totalDuration = segments.Last().End - segments.First().Start;
            var totalMinutes = totalDuration / 60.0;

            result.Annotations.Add(new HesitationAnnotation
            {
                Type = HesitationType.FatigueWarning,
                StartTime = lastQuarter.First().Start,
                EndTime = lastQuarter.Last().End,
                Text = string.Empty,
                Suggestion = $"Fatigue detected — {totalMinutes:F0} min in, speech rate down {(1 - ratio):P0}",
                Confidence = Math.Clamp((FatigueRateThreshold - ratio) * 5f, 0.3f, 1f)
            });
        }
    }

    private void DetectTopicChanges(
        List<TranscriptionSegment>? segments,
        ProsodyResult? prosody,
        HesitationResult result)
    {
        if (segments == null || segments.Count < 4 || prosody is not { IsSuccess: true }) return;

        // Compare prosody baselines between adjacent text segment groups
        for (int i = 1; i < segments.Count - 1; i++)
        {
            var prevSeg = segments[i - 1];
            var currSeg = segments[i];

            // Get prosody windows for each text segment
            var prevProsody = prosody.Segments
                .Where(p => p.StartTime < prevSeg.End && p.EndTime > prevSeg.Start && !p.IsSilence)
                .ToList();
            var currProsody = prosody.Segments
                .Where(p => p.StartTime < currSeg.End && p.EndTime > currSeg.Start && !p.IsSilence)
                .ToList();

            if (prevProsody.Count < 2 || currProsody.Count < 2) continue;

            float prevAvgPitch = prevProsody.Average(p => p.PitchDelta);
            float currAvgPitch = currProsody.Average(p => p.PitchDelta);
            float prevAvgEnergy = prevProsody.Average(p => p.EnergyDb);
            float currAvgEnergy = currProsody.Average(p => p.EnergyDb);

            float pitchShift = Math.Abs(currAvgPitch - prevAvgPitch);
            float energyShift = Math.Abs(currAvgEnergy - prevAvgEnergy);

            if (pitchShift > TopicChangePitchShift || energyShift > TopicChangeEnergyShift)
            {
                // Check for a preceding pause (strengthens the signal)
                bool hasPause = prosody.Pauses.Any(p =>
                    p.StartTime >= prevSeg.End - 0.2 && p.EndTime <= currSeg.Start + 0.2);

                float confidence = hasPause ? 0.85f : 0.6f;

                result.Annotations.Add(new HesitationAnnotation
                {
                    Type = HesitationType.TopicChange,
                    StartTime = prevSeg.End,
                    EndTime = currSeg.Start,
                    Text = string.Empty,
                    Suggestion = "Possible topic change — consider section break",
                    Confidence = confidence
                });
            }
        }
    }

    // ------- Helpers -------

    private static float ComputeFluencyScore(string text, HesitationResult result)
    {
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount == 0) return 1f;

        // Penalize for fillers, self-corrections, uncertainty
        float fillerPenalty = Math.Min(0.5f, result.FillerCount * 0.05f);
        float correctionPenalty = Math.Min(0.3f, result.SelfCorrectionCount * 0.08f);
        float uncertaintyPenalty = Math.Min(0.2f, result.Annotations.Count(a => a.Type == HesitationType.Uncertainty) * 0.1f);

        return Math.Clamp(1f - fillerPenalty - correctionPenalty - uncertaintyPenalty, 0f, 1f);
    }

    private static float ComputeSpeechRate(List<TranscriptionSegment> segments)
    {
        if (segments.Count == 0) return 0;

        int totalWords = segments.Sum(s => s.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        double totalTime = segments.Last().End - segments.First().Start;

        return totalTime > 0 ? (float)(totalWords / totalTime) : 0;
    }

    private static List<string> ResolveLanguages(string? detectedLanguage)
    {
        // If we have a detected language, prioritize it but also include all as fallback
        var languages = new List<string>();

        if (!string.IsNullOrEmpty(detectedLanguage))
        {
            // Normalize: "fr-FR" → "fr", "en-US" → "en"
            var primary = detectedLanguage.Split('-')[0].ToLowerInvariant();
            if (FillerWords.ContainsKey(primary))
                languages.Add(primary);
        }

        // Add all supported languages (detected language first if present)
        foreach (var lang in FillerWords.Keys)
        {
            if (!languages.Contains(lang))
                languages.Add(lang);
        }

        return languages;
    }

    private static HashSet<string> GetCombinedSet(Dictionary<string, HashSet<string>> dict, List<string> languages)
    {
        var combined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lang in languages)
        {
            if (dict.TryGetValue(lang, out var set))
            {
                foreach (var item in set)
                    combined.Add(item);
            }
        }
        return combined;
    }

    private static string CleanWord(string word)
    {
        // Strip leading/trailing punctuation
        return word.Trim(' ', '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '*');
    }

    private static (double start, double end) EstimateTimeForWordIndex(
        int wordIndex, int totalWords, List<TranscriptionSegment>? segments)
    {
        if (segments == null || segments.Count == 0)
            return (0, 0);

        double totalStart = segments.First().Start;
        double totalEnd = segments.Last().End;
        double duration = totalEnd - totalStart;

        if (totalWords <= 0) return (totalStart, totalEnd);

        double wordFraction = (double)wordIndex / totalWords;
        double start = totalStart + wordFraction * duration;
        double end = start + duration / totalWords;

        return (start, Math.Min(end, totalEnd));
    }

    private static (double start, double end) EstimateTimeForCharIndex(
        int charIndex, int length, string text, List<TranscriptionSegment>? segments)
    {
        if (segments == null || segments.Count == 0 || text.Length == 0)
            return (0, 0);

        double totalStart = segments.First().Start;
        double totalEnd = segments.Last().End;
        double duration = totalEnd - totalStart;

        double charFraction = (double)charIndex / text.Length;
        double start = totalStart + charFraction * duration;
        double end = start + ((double)length / text.Length) * duration;

        return (start, Math.Min(end, totalEnd));
    }

    private static string? BuildCorrectionSuggestion(string text, int markerIdx, int markerLength)
    {
        // Extract some context after the correction marker
        int afterIdx = markerIdx + markerLength;
        if (afterIdx >= text.Length) return null;

        // Take up to 60 chars after the marker
        int remaining = Math.Min(60, text.Length - afterIdx);
        var after = text.Substring(afterIdx, remaining).Trim();

        if (string.IsNullOrWhiteSpace(after)) return null;

        return $"Self-correction → \"{after}\"";
    }
}
