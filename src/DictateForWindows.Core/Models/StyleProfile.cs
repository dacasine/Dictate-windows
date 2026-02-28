namespace DictateForWindows.Core.Models;

/// <summary>
/// A style profile that defines how transcribed text should be formatted
/// for a specific target app category. Used by contextual auto-formatting.
/// </summary>
public class StyleProfile
{
    /// <summary>Unique identifier for this profile.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name (e.g., "Chat", "Email", "AI Assistant").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// System prompt sent to the LLM to adjust output style.
    /// If empty, no style transformation is applied.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>Category for grouping in UI.</summary>
    public StyleCategory Category { get; set; }

    /// <summary>Short description shown in settings.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Returns the built-in style profiles for common app categories.
    /// </summary>
    public static List<StyleProfile> GetDefaults() =>
    [
        new()
        {
            Id = "none",
            Name = "None",
            Category = StyleCategory.None,
            Description = "No style adjustment — raw transcription",
            SystemPrompt = string.Empty
        },
        new()
        {
            Id = "chat",
            Name = "Chat / Messaging",
            Category = StyleCategory.Chat,
            Description = "Concise, casual, single paragraph",
            SystemPrompt = "Reformat this dictated text for a chat message. Keep it concise and conversational. " +
                           "Use a single paragraph. Remove unnecessary filler. Don't add greetings or sign-offs. " +
                           "Only output the reformatted text, nothing else."
        },
        new()
        {
            Id = "email",
            Name = "Email",
            Category = StyleCategory.Email,
            Description = "Professional with greeting and sign-off",
            SystemPrompt = "Reformat this dictated text as a professional email body. " +
                           "Add an appropriate greeting and sign-off if not already present. " +
                           "Use proper paragraphs. Keep the tone professional but not overly formal. " +
                           "Only output the reformatted text, nothing else."
        },
        new()
        {
            Id = "ai_query",
            Name = "AI Assistant",
            Category = StyleCategory.AiAssistant,
            Description = "Clear query/prompt for AI assistants",
            SystemPrompt = "Reformat this dictated text as a clear, well-structured query for an AI assistant. " +
                           "Preserve the user's intent and questions. Remove filler words. " +
                           "If multiple questions are asked, number them for clarity. " +
                           "Only output the reformatted text, nothing else."
        },
        new()
        {
            Id = "search",
            Name = "Search Query",
            Category = StyleCategory.Search,
            Description = "Optimized search keywords",
            SystemPrompt = "Extract the core search query from this dictated text. " +
                           "Return only the essential search keywords/phrase, nothing else. " +
                           "Remove filler words, articles, and conversational elements. " +
                           "Keep it under 10 words."
        },
        new()
        {
            Id = "document",
            Name = "Document",
            Category = StyleCategory.Document,
            Description = "Structured paragraphs with proper formatting",
            SystemPrompt = "Reformat this dictated text for a document. " +
                           "Use proper paragraphs, complete sentences, and correct punctuation. " +
                           "Maintain a professional writing style. " +
                           "Only output the reformatted text, nothing else."
        },
        new()
        {
            Id = "code_comment",
            Name = "Code Comment",
            Category = StyleCategory.Code,
            Description = "Terse technical comment style",
            SystemPrompt = "Reformat this dictated text as a code comment. " +
                           "Be extremely concise and technical. Use imperative mood. " +
                           "No pleasantries. Example: 'Handle edge case where user ID is null'. " +
                           "Only output the reformatted text, nothing else."
        }
    ];
}

/// <summary>
/// Categories of output style for target apps.
/// </summary>
public enum StyleCategory
{
    /// <summary>No style transformation.</summary>
    None,

    /// <summary>Chat / instant messaging (Telegram, Slack, Discord, Teams).</summary>
    Chat,

    /// <summary>Email (Outlook, Gmail, Thunderbird).</summary>
    Email,

    /// <summary>AI assistants (Claude, ChatGPT, Perplexity, Copilot).</summary>
    AiAssistant,

    /// <summary>Search engines (Google, Bing, Perplexity search).</summary>
    Search,

    /// <summary>Document editing (Word, Google Docs, Notion).</summary>
    Document,

    /// <summary>Code editors (VS Code, Visual Studio, JetBrains).</summary>
    Code
}
