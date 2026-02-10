namespace DictateForWindows.Core.Models;

public enum ContextSourceType
{
    None,
    Clipboard,
    Screenshot
}

public class ContextSource
{
    public ContextSourceType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public bool IsActive { get; set; }
    public bool IsLoading { get; set; }

    public static ContextSource Empty(ContextSourceType type) => new()
    {
        Type = type,
        IsActive = false
    };

    public bool HasContent => !string.IsNullOrWhiteSpace(Text) || !string.IsNullOrEmpty(ThumbnailPath);
}
