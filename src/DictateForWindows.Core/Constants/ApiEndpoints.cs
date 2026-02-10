namespace DictateForWindows.Core.Constants;

/// <summary>
/// API endpoint URLs for various providers.
/// </summary>
public static class ApiEndpoints
{
    // OpenAI
    public const string OpenAiBaseUrl = "https://api.openai.com/v1";
    public const string OpenAiTranscription = "/audio/transcriptions";
    public const string OpenAiChat = "/chat/completions";

    // Groq
    public const string GroqBaseUrl = "https://api.groq.com/openai/v1";
    public const string GroqTranscription = "/audio/transcriptions";
    public const string GroqChat = "/chat/completions";

    // Timeouts
    public const int TranscriptionTimeoutSeconds = 120;
    public const int RewordingTimeoutSeconds = 60;
    public const int RetryDelaySeconds = 3;
    public const int MaxRetries = 3;
}

/// <summary>
/// Available transcription models.
/// </summary>
public static class TranscriptionModels
{
    // OpenAI
    public const string Whisper1 = "whisper-1";
    public const string Gpt4oTranscribe = "gpt-4o-transcribe";
    public const string Gpt4oMiniTranscribe = "gpt-4o-mini-transcribe";

    // Groq
    public const string WhisperLargeV3Turbo = "whisper-large-v3-turbo";
    public const string WhisperLargeV3 = "whisper-large-v3";

    public static readonly string[] OpenAiModels = [Whisper1, Gpt4oTranscribe, Gpt4oMiniTranscribe];
    public static readonly string[] GroqModels = [WhisperLargeV3Turbo, WhisperLargeV3];
}

/// <summary>
/// Available rewording models.
/// </summary>
public static class RewordingModels
{
    // OpenAI - Reasoning
    public const string O4Mini = "o4-mini";
    public const string O3Mini = "o3-mini";
    public const string O1 = "o1";
    public const string O1Mini = "o1-mini";

    // OpenAI - GPT-5
    public const string Gpt52 = "gpt-5.2";
    public const string Gpt5 = "gpt-5";
    public const string Gpt5Mini = "gpt-5-mini";

    // OpenAI - GPT-4
    public const string Gpt4o = "gpt-4o";
    public const string Gpt4oMini = "gpt-4o-mini";
    public const string Gpt4Turbo = "gpt-4-turbo";
    public const string Gpt4 = "gpt-4";

    // OpenAI - GPT-3.5
    public const string Gpt35Turbo = "gpt-3.5-turbo";

    // Groq - Llama
    public const string Llama31_8bInstant = "llama-3.1-8b-instant";
    public const string Llama33_70bVersatile = "llama-3.3-70b-versatile";
    public const string LlamaGuard4 = "llama-guard-4-12b";

    public static readonly string[] OpenAiModels =
    [
        O4Mini, O3Mini, O1, O1Mini,
        Gpt52, Gpt5, Gpt5Mini,
        Gpt4o, Gpt4oMini, Gpt4Turbo, Gpt4,
        Gpt35Turbo
    ];

    public static readonly string[] GroqModels =
    [
        Llama31_8bInstant, Llama33_70bVersatile, LlamaGuard4
    ];
}
