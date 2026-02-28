using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services;

/// <summary>
/// Contextual auto-formatting: selects and applies style transformations
/// based on the target app. When text is destined for Telegram, it becomes
/// concise; for Claude, a well-structured query; for Perplexity, search keywords.
/// </summary>
public class ContextualFormatter
{
    private static readonly Dictionary<string, StyleProfile> _profileCache;

    static ContextualFormatter()
    {
        _profileCache = StyleProfile.GetDefaults().ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the style profile for a target app.
    /// Returns null if no style should be applied (profile is "none" or missing).
    /// </summary>
    public StyleProfile? GetStyleForApp(Models.TargetApp? app)
    {
        if (app == null) return null;

        // 1. Use explicit style profile if set
        if (!string.IsNullOrEmpty(app.StyleProfileId) && app.StyleProfileId != "none")
        {
            if (_profileCache.TryGetValue(app.StyleProfileId, out var profile))
                return profile;
        }

        // 2. Auto-detect from app name / deep link pattern
        return AutoDetectStyle(app);
    }

    /// <summary>
    /// Get the system prompt for contextual formatting of a target app.
    /// Returns null if no contextual formatting should be applied.
    /// </summary>
    public string? GetSystemPromptForApp(Models.TargetApp? app)
    {
        var profile = GetStyleForApp(app);
        if (profile == null || string.IsNullOrEmpty(profile.SystemPrompt))
            return null;

        return profile.SystemPrompt;
    }

    /// <summary>
    /// Get all available style profiles.
    /// </summary>
    public static IReadOnlyList<StyleProfile> GetAllProfiles() => StyleProfile.GetDefaults();

    /// <summary>
    /// Auto-detect style category from app name and deep link pattern
    /// when no explicit StyleProfileId is set.
    /// </summary>
    private static StyleProfile? AutoDetectStyle(Models.TargetApp app)
    {
        var name = app.Name.ToLowerInvariant();
        var link = app.DeepLinkPattern.ToLowerInvariant();

        // Chat apps
        if (IsMatch(name, link, "telegram", "slack", "discord", "teams", "whatsapp", "signal", "messenger", "wechat"))
            return _profileCache.GetValueOrDefault("chat");

        // Email apps
        if (IsMatch(name, link, "outlook", "gmail", "thunderbird", "mail", "email", "mailto:"))
            return _profileCache.GetValueOrDefault("email");

        // AI assistants
        if (IsMatch(name, link, "claude", "chatgpt", "openai", "copilot", "gemini", "mistral", "llama"))
            return _profileCache.GetValueOrDefault("ai_query");

        // Search engines
        if (IsMatch(name, link, "perplexity", "google", "bing", "duckduckgo", "search"))
            return _profileCache.GetValueOrDefault("search");

        // Code editors
        if (IsMatch(name, link, "vscode", "visual studio", "intellij", "jetbrains", "vim", "neovim", "cursor"))
            return _profileCache.GetValueOrDefault("code_comment");

        // Document apps
        if (IsMatch(name, link, "word", "docs", "notion", "obsidian", "typora", "bear", "evernote"))
            return _profileCache.GetValueOrDefault("document");

        return null;
    }

    private static bool IsMatch(string name, string link, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                link.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
