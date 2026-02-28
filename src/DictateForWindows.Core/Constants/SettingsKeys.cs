namespace DictateForWindows.Core.Constants;

/// <summary>
/// All settings keys used by the application.
/// Mirrors the Android SharedPreferences keys for compatibility.
/// </summary>
public static class SettingsKeys
{
    // API Settings
    public const string ApiProvider = "dictate_api_provider";
    public const string OpenAiApiKey = "dictate_openai_api_key";
    public const string GroqApiKey = "dictate_groq_api_key";
    public const string CustomApiKey = "dictate_custom_api_key";
    public const string CustomApiHost = "dictate_custom_api_host";

    // Model Selection
    public const string TranscriptionModel = "dictate_transcription_model";
    public const string RewordingModel = "dictate_rewording_model";
    public const string CustomTranscriptionModel = "dictate_custom_transcription_model";
    public const string CustomRewordingModel = "dictate_custom_rewording_model";

    // Language Settings
    public const string TranscriptionLanguage = "dictate_transcription_language";
    public const string AppLanguage = "dictate_app_language";

    // Audio Settings
    public const string PreferBluetoothMic = "dictate_prefer_bluetooth_mic";
    public const string BluetoothTimeoutMs = "dictate_bluetooth_timeout_ms";
    public const string AudioInputDevice = "dictate_audio_input_device";

    // UI Settings
    public const string Theme = "dictate_theme";
    public const string AccentColor = "dictate_accent_color";
    public const string ShowPromptsPanel = "dictate_show_prompts_panel";
    public const string ShowVirtualButtons = "dictate_show_virtual_buttons";
    public const string AnimateTextOutput = "dictate_animate_text_output";
    public const string AnimationSpeed = "dictate_animation_speed";
    public const string OrbFont = "dictate_orb_font";

    // Behavior Settings
    public const string AutoEnter = "dictate_auto_enter";
    public const string AutoCapitalize = "dictate_auto_capitalize";
    public const string AutoPunctuation = "dictate_auto_punctuation";
    public const string InstantMode = "dictate_instant_mode";
    public const string SuppressPopupOnStart = "dictate_suppress_popup_on_start";

    // Hotkey Settings
    public const string ActivationHotkey = "dictate_activation_hotkey";
    public const string UseCopilotKey = "dictate_use_copilot_key";

    // System Prompts
    public const string SystemPromptMode = "dictate_system_prompt_mode";
    public const string CustomSystemPrompt = "dictate_custom_system_prompt";
    public const string AutoFormattingEnabled = "dictate_auto_formatting_enabled";

    // Proxy Settings
    public const string UseProxy = "dictate_use_proxy";
    public const string ProxyType = "dictate_proxy_type";
    public const string ProxyHost = "dictate_proxy_host";
    public const string ProxyPort = "dictate_proxy_port";
    public const string ProxyUsername = "dictate_proxy_username";
    public const string ProxyPassword = "dictate_proxy_password";

    // Window Settings
    public const string PopupWidth = "dictate_popup_width";
    public const string PopupHeight = "dictate_popup_height";
    public const string RememberPopupPosition = "dictate_remember_popup_position";
    public const string PopupPositionX = "dictate_popup_position_x";
    public const string PopupPositionY = "dictate_popup_position_y";

    // First Run
    public const string FirstRun = "dictate_first_run";
    public const string OnboardingComplete = "dictate_onboarding_complete";

    // Startup
    public const string StartWithWindows = "dictate_start_with_windows";
    public const string StartMinimized = "dictate_start_minimized";

    // Context & Target Apps
    public const string AutoScreenshotOnNoClipboard = "dictate_auto_screenshot_no_clipboard";
    public const string OcrLanguage = "dictate_ocr_language";
    public const string DefaultTargetApp = "dictate_default_target_app";

    // Active Prompt
    public const string ActivePromptId = "dictate_active_prompt_id";

    // Prosody Formatting (experimental)
    public const string ProsodyFormatting = "dictate_prosody_formatting";

    // Hesitation Intelligence (experimental)
    public const string HesitationAnalysis = "dictate_hesitation_analysis";

    // Voice Branching (experimental)
    public const string VoiceBranching = "dictate_voice_branching";

    // Contextual Auto-Formatting (experimental)
    public const string ContextualFormatting = "dictate_contextual_formatting";

    // Emotional Watermarking (experimental)
    public const string EmotionalWatermarking = "dictate_emotional_watermarking";
}

/// <summary>
/// Default values for settings.
/// </summary>
public static class SettingsDefaults
{
    public const int ApiProvider = 0; // OpenAI
    public const string TranscriptionModel = "whisper-1";
    public const string RewordingModel = "gpt-4o-mini";
    public const string TranscriptionLanguage = "fr";
    public const string AppLanguage = "system";
    public const bool PreferBluetoothMic = true;
    public const int BluetoothTimeoutMs = 2500;
    public const string Theme = "system";
    public const string AccentColor = "#0078D4";
    public const bool ShowPromptsPanel = true;
    public const bool ShowVirtualButtons = true;
    public const bool AnimateTextOutput = false;
    public const int AnimationSpeed = 5;
    public const string OrbFont = "Cascadia Code";
    public const bool AutoEnter = false;
    public const bool AutoCapitalize = true;
    public const bool AutoPunctuation = true;
    public const bool InstantMode = true;
    public const bool SuppressPopupOnStart = false;
    public const string ActivationHotkey = "Win+Shift+D";
    public const bool UseCopilotKey = false;
    public const int SystemPromptMode = 0; // None
    public const string CustomSystemPrompt = "";
    public const bool AutoFormattingEnabled = false;
    public const bool UseProxy = false;
    public const string ProxyType = "http";
    public const string ProxyHost = "";
    public const int ProxyPort = 8080;
    public const int PopupWidth = 400;
    public const int PopupHeight = 200;
    public const bool RememberPopupPosition = false;
    public const bool FirstRun = true;
    public const bool OnboardingComplete = false;
    public const bool StartWithWindows = true;
    public const bool StartMinimized = false;
    public const bool AutoScreenshotOnNoClipboard = true;
    public const string OcrLanguage = "auto";
    public const string DefaultTargetApp = "";
    public const int ActivePromptId = 0; // 0 = Default prompt
    public const bool ProsodyFormatting = false; // Experimental: prosody → typography
    public const bool HesitationAnalysis = false; // Experimental: hesitation intelligence
    public const bool VoiceBranching = false; // Experimental: voice branching (voice git)
    public const bool ContextualFormatting = false; // Experimental: style by target app
    public const bool EmotionalWatermarking = false; // Experimental: emotion detection from prosody
}
