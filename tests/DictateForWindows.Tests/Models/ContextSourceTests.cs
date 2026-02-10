using DictateForWindows.Core.Models;
using FluentAssertions;
using Xunit;

namespace DictateForWindows.Tests.Models;

public class ContextSourceTests
{
    [Fact]
    public void Empty_ShouldCreateInactiveSource()
    {
        var source = ContextSource.Empty(ContextSourceType.Clipboard);

        source.Type.Should().Be(ContextSourceType.Clipboard);
        source.IsActive.Should().BeFalse();
        source.HasContent.Should().BeFalse();
        source.Text.Should().BeEmpty();
        source.ThumbnailPath.Should().BeNull();
    }

    [Fact]
    public void HasContent_ShouldBeTrueWhenTextIsPresent()
    {
        var source = new ContextSource
        {
            Type = ContextSourceType.Clipboard,
            Text = "Hello world"
        };

        source.HasContent.Should().BeTrue();
    }

    [Fact]
    public void HasContent_ShouldBeTrueWhenThumbnailIsPresent()
    {
        var source = new ContextSource
        {
            Type = ContextSourceType.Screenshot,
            ThumbnailPath = "/tmp/screenshot.bmp"
        };

        source.HasContent.Should().BeTrue();
    }

    [Fact]
    public void HasContent_ShouldBeFalseWhenEmpty()
    {
        var source = new ContextSource
        {
            Type = ContextSourceType.None
        };

        source.HasContent.Should().BeFalse();
    }

    [Theory]
    [InlineData(ContextSourceType.None)]
    [InlineData(ContextSourceType.Clipboard)]
    [InlineData(ContextSourceType.Screenshot)]
    public void Empty_ShouldSetCorrectType(ContextSourceType type)
    {
        var source = ContextSource.Empty(type);

        source.Type.Should().Be(type);
    }
}
