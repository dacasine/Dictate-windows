namespace DictateForWindows.Core.Utilities;

/// <summary>
/// Supported languages for transcription with their display names and codes.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>
    /// Language information including code, name, and flag emoji.
    /// </summary>
    public record LanguageInfo(string Code, string Name, string NativeName, string FlagEmoji);

    /// <summary>
    /// All supported languages for Whisper transcription.
    /// </summary>
    public static readonly LanguageInfo[] All =
    [
        new("detect", "Auto-detect", "Auto", "\ud83c\udf10"),
        new("af", "Afrikaans", "Afrikaans", "\ud83c\uddff\ud83c\udde6"),
        new("sq", "Albanian", "Shqip", "\ud83c\udde6\ud83c\uddf1"),
        new("ar", "Arabic", "\u0627\u0644\u0639\u0631\u0628\u064a\u0629", "\ud83c\uddf8\ud83c\udde6"),
        new("hy", "Armenian", "\u0540\u0561\u0575\u0565\u0580\u0565\u0576", "\ud83c\udde6\ud83c\uddf2"),
        new("az", "Azerbaijani", "Az\u0259rbaycan", "\ud83c\udde6\ud83c\uddff"),
        new("eu", "Basque", "Euskara", "\ud83c\uddea\ud83c\uddf8"),
        new("be", "Belarusian", "\u0411\u0435\u043b\u0430\u0440\u0443\u0441\u043a\u0430\u044f", "\ud83c\udde7\ud83c\uddfe"),
        new("bn", "Bengali", "\u09ac\u09be\u0982\u09b2\u09be", "\ud83c\udde7\ud83c\udde9"),
        new("bg", "Bulgarian", "\u0411\u044a\u043b\u0433\u0430\u0440\u0441\u043a\u0438", "\ud83c\udde7\ud83c\uddec"),
        new("ca", "Catalan", "Catal\u00e0", "\ud83c\uddea\ud83c\uddf8"),
        new("zh", "Chinese", "\u4e2d\u6587", "\ud83c\udde8\ud83c\uddf3"),
        new("zh-cn", "Chinese (Simplified)", "\u7b80\u4f53\u4e2d\u6587", "\ud83c\udde8\ud83c\uddf3"),
        new("zh-tw", "Chinese (Traditional)", "\u7e41\u9ad4\u4e2d\u6587", "\ud83c\uddf9\ud83c\uddfc"),
        new("hr", "Croatian", "Hrvatski", "\ud83c\udded\ud83c\uddf7"),
        new("cs", "Czech", "\u010ce\u0161tina", "\ud83c\udde8\ud83c\uddff"),
        new("da", "Danish", "Dansk", "\ud83c\udde9\ud83c\uddf0"),
        new("nl", "Dutch", "Nederlands", "\ud83c\uddf3\ud83c\uddf1"),
        new("en", "English", "English", "\ud83c\uddec\ud83c\udde7"),
        new("et", "Estonian", "Eesti", "\ud83c\uddea\ud83c\uddea"),
        new("fi", "Finnish", "Suomi", "\ud83c\uddeb\ud83c\uddee"),
        new("fr", "French", "Fran\u00e7ais", "\ud83c\uddeb\ud83c\uddf7"),
        new("gl", "Galician", "Galego", "\ud83c\uddea\ud83c\uddf8"),
        new("ka", "Georgian", "\u10e5\u10d0\u10e0\u10d7\u10e3\u10da\u10d8", "\ud83c\uddec\ud83c\uddea"),
        new("de", "German", "Deutsch", "\ud83c\udde9\ud83c\uddea"),
        new("el", "Greek", "\u0395\u03bb\u03bb\u03b7\u03bd\u03b9\u03ba\u03ac", "\ud83c\uddec\ud83c\uddf7"),
        new("he", "Hebrew", "\u05e2\u05d1\u05e8\u05d9\u05ea", "\ud83c\uddee\ud83c\uddf1"),
        new("hi", "Hindi", "\u0939\u093f\u0928\u094d\u0926\u0940", "\ud83c\uddee\ud83c\uddf3"),
        new("hu", "Hungarian", "Magyar", "\ud83c\udded\ud83c\uddfa"),
        new("id", "Indonesian", "Bahasa Indonesia", "\ud83c\uddee\ud83c\udde9"),
        new("it", "Italian", "Italiano", "\ud83c\uddee\ud83c\uddf9"),
        new("ja", "Japanese", "\u65e5\u672c\u8a9e", "\ud83c\uddef\ud83c\uddf5"),
        new("kk", "Kazakh", "\u049a\u0430\u0437\u0430\u049b\u0448\u0430", "\ud83c\uddf0\ud83c\uddff"),
        new("ko", "Korean", "\ud55c\uad6d\uc5b4", "\ud83c\uddf0\ud83c\uddf7"),
        new("lv", "Latvian", "Latvie\u0161u", "\ud83c\uddf1\ud83c\uddfb"),
        new("lt", "Lithuanian", "Lietuvi\u0173", "\ud83c\uddf1\ud83c\uddf9"),
        new("mk", "Macedonian", "\u041c\u0430\u043a\u0435\u0434\u043e\u043d\u0441\u043a\u0438", "\ud83c\uddf2\ud83c\uddf0"),
        new("ms", "Malay", "Bahasa Melayu", "\ud83c\uddf2\ud83c\uddfe"),
        new("mr", "Marathi", "\u092e\u0930\u093e\u0920\u0940", "\ud83c\uddee\ud83c\uddf3"),
        new("ne", "Nepali", "\u0928\u0947\u092a\u093e\u0932\u0940", "\ud83c\uddf3\ud83c\uddf5"),
        new("no", "Norwegian", "Norsk", "\ud83c\uddf3\ud83c\uddf4"),
        new("nn", "Norwegian Nynorsk", "Nynorsk", "\ud83c\uddf3\ud83c\uddf4"),
        new("fa", "Persian", "\u0641\u0627\u0631\u0633\u06cc", "\ud83c\uddee\ud83c\uddf7"),
        new("pl", "Polish", "Polski", "\ud83c\uddf5\ud83c\uddf1"),
        new("pt", "Portuguese", "Portugu\u00eas", "\ud83c\uddf5\ud83c\uddf9"),
        new("pa", "Punjabi", "\u0a2a\u0a70\u0a1c\u0a3e\u0a2c\u0a40", "\ud83c\uddee\ud83c\uddf3"),
        new("ro", "Romanian", "Rom\u00e2n\u0103", "\ud83c\uddf7\ud83c\uddf4"),
        new("ru", "Russian", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439", "\ud83c\uddf7\ud83c\uddfa"),
        new("sr", "Serbian", "\u0421\u0440\u043f\u0441\u043a\u0438", "\ud83c\uddf7\ud83c\uddf8"),
        new("sk", "Slovak", "Sloven\u010dina", "\ud83c\uddf8\ud83c\uddf0"),
        new("sl", "Slovenian", "Sloven\u0161\u010dina", "\ud83c\uddf8\ud83c\uddee"),
        new("es", "Spanish", "Espa\u00f1ol", "\ud83c\uddea\ud83c\uddf8"),
        new("sw", "Swahili", "Kiswahili", "\ud83c\uddf0\ud83c\uddea"),
        new("sv", "Swedish", "Svenska", "\ud83c\uddf8\ud83c\uddea"),
        new("tl", "Tagalog", "Tagalog", "\ud83c\uddf5\ud83c\udded"),
        new("ta", "Tamil", "\u0ba4\u0bae\u0bbf\u0bb4\u0bcd", "\ud83c\uddee\ud83c\uddf3"),
        new("th", "Thai", "\u0e44\u0e17\u0e22", "\ud83c\uddf9\ud83c\udded"),
        new("tr", "Turkish", "T\u00fcrk\u00e7e", "\ud83c\uddf9\ud83c\uddf7"),
        new("uk", "Ukrainian", "\u0423\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u0430", "\ud83c\uddfa\ud83c\udde6"),
        new("ur", "Urdu", "\u0627\u0631\u062f\u0648", "\ud83c\uddf5\ud83c\uddf0"),
        new("vi", "Vietnamese", "Ti\u1ebfng Vi\u1ec7t", "\ud83c\uddfb\ud83c\uddf3"),
        new("cy", "Welsh", "Cymraeg", "\ud83c\uddec\ud83c\udde7"),
        new("yue", "Cantonese", "\u7cb5\u8a9e", "\ud83c\udded\ud83c\uddf0"),
        new("yue-cn", "Cantonese (Simplified)", "\u7ca4\u8bed", "\ud83c\udde8\ud83c\uddf3"),
        new("yue-hk", "Cantonese (Traditional)", "\u7cb5\u8a9e", "\ud83c\udded\ud83c\uddf0"),
    ];

    /// <summary>
    /// Get language info by code.
    /// </summary>
    public static LanguageInfo? GetByCode(string code) =>
        All.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get display name for a language code.
    /// </summary>
    public static string GetDisplayName(string code) =>
        GetByCode(code)?.Name ?? code;
}
