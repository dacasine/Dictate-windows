using System.Text.Json;
using System.Text.Json.Serialization;
using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Settings;

/// <summary>
/// Settings service that persists configuration to a JSON file.
/// </summary>
public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly object _lock = new();
    private Dictionary<string, JsonElement> _settings;
    private bool _isDirty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public JsonSettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? GetDefaultSettingsPath();
        _settings = new Dictionary<string, JsonElement>();
        Load();
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dictateFolder = Path.Combine(appData, "DictateForWindows");
        Directory.CreateDirectory(dictateFolder);
        return Path.Combine(dictateFolder, "settings.json");
    }

    /// <summary>
    /// Loads settings from the JSON file.
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)
                                ?? new Dictionary<string, JsonElement>();
                }
                catch (JsonException)
                {
                    _settings = new Dictionary<string, JsonElement>();
                }
            }
            else
            {
                _settings = new Dictionary<string, JsonElement>();
            }
            _isDirty = false;
        }
    }

    /// <summary>
    /// Saves settings to the JSON file.
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            if (!_isDirty) return;

            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
            _isDirty = false;
        }
    }

    public string GetString(string key, string defaultValue = "")
    {
        lock (_lock)
        {
            if (_settings.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? defaultValue;
            }
            return defaultValue;
        }
    }

    public void SetString(string key, string value)
    {
        lock (_lock)
        {
            _settings[key] = JsonSerializer.SerializeToElement(value);
            _isDirty = true;
        }
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        lock (_lock)
        {
            if (_settings.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }
            return defaultValue;
        }
    }

    public void SetInt(string key, int value)
    {
        lock (_lock)
        {
            _settings[key] = JsonSerializer.SerializeToElement(value);
            _isDirty = true;
        }
    }

    public long GetLong(string key, long defaultValue = 0)
    {
        lock (_lock)
        {
            if (_settings.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt64();
            }
            return defaultValue;
        }
    }

    public void SetLong(string key, long value)
    {
        lock (_lock)
        {
            _settings[key] = JsonSerializer.SerializeToElement(value);
            _isDirty = true;
        }
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        lock (_lock)
        {
            if (_settings.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }
    }

    public void SetBool(string key, bool value)
    {
        lock (_lock)
        {
            _settings[key] = JsonSerializer.SerializeToElement(value);
            _isDirty = true;
        }
    }

    public double GetDouble(string key, double defaultValue = 0.0)
    {
        lock (_lock)
        {
            if (_settings.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number)
            {
                return element.GetDouble();
            }
            return defaultValue;
        }
    }

    public void SetDouble(string key, double value)
    {
        lock (_lock)
        {
            _settings[key] = JsonSerializer.SerializeToElement(value);
            _isDirty = true;
        }
    }

    public T? Get<T>(string key, T? defaultValue = default)
    {
        lock (_lock)
        {
            if (_settings.TryGetValue(key, out var element))
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions);
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            _settings[key] = JsonSerializer.SerializeToElement(value, JsonOptions);
            _isDirty = true;
        }
    }

    public bool ContainsKey(string key)
    {
        lock (_lock)
        {
            return _settings.ContainsKey(key);
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_settings.Remove(key))
            {
                _isDirty = true;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _settings.Clear();
            _isDirty = true;
        }
    }

    // Convenience properties for common settings

    public ApiProvider ApiProvider
    {
        get => (ApiProvider)GetInt(SettingsKeys.ApiProvider, SettingsDefaults.ApiProvider);
        set => SetInt(SettingsKeys.ApiProvider, (int)value);
    }

    public string OpenAiApiKey
    {
        get => GetString(SettingsKeys.OpenAiApiKey);
        set => SetString(SettingsKeys.OpenAiApiKey, SanitizeApiKey(value));
    }

    public string GroqApiKey
    {
        get => GetString(SettingsKeys.GroqApiKey);
        set => SetString(SettingsKeys.GroqApiKey, SanitizeApiKey(value));
    }

    public string CustomApiKey
    {
        get => GetString(SettingsKeys.CustomApiKey);
        set => SetString(SettingsKeys.CustomApiKey, SanitizeApiKey(value));
    }

    public string CustomApiHost
    {
        get => GetString(SettingsKeys.CustomApiHost);
        set => SetString(SettingsKeys.CustomApiHost, value);
    }

    public string TranscriptionModel
    {
        get => GetString(SettingsKeys.TranscriptionModel, SettingsDefaults.TranscriptionModel);
        set => SetString(SettingsKeys.TranscriptionModel, value);
    }

    public string RewordingModel
    {
        get => GetString(SettingsKeys.RewordingModel, SettingsDefaults.RewordingModel);
        set => SetString(SettingsKeys.RewordingModel, value);
    }

    public string TranscriptionLanguage
    {
        get => GetString(SettingsKeys.TranscriptionLanguage, SettingsDefaults.TranscriptionLanguage);
        set => SetString(SettingsKeys.TranscriptionLanguage, value);
    }

    public string Theme
    {
        get => GetString(SettingsKeys.Theme, SettingsDefaults.Theme);
        set => SetString(SettingsKeys.Theme, value);
    }

    public string AccentColor
    {
        get => GetString(SettingsKeys.AccentColor, SettingsDefaults.AccentColor);
        set => SetString(SettingsKeys.AccentColor, value);
    }

    public string OrbFont
    {
        get => GetString(SettingsKeys.OrbFont, SettingsDefaults.OrbFont);
        set => SetString(SettingsKeys.OrbFont, value);
    }

    public bool PreferBluetoothMic
    {
        get => GetBool(SettingsKeys.PreferBluetoothMic, SettingsDefaults.PreferBluetoothMic);
        set => SetBool(SettingsKeys.PreferBluetoothMic, value);
    }

    public int BluetoothTimeoutMs
    {
        get => GetInt(SettingsKeys.BluetoothTimeoutMs, SettingsDefaults.BluetoothTimeoutMs);
        set => SetInt(SettingsKeys.BluetoothTimeoutMs, value);
    }

    public bool AutoEnter
    {
        get => GetBool(SettingsKeys.AutoEnter, SettingsDefaults.AutoEnter);
        set => SetBool(SettingsKeys.AutoEnter, value);
    }

    public bool InstantMode
    {
        get => GetBool(SettingsKeys.InstantMode, SettingsDefaults.InstantMode);
        set => SetBool(SettingsKeys.InstantMode, value);
    }

    public int AnimationSpeed
    {
        get => GetInt(SettingsKeys.AnimationSpeed, SettingsDefaults.AnimationSpeed);
        set => SetInt(SettingsKeys.AnimationSpeed, value);
    }

    public bool AutoFormattingEnabled
    {
        get => GetBool(SettingsKeys.AutoFormattingEnabled, SettingsDefaults.AutoFormattingEnabled);
        set => SetBool(SettingsKeys.AutoFormattingEnabled, value);
    }

    public string ActivationHotkey
    {
        get => GetString(SettingsKeys.ActivationHotkey, SettingsDefaults.ActivationHotkey);
        set => SetString(SettingsKeys.ActivationHotkey, value);
    }

    public bool UseCopilotKey
    {
        get => GetBool(SettingsKeys.UseCopilotKey, SettingsDefaults.UseCopilotKey);
        set => SetBool(SettingsKeys.UseCopilotKey, value);
    }

    public bool FirstRun
    {
        get => GetBool(SettingsKeys.FirstRun, SettingsDefaults.FirstRun);
        set => SetBool(SettingsKeys.FirstRun, value);
    }

    public bool OnboardingComplete
    {
        get => GetBool(SettingsKeys.OnboardingComplete, SettingsDefaults.OnboardingComplete);
        set => SetBool(SettingsKeys.OnboardingComplete, value);
    }

    public bool StartWithWindows
    {
        get => GetBool(SettingsKeys.StartWithWindows, SettingsDefaults.StartWithWindows);
        set => SetBool(SettingsKeys.StartWithWindows, value);
    }

    /// <summary>
    /// Gets the API key for the current provider.
    /// </summary>
    public string GetCurrentApiKey()
    {
        return ApiProvider switch
        {
            Models.ApiProvider.OpenAI => OpenAiApiKey,
            Models.ApiProvider.Groq => GroqApiKey,
            Models.ApiProvider.Custom => CustomApiKey,
            _ => OpenAiApiKey
        };
    }

    /// <summary>
    /// Sanitize API key by removing non-printable characters.
    /// </summary>
    private static string SanitizeApiKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        // Remove non-ASCII printable characters (same as Android: replaceAll("[^ -~]", ""))
        return new string(key.Where(c => c >= ' ' && c <= '~').ToArray());
    }
}

