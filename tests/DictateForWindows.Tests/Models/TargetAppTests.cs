using DictateForWindows.Core.Models;
using FluentAssertions;
using Xunit;

namespace DictateForWindows.Tests.Models;

public class TargetAppTests
{
    [Fact]
    public void BuildLink_ShouldReplaceTextPlaceholder()
    {
        var app = new TargetApp
        {
            DeepLinkPattern = "https://example.com/search?q={text}"
        };

        var link = app.BuildLink("hello world");

        link.Should().Be("https://example.com/search?q=hello%20world");
    }

    [Fact]
    public void BuildLink_ShouldUrlEncodeSpecialCharacters()
    {
        var app = new TargetApp
        {
            DeepLinkPattern = "https://example.com/search?q={text}"
        };

        var link = app.BuildLink("test&value=1");

        link.Should().Contain("test%26value%3D1");
    }

    [Fact]
    public void BuildLink_ShouldHandleEmptyText()
    {
        var app = new TargetApp
        {
            DeepLinkPattern = "https://example.com/search?q={text}"
        };

        var link = app.BuildLink("");

        link.Should().Be("https://example.com/search?q=");
    }

    [Fact]
    public void GetDefaults_ShouldReturnFourApps()
    {
        var defaults = TargetApp.GetDefaults();

        defaults.Should().HaveCount(4);
    }

    [Fact]
    public void GetDefaults_ShouldContainPerplexityClaudeChatGPTTelegram()
    {
        var defaults = TargetApp.GetDefaults();

        defaults.Select(a => a.Name).Should().Contain(new[] { "Perplexity", "Claude", "ChatGPT", "Telegram" });
    }

    [Fact]
    public void GetDefaults_ShouldHaveUniqueIds()
    {
        var defaults = TargetApp.GetDefaults();

        defaults.Select(a => a.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetDefaults_ShouldHaveOrderedPositions()
    {
        var defaults = TargetApp.GetDefaults();

        defaults.Select(a => a.Position).Should().BeInAscendingOrder();
    }

    [Fact]
    public void NewTargetApp_ShouldHaveGeneratedId()
    {
        var app = new TargetApp { Name = "Test" };

        app.Id.Should().NotBeNullOrEmpty();
        app.Id.Should().HaveLength(32); // Guid "N" format
    }

    [Fact]
    public void NewTargetApp_ShouldBeEnabledByDefault()
    {
        var app = new TargetApp();

        app.IsEnabled.Should().BeTrue();
    }
}
