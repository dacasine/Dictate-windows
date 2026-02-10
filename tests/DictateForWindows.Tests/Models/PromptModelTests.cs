using DictateForWindows.Core.Models;
using Xunit;

namespace DictateForWindows.Tests.Models;

public class PromptModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var model = new PromptModel();

        Assert.Equal(0, model.Id);
        Assert.Equal(0, model.Position);
        Assert.Equal(string.Empty, model.Name);
        Assert.Equal(string.Empty, model.Prompt);
        Assert.False(model.RequiresSelection);
        Assert.False(model.AutoApply);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var model = new PromptModel
        {
            Id = 1,
            Position = 5,
            Name = "Test Prompt",
            Prompt = "Rewrite this text formally",
            RequiresSelection = true,
            AutoApply = true
        };

        Assert.Equal(1, model.Id);
        Assert.Equal(5, model.Position);
        Assert.Equal("Test Prompt", model.Name);
        Assert.Equal("Rewrite this text formally", model.Prompt);
        Assert.True(model.RequiresSelection);
        Assert.True(model.AutoApply);
    }

    [Fact]
    public void IsSpecialButton_ReturnsTrueForNegativePosition()
    {
        var instantButton = new PromptModel { Position = -1 };
        var addButton = new PromptModel { Position = -2 };
        var selectAllButton = new PromptModel { Position = -3 };

        Assert.True(instantButton.IsSpecialButton);
        Assert.True(addButton.IsSpecialButton);
        Assert.True(selectAllButton.IsSpecialButton);
    }

    [Fact]
    public void IsSpecialButton_ReturnsFalseForNormalPrompt()
    {
        var normalPrompt = new PromptModel { Position = 0 };
        var anotherPrompt = new PromptModel { Position = 5 };

        Assert.False(normalPrompt.IsSpecialButton);
        Assert.False(anotherPrompt.IsSpecialButton);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new PromptModel
        {
            Id = 1,
            Position = 2,
            Name = "Original",
            Prompt = "Original prompt",
            RequiresSelection = true,
            AutoApply = true
        };

        var clone = original.Clone();

        // Verify clone has same values
        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Position, clone.Position);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Prompt, clone.Prompt);
        Assert.Equal(original.RequiresSelection, clone.RequiresSelection);
        Assert.Equal(original.AutoApply, clone.AutoApply);

        // Verify clone is independent
        clone.Name = "Modified";
        Assert.NotEqual(original.Name, clone.Name);
    }
}
