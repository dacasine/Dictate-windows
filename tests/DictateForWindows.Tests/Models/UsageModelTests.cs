using DictateForWindows.Core.Models;
using Xunit;

namespace DictateForWindows.Tests.Models;

public class UsageModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var model = new UsageModel();

        Assert.Equal(string.Empty, model.ModelName);
        Assert.Equal(0, model.AudioTimeMs);
        Assert.Equal(0, model.InputTokens);
        Assert.Equal(0, model.OutputTokens);
        Assert.Equal(ApiProvider.OpenAI, model.Provider);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var model = new UsageModel
        {
            ModelName = "whisper-1",
            AudioTimeMs = 60000,
            InputTokens = 100,
            OutputTokens = 50,
            Provider = ApiProvider.Groq
        };

        Assert.Equal("whisper-1", model.ModelName);
        Assert.Equal(60000, model.AudioTimeMs);
        Assert.Equal(100, model.InputTokens);
        Assert.Equal(50, model.OutputTokens);
        Assert.Equal(ApiProvider.Groq, model.Provider);
    }

    [Fact]
    public void AudioTimeFormatted_ReturnsCorrectFormat()
    {
        var model = new UsageModel { AudioTimeMs = 65000 }; // 1:05

        Assert.Equal("1:05", model.AudioTimeFormatted);
    }

    [Fact]
    public void AudioTimeFormatted_HandlesHours()
    {
        var model = new UsageModel { AudioTimeMs = 3665000 }; // 1:01:05

        Assert.Equal("1:01:05", model.AudioTimeFormatted);
    }

    [Fact]
    public void AudioTimeFormatted_HandlesZero()
    {
        var model = new UsageModel { AudioTimeMs = 0 };

        Assert.Equal("0:00", model.AudioTimeFormatted);
    }

    [Fact]
    public void TotalTokens_ReturnsSumOfInputAndOutput()
    {
        var model = new UsageModel
        {
            InputTokens = 100,
            OutputTokens = 50
        };

        Assert.Equal(150, model.TotalTokens);
    }
}

public class TranscriptionResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = TranscriptionResult.Success("Hello world", 5000);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello world", result.Text);
        Assert.Equal(5000, result.DurationMs);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        var result = TranscriptionResult.Failure("Invalid API key");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Text);
        Assert.Equal(0, result.DurationMs);
        Assert.Equal("Invalid API key", result.Error);
    }

    [Fact]
    public void Success_WithEmptyText_IsStillSuccess()
    {
        var result = TranscriptionResult.Success("", 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);
    }
}

public class RewordingResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = RewordingResult.Success("Reworded text", 50, 30);

        Assert.True(result.IsSuccess);
        Assert.Equal("Reworded text", result.Text);
        Assert.Equal(50, result.InputTokens);
        Assert.Equal(30, result.OutputTokens);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        var result = RewordingResult.Failure("Rate limit exceeded");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Text);
        Assert.Equal(0, result.InputTokens);
        Assert.Equal(0, result.OutputTokens);
        Assert.Equal("Rate limit exceeded", result.Error);
    }
}
