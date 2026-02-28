namespace DictateForWindows.Core.Models;

/// <summary>
/// A single branch in a voice branching session.
/// The user can record multiple alternatives and pick the best one before injection.
/// </summary>
public class VoiceBranch
{
    /// <summary>0-based branch index (A=0, B=1, etc.).</summary>
    public int Index { get; set; }

    /// <summary>The transcribed (and optionally formatted) text for this branch.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>When this branch was recorded.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Display label: "A", "B", "C", etc.</summary>
    public string Label => ((char)('A' + Index)).ToString();
}
