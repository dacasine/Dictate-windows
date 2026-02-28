using System.Net.Http.Headers;
using System.Text.Json;
using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Models;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Transcription;

/// <summary>
/// Groq transcription provider using Whisper API (OpenAI-compatible endpoint).
/// </summary>
public class GroqTranscriptionProvider : ITranscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqTranscriptionProvider>? _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public string ProviderName => "Groq";
    public ApiProvider Provider => ApiProvider.Groq;

    public IReadOnlyList<string> AvailableModels => TranscriptionModels.GroqModels;

    public GroqTranscriptionProvider(string apiKey, HttpClient? httpClient = null,
        string? baseUrl = null, ILogger<GroqTranscriptionProvider>? logger = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _httpClient = httpClient ?? new HttpClient();
        _baseUrl = baseUrl ?? ApiEndpoints.GroqBaseUrl;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(ApiEndpoints.TranscriptionTimeoutSeconds);
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string model,
        string language,
        string? prompt = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
        {
            return TranscriptionResult.FromError(TranscriptionError.Unknown, $"Audio file not found: {audioFilePath}");
        }

        try
        {
            var url = $"{_baseUrl.TrimEnd('/')}{ApiEndpoints.GroqTranscription}";

            using var content = new MultipartFormDataContent();

            // Add audio file
            var audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(audioFilePath));
            content.Add(audioContent, "file", Path.GetFileName(audioFilePath));

            // Add model
            content.Add(new StringContent(model), "model");

            // Add response format
            content.Add(new StringContent("verbose_json"), "response_format");

            // Add language if not auto-detect
            if (!string.IsNullOrEmpty(language) &&
                !language.Equals("detect", StringComparison.OrdinalIgnoreCase) &&
                !language.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                content.Add(new StringContent(language), "language");
            }

            // Add prompt if provided
            if (!string.IsNullOrEmpty(prompt))
            {
                content.Add(new StringContent(prompt), "prompt");
            }

            // Create request
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // Send request with retry
            var response = await SendWithRetryAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return HandleErrorResponse(response.StatusCode, responseJson);
            }

            // Parse response
            var result = JsonSerializer.Deserialize<WhisperResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return TranscriptionResult.FromError(TranscriptionError.Unknown, "Invalid API response");
            }

            return TranscriptionResult.FromSuccess(
                result.Text ?? string.Empty,
                result.Duration ?? 0,
                result.Language,
                result.Segments?.Select(s => new TranscriptionSegment
                {
                    Id = s.Id,
                    Start = s.Start,
                    End = s.End,
                    Text = s.Text ?? string.Empty
                }).ToList());
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TranscriptionResult.FromError(TranscriptionError.Timeout, "Request was cancelled");
        }
        catch (TaskCanceledException)
        {
            return TranscriptionResult.FromError(TranscriptionError.Timeout, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error during transcription");
            return TranscriptionResult.FromError(TranscriptionError.NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during transcription");
            return TranscriptionResult.FromError(TranscriptionError.Unknown, ex.Message);
        }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl.TrimEnd('/')}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage? lastResponse = null;
        Exception? lastException = null;

        for (int attempt = 0; attempt < ApiEndpoints.MaxRetries; attempt++)
        {
            try
            {
                using var clonedRequest = await CloneRequestAsync(request);
                lastResponse = await _httpClient.SendAsync(clonedRequest, cancellationToken);

                if (lastResponse.IsSuccessStatusCode || IsNonRetryableError(lastResponse.StatusCode))
                {
                    return lastResponse;
                }

                _logger?.LogWarning("Transcription attempt {Attempt} failed with status {Status}",
                    attempt + 1, lastResponse.StatusCode);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Transcription attempt {Attempt} failed with exception", attempt + 1);
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

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
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

    private static TranscriptionResult HandleErrorResponse(System.Net.HttpStatusCode statusCode, string responseJson)
    {
        var errorMessage = ExtractErrorMessage(responseJson);
        var lowerMessage = errorMessage?.ToLowerInvariant() ?? "";

        if (lowerMessage.Contains("api key") || statusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return TranscriptionResult.FromError(TranscriptionError.InvalidApiKey, errorMessage);
        }

        if (lowerMessage.Contains("quota") || lowerMessage.Contains("rate limit") ||
            statusCode == System.Net.HttpStatusCode.PaymentRequired ||
            statusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return TranscriptionResult.FromError(TranscriptionError.QuotaExceeded, errorMessage);
        }

        if (lowerMessage.Contains("audio duration") || lowerMessage.Contains("content size limit"))
        {
            return TranscriptionResult.FromError(TranscriptionError.ContentSizeLimit, errorMessage);
        }

        if (lowerMessage.Contains("format"))
        {
            return TranscriptionResult.FromError(TranscriptionError.FormatNotSupported, errorMessage);
        }

        return TranscriptionResult.FromError(TranscriptionError.Unknown, errorMessage);
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

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".mp4" => "audio/mp4",
            ".m4a" => "audio/mp4",
            ".mpeg" => "audio/mpeg",
            ".mpga" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };
    }

    private class WhisperResponse
    {
        public string? Text { get; set; }
        public double? Duration { get; set; }
        public string? Language { get; set; }
        public List<WhisperSegment>? Segments { get; set; }
    }

    private class WhisperSegment
    {
        public int Id { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public string? Text { get; set; }
    }
}
