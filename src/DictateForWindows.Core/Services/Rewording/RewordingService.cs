using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Settings;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Rewording;

/// <summary>
/// Service for rewording text using AI chat models.
/// </summary>
public class RewordingService : IRewordingService
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RewordingService>? _logger;

    /// <summary>
    /// Default system prompt for precise rewording.
    /// </summary>
    public const string DefaultSystemPrompt =
        "Be accurate with your output. Only output exactly what was requested, no additional text.";

    /// <summary>
    /// Auto-formatting prompt for converting voice commands to formatting.
    /// </summary>
    public const string AutoFormattingPrompt = """
        You are a text formatter. Convert spoken formatting commands into actual formatting.
        Rules:
        - "new line" or "newline" → insert a line break
        - "new paragraph" → insert two line breaks
        - "period" or "full stop" → .
        - "comma" → ,
        - "question mark" → ?
        - "exclamation mark" or "exclamation point" → !
        - "colon" → :
        - "semicolon" → ;
        - "open parenthesis" or "open bracket" → (
        - "close parenthesis" or "close bracket" → )
        - "open quote" or "quote" → "
        - "close quote" or "end quote" → "
        - "hyphen" or "dash" → -
        - "bold [text] end bold" → **text**
        - "italic [text] end italic" → *text*
        - "underline [text] end underline" → _text_
        - Numbers spoken as words should remain as words unless followed by "as number"

        Only output the formatted text, nothing else.
        """;

    public RewordingService(
        ISettingsService settings,
        HttpClient? httpClient = null,
        ILogger<RewordingService>? logger = null)
    {
        _settings = settings;
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(ApiEndpoints.RewordingTimeoutSeconds);
    }

    /// <summary>
    /// Reword text using the configured AI model.
    /// </summary>
    public async Task<RewordingResult> RewordAsync(
        string text,
        string prompt,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var provider = _settings.ApiProvider;
        var model = GetModelForProvider(provider);
        var apiKey = GetApiKeyForProvider(provider);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RewordingResult.FromError(RewordingError.InvalidApiKey,
                "No API key configured. Please configure an API key in settings.");
        }

        return await RewordInternalAsync(text, prompt, systemPrompt, provider, model, apiKey, cancellationToken);
    }

    /// <summary>
    /// Reword with explicit parameters.
    /// </summary>
    public async Task<RewordingResult> RewordAsync(
        string text,
        string prompt,
        ApiProvider provider,
        string model,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKeyForProvider(provider);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RewordingResult.FromError(RewordingError.InvalidApiKey,
                $"No API key configured for {provider}.");
        }

        return await RewordInternalAsync(text, prompt, systemPrompt, provider, model, apiKey, cancellationToken);
    }

    /// <summary>
    /// Apply auto-formatting to transcribed text.
    /// </summary>
    public async Task<RewordingResult> ApplyAutoFormattingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return await RewordAsync(text, text, AutoFormattingPrompt, cancellationToken);
    }

    /// <summary>
    /// Get available models for a provider.
    /// </summary>
    public IReadOnlyList<string> GetAvailableModels(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.OpenAI => RewordingModels.OpenAiModels,
            ApiProvider.Groq => RewordingModels.GroqModels,
            ApiProvider.Custom => [_settings.GetString(SettingsKeys.CustomRewordingModel, "gpt-4o-mini")],
            _ => RewordingModels.OpenAiModels
        };
    }

    private async Task<RewordingResult> RewordInternalAsync(
        string text,
        string prompt,
        string? systemPrompt,
        ApiProvider provider,
        string model,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = GetBaseUrlForProvider(provider);
            var url = $"{baseUrl.TrimEnd('/')}{ApiEndpoints.OpenAiChat}";

            // Build the full prompt
            var fullPrompt = BuildFullPrompt(prompt, text);

            // Build request body
            var requestBody = BuildRequestBody(model, fullPrompt, systemPrompt);
            var json = JsonSerializer.Serialize(requestBody);

            _logger?.LogInformation(
                "Starting rewording with provider={Provider}, model={Model}",
                provider, model);

            // Send request with retry
            var response = await SendWithRetryAsync(url, json, apiKey, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return HandleErrorResponse(response.StatusCode, responseJson);
            }

            // Parse response
            var result = ParseChatResponse(responseJson);
            return result;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return RewordingResult.FromError(RewordingError.Timeout, "Request was cancelled");
        }
        catch (TaskCanceledException)
        {
            return RewordingResult.FromError(RewordingError.Timeout, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error during rewording");
            return RewordingResult.FromError(RewordingError.NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during rewording");
            return RewordingResult.FromError(RewordingError.Unknown, ex.Message);
        }
    }

    private static string BuildFullPrompt(string prompt, string text)
    {
        if (string.IsNullOrEmpty(prompt) || prompt == text)
        {
            return text;
        }

        return $"{prompt}\n\n{text}";
    }

    private static object BuildRequestBody(string model, string userMessage, string? systemPrompt)
    {
        var messages = new List<object>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        messages.Add(new { role = "user", content = userMessage });

        return new
        {
            model,
            messages
        };
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string url,
        string jsonBody,
        string apiKey,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? lastResponse = null;
        Exception? lastException = null;

        for (int attempt = 0; attempt < ApiEndpoints.MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                lastResponse = await _httpClient.SendAsync(request, cancellationToken);

                if (lastResponse.IsSuccessStatusCode || IsNonRetryableError(lastResponse.StatusCode))
                {
                    return lastResponse;
                }

                _logger?.LogWarning("Rewording attempt {Attempt} failed with status {Status}",
                    attempt + 1, lastResponse.StatusCode);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Rewording attempt {Attempt} failed with exception", attempt + 1);
            }

            if (attempt < ApiEndpoints.MaxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(ApiEndpoints.RetryDelaySeconds), cancellationToken);
            }
        }

        if (lastResponse != null)
        {
            return lastResponse;
        }

        throw lastException ?? new HttpRequestException("All retry attempts failed");
    }

    private static bool IsNonRetryableError(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => true,
            System.Net.HttpStatusCode.Forbidden => true,
            System.Net.HttpStatusCode.BadRequest => true,
            System.Net.HttpStatusCode.PaymentRequired => true,
            _ => false
        };
    }

    private static RewordingResult ParseChatResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Get the message content
            var text = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            // Get token usage
            var usage = root.GetProperty("usage");
            var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

            return RewordingResult.FromSuccess(text, inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            return RewordingResult.FromError(RewordingError.Unknown, $"Failed to parse response: {ex.Message}");
        }
    }

    private static RewordingResult HandleErrorResponse(System.Net.HttpStatusCode statusCode, string responseJson)
    {
        var errorMessage = ExtractErrorMessage(responseJson);
        var lowerMessage = errorMessage?.ToLowerInvariant() ?? "";

        if (lowerMessage.Contains("api key") || statusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return RewordingResult.FromError(RewordingError.InvalidApiKey, errorMessage);
        }

        if (lowerMessage.Contains("quota") || lowerMessage.Contains("rate limit") ||
            statusCode == System.Net.HttpStatusCode.PaymentRequired ||
            statusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return RewordingResult.FromError(RewordingError.QuotaExceeded, errorMessage);
        }

        if (lowerMessage.Contains("content") && (lowerMessage.Contains("policy") || lowerMessage.Contains("filter")))
        {
            return RewordingResult.FromError(RewordingError.ContentFiltered, errorMessage);
        }

        if (lowerMessage.Contains("length") || lowerMessage.Contains("too long"))
        {
            return RewordingResult.FromError(RewordingError.ContentTooLong, errorMessage);
        }

        return RewordingResult.FromError(RewordingError.Unknown, errorMessage);
    }

    private static string? ExtractErrorMessage(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return responseJson;
    }

    private string GetApiKeyForProvider(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.OpenAI => _settings.OpenAiApiKey,
            ApiProvider.Groq => _settings.GroqApiKey,
            ApiProvider.Custom => _settings.CustomApiKey,
            _ => _settings.OpenAiApiKey
        };
    }

    private string GetModelForProvider(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.OpenAI => _settings.GetString(SettingsKeys.RewordingModel, RewordingModels.Gpt4oMini),
            ApiProvider.Groq => _settings.GetString(SettingsKeys.RewordingModel, RewordingModels.Llama33_70bVersatile),
            ApiProvider.Custom => _settings.GetString(SettingsKeys.CustomRewordingModel, "gpt-4o-mini"),
            _ => _settings.RewordingModel
        };
    }

    private string GetBaseUrlForProvider(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.OpenAI => ApiEndpoints.OpenAiBaseUrl,
            ApiProvider.Groq => ApiEndpoints.GroqBaseUrl,
            ApiProvider.Custom => _settings.CustomApiHost,
            _ => ApiEndpoints.OpenAiBaseUrl
        };
    }
}

/// <summary>
/// Interface for rewording service.
/// </summary>
public interface IRewordingService
{
    Task<RewordingResult> RewordAsync(
        string text,
        string prompt,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<RewordingResult> RewordAsync(
        string text,
        string prompt,
        ApiProvider provider,
        string model,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<RewordingResult> ApplyAutoFormattingAsync(
        string text,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetAvailableModels(ApiProvider provider);
}
