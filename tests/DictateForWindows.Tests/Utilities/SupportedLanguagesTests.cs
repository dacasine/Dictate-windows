using DictateForWindows.Core.Utilities;
using Xunit;

namespace DictateForWindows.Tests.Utilities;

public class SupportedLanguagesTests
{
    [Fact]
    public void All_ContainsAtLeast60Languages()
    {
        Assert.True(SupportedLanguages.All.Length >= 60,
            $"Expected at least 60 languages, got {SupportedLanguages.All.Length}");
    }

    [Fact]
    public void All_ContainsDetectOption()
    {
        Assert.Contains(SupportedLanguages.All, l => l.Code == "detect");
    }

    [Fact]
    public void All_DetectIsFirst()
    {
        var first = SupportedLanguages.All.First();
        Assert.Equal("detect", first.Code);
        Assert.Equal("Auto-detect", first.Name);
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("de", "German")]
    [InlineData("es", "Spanish")]
    [InlineData("fr", "French")]
    [InlineData("ja", "Japanese")]
    [InlineData("zh", "Chinese")]
    public void All_ContainsLanguageWithCorrectName(string code, string expectedName)
    {
        var language = SupportedLanguages.All.FirstOrDefault(l => l.Code == code);
        Assert.NotNull(language);
        Assert.Equal(expectedName, language.Name);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("ja")]
    public void All_LanguagesHaveFlagEmoji(string code)
    {
        var language = SupportedLanguages.All.FirstOrDefault(l => l.Code == code);
        Assert.NotNull(language);
        Assert.False(string.IsNullOrEmpty(language.FlagEmoji),
            $"Language {code} should have a flag emoji");
    }

    [Fact]
    public void GetByCode_ReturnsCorrectLanguage()
    {
        var result = SupportedLanguages.GetByCode("en");
        Assert.NotNull(result);
        Assert.Equal("English", result.Name);
    }

    [Fact]
    public void GetByCode_IsCaseInsensitive()
    {
        var lower = SupportedLanguages.GetByCode("en");
        var upper = SupportedLanguages.GetByCode("EN");
        var mixed = SupportedLanguages.GetByCode("En");

        Assert.NotNull(lower);
        Assert.NotNull(upper);
        Assert.NotNull(mixed);
        Assert.Equal(lower.Code, upper.Code);
        Assert.Equal(lower.Code, mixed.Code);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("unknown")]
    [InlineData("")]
    public void GetByCode_ReturnsNullForUnknownCode(string code)
    {
        var result = SupportedLanguages.GetByCode(code);
        Assert.Null(result);
    }
}
