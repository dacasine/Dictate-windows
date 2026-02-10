using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Settings;
using DictateForWindows.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Transcription;

/// <summary>
/// Orchestrates transcription using the configured provider.
/// </summary>
public class TranscriptionService : ITranscriptionService
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "transcription.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private readonly ISettingsService _settings;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<TranscriptionService>? _logger;

    public TranscriptionService(
        ISettingsService settings,
        IHttpClientFactory? httpClientFactory = null,
        ILogger<TranscriptionService>? logger = null)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Transcribe an audio file using the configured provider.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        Log($"TranscribeAsync called with audioFilePath: {audioFilePath}");
        Log($"File exists: {File.Exists(audioFilePath)}");
        if (File.Exists(audioFilePath))
        {
            var fileInfo = new FileInfo(audioFilePath);
            Log($"File size: {fileInfo.Length} bytes");
        }

        Log($"Creating provider for {_settings.ApiProvider}...");
        var provider = CreateProvider();
        if (provider == null)
        {
            Log("ERROR: No provider created (no API key configured)");
            return TranscriptionResult.FromError(
                TranscriptionError.InvalidApiKey,
                "No API key configured. Please configure an API key in settings.");
        }
        Log($"Provider created: {provider.ProviderName}");

        var model = GetModelForProvider(_settings.ApiProvider);
        var language = _settings.TranscriptionLanguage;
        var prompt = GetStylePrompt(language);

        Log($"Calling provider.TranscribeAsync with model={model}, language={language}");
        _logger?.LogInformation(
            "Starting transcription with provider={Provider}, model={Model}, language={Language}",
            provider.ProviderName, model, language);

        try
        {
            var result = await provider.TranscribeAsync(audioFilePath, model, language, prompt, cancellationToken);
            Log($"Transcription result: IsSuccess={result.IsSuccess}, Text={result.Text?.Substring(0, Math.Min(100, result.Text?.Length ?? 0))}, Error={result.Error}");
            return result;
        }
        catch (Exception ex)
        {
            Log($"ERROR in TranscribeAsync: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Transcribe with explicit parameters.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        ApiProvider apiProvider,
        string model,
        string language,
        CancellationToken cancellationToken = default)
    {
        var provider = CreateProvider(apiProvider);
        if (provider == null)
        {
            return TranscriptionResult.FromError(
                TranscriptionError.InvalidApiKey,
                $"No API key configured for {apiProvider}.");
        }

        var prompt = GetStylePrompt(language);
        return await provider.TranscribeAsync(audioFilePath, model, language, prompt, cancellationToken);
    }

    /// <summary>
    /// Get available models for a provider.
    /// </summary>
    public IReadOnlyList<string> GetAvailableModels(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.OpenAI => TranscriptionModels.OpenAiModels,
            ApiProvider.Groq => TranscriptionModels.GroqModels,
            ApiProvider.Custom => [_settings.GetString(SettingsKeys.CustomTranscriptionModel, "whisper-1")],
            _ => TranscriptionModels.OpenAiModels
        };
    }

    /// <summary>
    /// Validate the API key for a provider.
    /// </summary>
    public async Task<bool> ValidateApiKeyAsync(ApiProvider provider, string apiKey)
    {
        var transcriptionProvider = CreateProvider(provider, apiKey);
        if (transcriptionProvider == null)
        {
            return false;
        }

        return await transcriptionProvider.ValidateApiKeyAsync(apiKey);
    }

    private ITranscriptionProvider? CreateProvider(ApiProvider? providerOverride = null, string? apiKeyOverride = null)
    {
        var provider = providerOverride ?? _settings.ApiProvider;
        var apiKey = apiKeyOverride ?? GetApiKeyForProvider(provider);

        Log($"CreateProvider: provider={provider}, apiKey={(string.IsNullOrWhiteSpace(apiKey) ? "EMPTY" : $"{apiKey.Substring(0, Math.Min(8, apiKey.Length))}...")}");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log("CreateProvider: API key is empty, returning null");
            return null;
        }

        var httpClient = _httpClientFactory?.CreateClient() ?? new HttpClient();
        ConfigureProxy(httpClient);

        return provider switch
        {
            ApiProvider.OpenAI => new OpenAITranscriptionProvider(apiKey, httpClient,
                logger: _logger as ILogger<OpenAITranscriptionProvider>),
            ApiProvider.Groq => new GroqTranscriptionProvider(apiKey, httpClient,
                logger: _logger as ILogger<GroqTranscriptionProvider>),
            ApiProvider.Custom => new OpenAITranscriptionProvider(apiKey, httpClient,
                _settings.CustomApiHost, _logger as ILogger<OpenAITranscriptionProvider>),
            _ => new OpenAITranscriptionProvider(apiKey, httpClient,
                logger: _logger as ILogger<OpenAITranscriptionProvider>)
        };
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
            ApiProvider.OpenAI => _settings.GetString(SettingsKeys.TranscriptionModel, TranscriptionModels.Whisper1),
            ApiProvider.Groq => _settings.GetString(SettingsKeys.TranscriptionModel, TranscriptionModels.WhisperLargeV3Turbo),
            ApiProvider.Custom => _settings.GetString(SettingsKeys.CustomTranscriptionModel, "whisper-1"),
            _ => _settings.TranscriptionModel
        };
    }

    private string GetStylePrompt(string language)
    {
        var promptMode = _settings.GetInt(SettingsKeys.SystemPromptMode, 1);

        return promptMode switch
        {
            1 => PunctuationPrompts.GetPromptForLanguage(language),
            2 => _settings.GetString(SettingsKeys.CustomSystemPrompt, ""),
            _ => ""
        };
    }

    private void ConfigureProxy(HttpClient httpClient)
    {
        // Note: Proxy configuration needs to be done at HttpClientHandler level
        // For now, we rely on system proxy settings
        // TODO: Implement custom proxy configuration from settings
    }
}

/// <summary>
/// Interface for transcription service.
/// </summary>
public interface ITranscriptionService
{
    Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default);

    Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        ApiProvider apiProvider,
        string model,
        string language,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetAvailableModels(ApiProvider provider);

    Task<bool> ValidateApiKeyAsync(ApiProvider provider, string apiKey);
}
