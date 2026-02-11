using DictateForWindows.Core.Constants;
using Xunit;
using System.Reflection;

namespace DictateForWindows.Tests.Constants;

public class SettingsKeysTests
{
    [Fact]
    public void AllKeys_AreUnique()
    {
        var fields = typeof(SettingsKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToList();

        var values = fields.Select(f => (string)f.GetValue(null)!).ToList();
        var uniqueValues = values.Distinct().ToList();

        Assert.Equal(values.Count, uniqueValues.Count);
    }

    [Fact]
    public void AllKeys_AreNotEmpty()
    {
        var fields = typeof(SettingsKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToList();

        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"Settings key {field.Name} should not be empty");
        }
    }

    [Fact]
    public void ApiProviderKey_Exists()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.ApiProvider));
    }

    [Fact]
    public void ApiKeyKeys_Exist()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.OpenAiApiKey));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.GroqApiKey));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.CustomApiKey));
    }

    [Fact]
    public void ModelKeys_Exist()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.TranscriptionModel));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.RewordingModel));
    }

    [Fact]
    public void HotkeyKey_Exists()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.ActivationHotkey));
    }

    [Fact]
    public void ThemeKey_Exists()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.Theme));
    }

    [Fact]
    public void LanguageKey_Exists()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.TranscriptionLanguage));
    }

    [Fact]
    public void BehaviorKeys_Exist()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.InstantMode));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.AutoEnter));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.AutoFormattingEnabled));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.AnimationSpeed));
    }

    [Fact]
    public void ProxyKeys_Exist()
    {
        Assert.False(string.IsNullOrEmpty(SettingsKeys.UseProxy));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.ProxyHost));
        Assert.False(string.IsNullOrEmpty(SettingsKeys.ProxyPort));
    }

    [Theory]
    [InlineData("dictate_api_provider")]
    [InlineData("dictate_openai_api_key")]
    [InlineData("dictate_groq_api_key")]
    [InlineData("dictate_transcription_model")]
    [InlineData("dictate_rewording_model")]
    public void Keys_FollowNamingConvention(string expectedKey)
    {
        var allFields = typeof(SettingsKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        Assert.Contains(expectedKey, allFields);
    }
}
