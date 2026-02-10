using CommunityToolkit.Mvvm.ComponentModel;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Settings;

namespace DictateForWindows.ViewModels;

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private ApiProvider _apiProvider;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _transcriptionModel = string.Empty;

    [ObservableProperty]
    private string _rewordingModel = string.Empty;

    [ObservableProperty]
    private string _transcriptionLanguage = "detect";

    [ObservableProperty]
    private string _theme = "system";

    [ObservableProperty]
    private bool _instantMode = true;

    [ObservableProperty]
    private bool _autoEnter;

    [ObservableProperty]
    private int _animationSpeed = 5;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        LoadSettings();
    }

    private void LoadSettings()
    {
        ApiProvider = _settings.ApiProvider;
        ApiKey = _settings.GetCurrentApiKey();
        TranscriptionModel = _settings.TranscriptionModel;
        RewordingModel = _settings.RewordingModel;
        TranscriptionLanguage = _settings.TranscriptionLanguage;
        Theme = _settings.Theme;
        InstantMode = _settings.InstantMode;
        AutoEnter = _settings.AutoEnter;
        AnimationSpeed = _settings.AnimationSpeed;
    }

    public void Save()
    {
        _settings.ApiProvider = ApiProvider;
        _settings.TranscriptionModel = TranscriptionModel;
        _settings.RewordingModel = RewordingModel;
        _settings.TranscriptionLanguage = TranscriptionLanguage;
        _settings.Theme = Theme;
        _settings.InstantMode = InstantMode;
        _settings.AutoEnter = AutoEnter;
        _settings.AnimationSpeed = AnimationSpeed;
        _settings.Save();
    }
}
