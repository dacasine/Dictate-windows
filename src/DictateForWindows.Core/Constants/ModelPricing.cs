namespace DictateForWindows.Core.Constants;

/// <summary>
/// Pricing information for AI models.
/// Prices are in USD.
/// </summary>
public static class ModelPricing
{
    /// <summary>
    /// Transcription model pricing (per second of audio).
    /// </summary>
    public static readonly Dictionary<string, decimal> TranscriptionPricePerSecond = new()
    {
        // OpenAI
        ["whisper-1"] = 0.0001m,
        ["gpt-4o-transcribe"] = 0.0001m,
        ["gpt-4o-mini-transcribe"] = 0.00005m,

        // Groq
        ["whisper-large-v3-turbo"] = 0.000011m,
        ["whisper-large-v3"] = 0.000031m,
    };

    /// <summary>
    /// Rewording model pricing (input price per token).
    /// </summary>
    public static readonly Dictionary<string, decimal> RewordingInputPricePerToken = new()
    {
        // OpenAI - Reasoning
        ["o4-mini"] = 0.0000011m,
        ["o3-mini"] = 0.0000011m,
        ["o1"] = 0.000015m,
        ["o1-mini"] = 0.0000011m,

        // OpenAI - GPT-5
        ["gpt-5.2"] = 0.00000175m,
        ["gpt-5"] = 0.00000125m,
        ["gpt-5-mini"] = 0.00000025m,

        // OpenAI - GPT-4
        ["gpt-4o"] = 0.0000025m,
        ["gpt-4o-mini"] = 0.00000015m,
        ["gpt-4-turbo"] = 0.00001m,
        ["gpt-4"] = 0.00003m,

        // OpenAI - GPT-3.5
        ["gpt-3.5-turbo"] = 0.0000005m,

        // Groq
        ["llama-3.1-8b-instant"] = 0.00000005m,
        ["llama-3.3-70b-versatile"] = 0.00000059m,
        ["llama-guard-4-12b"] = 0.0000002m,
    };

    /// <summary>
    /// Rewording model pricing (output price per token).
    /// </summary>
    public static readonly Dictionary<string, decimal> RewordingOutputPricePerToken = new()
    {
        // OpenAI - Reasoning
        ["o4-mini"] = 0.0000044m,
        ["o3-mini"] = 0.0000044m,
        ["o1"] = 0.00006m,
        ["o1-mini"] = 0.0000044m,

        // OpenAI - GPT-5
        ["gpt-5.2"] = 0.000014m,
        ["gpt-5"] = 0.00001m,
        ["gpt-5-mini"] = 0.000002m,

        // OpenAI - GPT-4
        ["gpt-4o"] = 0.00001m,
        ["gpt-4o-mini"] = 0.0000006m,
        ["gpt-4-turbo"] = 0.00003m,
        ["gpt-4"] = 0.00006m,

        // OpenAI - GPT-3.5
        ["gpt-3.5-turbo"] = 0.0000015m,

        // Groq
        ["llama-3.1-8b-instant"] = 0.00000008m,
        ["llama-3.3-70b-versatile"] = 0.00000079m,
        ["llama-guard-4-12b"] = 0.0000002m,
    };

    /// <summary>
    /// List of all transcription model names.
    /// </summary>
    public static IEnumerable<string> AllTranscriptionModels => TranscriptionPricePerSecond.Keys;

    /// <summary>
    /// List of all rewording model names.
    /// </summary>
    public static IEnumerable<string> AllRewordingModels => RewordingInputPricePerToken.Keys;

    /// <summary>
    /// Get the cost per second for a transcription model.
    /// </summary>
    public static decimal GetTranscriptionCostPerSecond(string model)
    {
        return TranscriptionPricePerSecond.TryGetValue(model, out var price) ? price : 0m;
    }

    /// <summary>
    /// Get the cost per token (input, output) for a rewording model.
    /// </summary>
    public static (decimal Input, decimal Output) GetRewordingCostPerToken(string model)
    {
        var input = RewordingInputPricePerToken.TryGetValue(model, out var inputPrice) ? inputPrice : 0m;
        var output = RewordingOutputPricePerToken.TryGetValue(model, out var outputPrice) ? outputPrice : 0m;
        return (input, output);
    }

    /// <summary>
    /// Calculate the cost for a transcription.
    /// </summary>
    /// <param name="model">The model used.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <returns>Cost in USD.</returns>
    public static decimal CalculateTranscriptionCost(string model, long durationMs)
    {
        var durationSeconds = durationMs / 1000.0;
        if (TranscriptionPricePerSecond.TryGetValue(model, out var pricePerSecond))
        {
            return pricePerSecond * (decimal)durationSeconds;
        }
        return 0m;
    }

    /// <summary>
    /// Calculate the cost for a rewording operation.
    /// </summary>
    /// <param name="model">The model used.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <returns>Cost in USD.</returns>
    public static decimal CalculateRewordingCost(string model, int inputTokens, int outputTokens)
    {
        var inputCost = 0m;
        var outputCost = 0m;

        if (RewordingInputPricePerToken.TryGetValue(model, out var inputPrice))
        {
            inputCost = inputPrice * inputTokens;
        }

        if (RewordingOutputPricePerToken.TryGetValue(model, out var outputPrice))
        {
            outputCost = outputPrice * outputTokens;
        }

        return inputCost + outputCost;
    }
}