/// <summary>
/// Interface for settings service.
/// </summary>
public interface ISettingsService
{
    void Load();
    void Save();
    string GetString(string key, string defaultValue = "");
    void SetString(string key, string value);
    int GetInt(string key, int defaultValue = 0);
    void SetInt(string key, int value);
    long GetLong(string key, long defaultValue = 0);
    void SetLong(string key, long value);
    bool GetBool(string key, bool defaultValue = false);
    void SetBool(string key, bool value);
    double GetDouble(string key, double defaultValue = 0.0);
    void SetDouble(string key, double value);
    T? Get<T>(string key, T? defaultValue = default);
    void Set<T>(string key, T value);
    bool ContainsKey(string key);
    void Remove(string key);
    void Clear();

    // Common settings
    ApiProvider ApiProvider { get; set; }
    string OpenAiApiKey { get; set; }
    string GroqApiKey { get; set; }
    string CustomApiKey { get; set; }
    string CustomApiHost { get; set; }
    string TranscriptionModel { get; set; }
    string RewordingModel { get; set; }
    string TranscriptionLanguage { get; set; }
    string Theme { get; set; }
    string AccentColor { get; set; }
    string OrbFont { get; set; }
    bool PreferBluetoothMic { get; set; }
    int BluetoothTimeoutMs { get; set; }
    bool AutoEnter { get; set; }
    bool InstantMode { get; set; }
    int AnimationSpeed { get; set; }
    bool AutoFormattingEnabled { get; set; }
    string ActivationHotkey { get; set; }
    bool UseCopilotKey { get; set; }
    bool FirstRun { get; set; }
    bool OnboardingComplete { get; set; }
    bool StartWithWindows { get; set; }
    string GetCurrentApiKey();
}
