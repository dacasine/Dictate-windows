namespace DictateForWindows.Core.Models;

public class TargetApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string DeepLinkPattern { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE71E";
    public int Position { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Style profile ID for contextual auto-formatting.
    /// If null or "none", no style transformation is applied.
    /// </summary>
    public string? StyleProfileId { get; set; }

    /// <summary>
    /// Build the final URL/command by replacing {text} or {query} with the given text.
    /// </summary>
    public string BuildLink(string text)
    {
        var encoded = Uri.EscapeDataString(text);
        return DeepLinkPattern
            .Replace("{text}", encoded)
            .Replace("{query}", encoded);
    }

    public static List<TargetApp> GetDefaults() =>
    [
        new()
        {
            Id = "perplexity",
            Name = "Perplexity",
            DeepLinkPattern = "https://www.perplexity.ai/search?q={text}",
            IconGlyph = "\uE721",
            Position = 0,
            StyleProfileId = "search"
        },
        new()
        {
            Id = "claude",
            Name = "Claude",
            DeepLinkPattern = "https://claude.ai/new?q={text}",
            IconGlyph = "\uE8BD",
            Position = 1,
            StyleProfileId = "ai_query"
        },
        new()
        {
            Id = "chatgpt",
            Name = "ChatGPT",
            DeepLinkPattern = "https://chatgpt.com/?q={text}",
            IconGlyph = "\uE774",
            Position = 2,
            StyleProfileId = "ai_query"
        },
        new()
        {
            Id = "telegram",
            Name = "Telegram",
            DeepLinkPattern = "tg://msg?text={text}",
            IconGlyph = "\uE724",
            Position = 3,
            StyleProfileId = "chat"
        }
    ];
}
