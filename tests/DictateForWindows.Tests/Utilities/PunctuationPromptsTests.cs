using DictateForWindows.Core.Utilities;
using Xunit;

namespace DictateForWindows.Tests.Utilities;

public class PunctuationPromptsTests
{
    [Theory]
    [InlineData("en", "This sentence has capitalization and punctuation.")]
    [InlineData("EN", "This sentence has capitalization and punctuation.")]
    [InlineData("de", "Dieser Satz hat Großbuchstaben und Zeichensetzung.")]
    [InlineData("es", "Esta frase tiene mayúsculas y puntuación.")]
    [InlineData("fr", "Cette phrase contient des majuscules et de la ponctuation.")]
    [InlineData("ja", "この文には大文字と句読点があります。")]
    [InlineData("zh", "这句话有大写字母和标点符号。")]
    public void GetPromptForLanguage_ReturnsCorrectPrompt(string languageCode, string expected)
    {
        var result = PunctuationPrompts.GetPromptForLanguage(languageCode);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("en-US", "This sentence has capitalization and punctuation.")]
    [InlineData("en-GB", "This sentence has capitalization and punctuation.")]
    [InlineData("de-DE", "Dieser Satz hat Großbuchstaben und Zeichensetzung.")]
    [InlineData("es-MX", "Esta frase tiene mayúsculas y puntuación.")]
    [InlineData("pt-BR", "Esta frase tem maiúsculas e pontuação.")]
    public void GetPromptForLanguage_FallsBackToBaseLanguage(string languageCode, string expected)
    {
        var result = PunctuationPrompts.GetPromptForLanguage(languageCode);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("detect")]
    [InlineData("DETECT")]
    public void GetPromptForLanguage_ReturnsEnglishForNullOrDetect(string? languageCode)
    {
        var result = PunctuationPrompts.GetPromptForLanguage(languageCode!);
        Assert.Equal("This sentence has capitalization and punctuation.", result);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("unknown")]
    [InlineData("xyz-ABC")]
    public void GetPromptForLanguage_ReturnsEnglishForUnknownLanguage(string languageCode)
    {
        var result = PunctuationPrompts.GetPromptForLanguage(languageCode);
        Assert.Equal("This sentence has capitalization and punctuation.", result);
    }

    [Fact]
    public void GetSupportedLanguages_ReturnsAtLeast60Languages()
    {
        var languages = PunctuationPrompts.GetSupportedLanguages().ToList();
        Assert.True(languages.Count >= 60, $"Expected at least 60 languages, got {languages.Count}");
    }

    [Fact]
    public void GetSupportedLanguages_ContainsCommonLanguages()
    {
        var languages = PunctuationPrompts.GetSupportedLanguages().ToList();

        Assert.Contains("en", languages);
        Assert.Contains("de", languages);
        Assert.Contains("es", languages);
        Assert.Contains("fr", languages);
        Assert.Contains("ja", languages);
        Assert.Contains("zh", languages);
        Assert.Contains("ar", languages);
        Assert.Contains("ru", languages);
    }
}
