namespace DictateForWindows.Core.Models;

/// <summary>
/// Represents a custom prompt for text rewording/generation.
/// </summary>
public class PromptModel
{
    /// <summary>
    /// Unique identifier for the prompt.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Position in the list for ordering.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Display name of the prompt.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The actual prompt text sent to the AI model.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Whether this prompt requires text to be selected first.
    /// </summary>
    public bool RequiresSelection { get; set; }

    /// <summary>
    /// Whether this prompt should be automatically applied after transcription.
    /// </summary>
    public bool AutoApply { get; set; }

    /// <summary>
    /// Returns true if this is a special button (negative position).
    /// </summary>
    public bool IsSpecialButton => Position < 0;

    /// <summary>
    /// Creates a copy of this prompt model.
    /// </summary>
    public PromptModel Clone() => new()
    {
        Id = Id,
        Position = Position,
        Name = Name,
        Prompt = Prompt,
        RequiresSelection = RequiresSelection,
        AutoApply = AutoApply
    };
}

/// <summary>
/// Special prompt IDs for virtual buttons.
/// </summary>
public static class SpecialPromptIds
{
    /// <summary>
    /// Default prompt (no rewording, just transcription).
    /// </summary>
    public const int Default = 0;

    /// <summary>
    /// Instant output button (outputs prompt text directly).
    /// </summary>
    public const int Instant = -1;

    /// <summary>
    /// Add new prompt button.
    /// </summary>
    public const int Add = -2;

    /// <summary>
    /// Select all text button.
    /// </summary>
    public const int SelectAll = -3;
}
