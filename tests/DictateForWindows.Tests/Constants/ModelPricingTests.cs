using DictateForWindows.Core.Constants;
using Xunit;

namespace DictateForWindows.Tests.Constants;

public class ModelPricingTests
{
    [Theory]
    [InlineData("whisper-1", 0.0001)]
    [InlineData("gpt-4o-transcribe", 0.0001)]
    [InlineData("gpt-4o-mini-transcribe", 0.00005)]
    [InlineData("whisper-large-v3-turbo", 0.000011)]
    [InlineData("whisper-large-v3", 0.000031)]
    public void GetTranscriptionCostPerSecond_ReturnsCorrectPrice(string model, decimal expected)
    {
        var result = ModelPricing.GetTranscriptionCostPerSecond(model);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("unknown-model")]
    [InlineData("")]
    public void GetTranscriptionCostPerSecond_ReturnsZeroForUnknownModel(string model)
    {
        var result = ModelPricing.GetTranscriptionCostPerSecond(model);
        Assert.Equal(0m, result);
    }

    [Theory]
    [InlineData("gpt-4o-mini", 0.00000015, 0.0000006)]
    [InlineData("gpt-4o", 0.0000025, 0.00001)]
    [InlineData("o4-mini", 0.0000011, 0.0000044)]
    [InlineData("o3-mini", 0.0000011, 0.0000044)]
    [InlineData("o1", 0.000015, 0.00006)]
    public void GetRewordingCostPerToken_ReturnsCorrectPrices(
        string model, decimal expectedInput, decimal expectedOutput)
    {
        var (input, output) = ModelPricing.GetRewordingCostPerToken(model);
        Assert.Equal(expectedInput, input);
        Assert.Equal(expectedOutput, output);
    }

    [Fact]
    public void GetRewordingCostPerToken_ReturnsZeroForUnknownModel()
    {
        var (input, output) = ModelPricing.GetRewordingCostPerToken("unknown-model");
        Assert.Equal(0m, input);
        Assert.Equal(0m, output);
    }

    [Fact]
    public void CalculateTranscriptionCost_CalculatesCorrectly()
    {
        // 60 seconds at $0.0001/second = $0.006
        var cost = ModelPricing.CalculateTranscriptionCost("whisper-1", 60000);
        Assert.Equal(0.006m, cost);
    }

    [Fact]
    public void CalculateTranscriptionCost_HandlesZeroDuration()
    {
        var cost = ModelPricing.CalculateTranscriptionCost("whisper-1", 0);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void CalculateRewordingCost_CalculatesCorrectly()
    {
        // 100 input tokens + 50 output tokens with gpt-4o-mini
        // Input: 100 * 0.00000015 = 0.000015
        // Output: 50 * 0.0000006 = 0.00003
        // Total: 0.000045
        var cost = ModelPricing.CalculateRewordingCost("gpt-4o-mini", 100, 50);
        Assert.Equal(0.000045m, cost);
    }

    [Fact]
    public void CalculateRewordingCost_HandlesZeroTokens()
    {
        var cost = ModelPricing.CalculateRewordingCost("gpt-4o-mini", 0, 0);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void TranscriptionModels_ContainsAllExpectedModels()
    {
        var models = ModelPricing.AllTranscriptionModels;

        Assert.Contains("whisper-1", models);
        Assert.Contains("gpt-4o-transcribe", models);
        Assert.Contains("gpt-4o-mini-transcribe", models);
        Assert.Contains("whisper-large-v3-turbo", models);
        Assert.Contains("whisper-large-v3", models);
    }

    [Fact]
    public void RewordingModels_ContainsAllExpectedModels()
    {
        var models = ModelPricing.AllRewordingModels;

        Assert.Contains("gpt-4o-mini", models);
        Assert.Contains("gpt-4o", models);
        Assert.Contains("o4-mini", models);
        Assert.Contains("o3-mini", models);
        Assert.Contains("o1", models);
        Assert.Contains("llama-3.3-70b-versatile", models);
        Assert.Contains("llama-3.1-8b-instant", models);
    }
}
