using System.Text;
using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Prosody;

/// <summary>
/// Applies prosody-based formatting to transcribed text.
/// Maps voice physical properties to typography:
/// - Loud/emphatic speech → **bold**
/// - Whispered speech → *italic*
/// - Long pauses → paragraph breaks
/// - Medium pauses → line breaks
/// - Rising pitch at segment end → question mark
/// - High energy + falling pitch → exclamation mark
/// </summary>
public class ProsodyFormatter
{
    // Energy thresholds (dB above/below baseline)
    private const float BoldEnergyThreshold = 6f;     // ~4x louder than baseline
    private const float WhisperEnergyThreshold = -8f;  // significantly quieter

    // Pause thresholds (milliseconds)
    private const double ParagraphPauseMs = 1500;
    private const double LinePauseMs = 500;

    // Pitch thresholds for punctuation
    private const float RisingPitchThreshold = 0.15f;  // 15% rise → question
    private const float FallingPitchWithEnergy = -0.10f; // 10% fall + loud → exclamation

    /// <summary>
    /// Apply prosody-based formatting to transcribed text using aligned segments.
    /// </summary>
    public string ApplyFormatting(
        string rawText,
        List<TranscriptionSegment>? textSegments,
        ProsodyResult prosody)
    {
        if (textSegments == null || textSegments.Count == 0 || !prosody.IsSuccess)
            return rawText;

        var result = new StringBuilder();

        for (int i = 0; i < textSegments.Count; i++)
        {
            var seg = textSegments[i];
            var segText = seg.Text.TrimStart();
            if (string.IsNullOrEmpty(segText)) continue;

            // Find overlapping prosody windows for this text segment
            var overlapping = prosody.Segments
                .Where(p => p.StartTime < seg.End && p.EndTime > seg.Start && !p.IsSilence)
                .ToList();

            if (overlapping.Count == 0)
            {
                result.Append(segText);
                AppendPauseBetween(result, seg, i < textSegments.Count - 1 ? textSegments[i + 1] : null, prosody.Pauses);
                continue;
            }

            // Compute average energy delta and pitch behavior for this segment
            float avgEnergyDelta = overlapping.Average(p => p.EnergyDelta);
            bool isWhisper = overlapping.All(p => p.IsWhisper);

            // Check pitch at segment end for punctuation
            var endWindows = overlapping.Where(p => p.EndTime >= seg.End - 0.3).ToList();
            float endPitchDelta = endWindows.Count > 0 ? endWindows.Average(p => p.PitchDelta) : 0f;

            // Apply formatting
            string formatted = segText;

            // Bold: loud speech
            if (avgEnergyDelta > BoldEnergyThreshold)
            {
                formatted = WrapBold(formatted);
            }
            // Italic: whisper
            else if (isWhisper || avgEnergyDelta < WhisperEnergyThreshold)
            {
                formatted = WrapItalic(formatted);
            }

            // Punctuation from prosody (only if segment doesn't already end with punctuation)
            if (!EndsWithPunctuation(formatted))
            {
                if (endPitchDelta > RisingPitchThreshold)
                {
                    formatted = formatted.TrimEnd() + "?";
                }
                else if (endPitchDelta < FallingPitchWithEnergy && avgEnergyDelta > BoldEnergyThreshold / 2)
                {
                    formatted = formatted.TrimEnd() + "!";
                }
            }

            result.Append(formatted);

            // Add pause-based breaks between segments
            AppendPauseBetween(result, seg, i < textSegments.Count - 1 ? textSegments[i + 1] : null, prosody.Pauses);
        }

        return result.ToString().Trim();
    }

    private static void AppendPauseBetween(StringBuilder sb, TranscriptionSegment current,
        TranscriptionSegment? next, List<PauseEvent> pauses)
    {
        if (next == null) return;

        // Find pauses between current segment end and next segment start
        var gapPause = pauses.FirstOrDefault(p =>
            p.StartTime >= current.End - 0.1 && p.EndTime <= next.Start + 0.1);

        if (gapPause != null)
        {
            if (gapPause.DurationMs >= ParagraphPauseMs)
            {
                sb.Append("\n\n");
                return;
            }
            if (gapPause.DurationMs >= LinePauseMs)
            {
                sb.Append('\n');
                return;
            }
        }

        // Default: space between segments (if not already present)
        if (sb.Length > 0 && sb[^1] != ' ' && sb[^1] != '\n')
        {
            sb.Append(' ');
        }
    }

    private static string WrapBold(string text)
    {
        var trimmed = text.Trim();
        var leading = text[..text.IndexOf(trimmed[0])];
        var trailing = text[(text.LastIndexOf(trimmed[^1]) + 1)..];
        return $"{leading}**{trimmed}**{trailing}";
    }

    private static string WrapItalic(string text)
    {
        var trimmed = text.Trim();
        var leading = text[..text.IndexOf(trimmed[0])];
        var trailing = text[(text.LastIndexOf(trimmed[^1]) + 1)..];
        return $"{leading}*{trimmed}*{trailing}";
    }

    private static bool EndsWithPunctuation(string text)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.Length == 0) return false;
        char last = trimmed[^1];
        // Also check before markdown markers
        if (last == '*')
        {
            int inner = trimmed.Length - 1;
            while (inner > 0 && trimmed[inner] == '*') inner--;
            last = trimmed[inner];
        }
        return last is '.' or '!' or '?' or ',' or ';' or ':' or '…';
    }
}
