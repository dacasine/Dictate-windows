using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Settings;
using DictateForWindows.Core.Services.Transcription;
using DictateForWindows.Core.Services.Rewording;
using DictateForWindows.Core.Utilities;
using Windows.System;

namespace DictateForWindows.Views;

/// <summary>
/// Settings window for configuring the application.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IRewordingService _rewordingService;
    private bool _isLoading = true;

    public SettingsWindow()
    {
        InitializeComponent();

        _settings = App.Current.Services.GetRequiredService<ISettingsService>();
        _transcriptionService = App.Current.Services.GetRequiredService<ITranscriptionService>();
        _rewordingService = App.Current.Services.GetRequiredService<IRewordingService>();

        this.SetWindowSize(900, 700);
        this.Title = "Dictate Settings";

        // Select first tab
        SettingsNav.SelectedItem = SettingsNav.MenuItems[0];

        LoadSettings();
        _isLoading = false;
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            ApiTab.Visibility = tag == "api" ? Visibility.Visible : Visibility.Collapsed;
            GeneralTab.Visibility = tag == "general" ? Visibility.Visible : Visibility.Collapsed;
            AboutTab.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void LoadSettings()
    {
        // API Provider
        ApiProviderComboBox.SelectedIndex = (int)_settings.ApiProvider;
        UpdateApiKeyBox();
        UpdateModels();

        // Custom host
        CustomHostBox.Text = _settings.CustomApiHost;
        CustomHostPanel.Visibility = _settings.ApiProvider == ApiProvider.Custom
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Language
        LoadLanguages();

        // Hotkey
        HotkeyBox.Text = _settings.ActivationHotkey;
        CopilotKeyToggle.IsOn = _settings.UseCopilotKey;

        // Behavior
        InstantModeToggle.IsOn = _settings.InstantMode;
        AutoEnterToggle.IsOn = _settings.AutoEnter;
        AutoFormattingToggle.IsOn = _settings.AutoFormattingEnabled;
        AnimationSpeedSlider.Value = _settings.AnimationSpeed;
        AnimationSpeedPanel.Visibility = _settings.InstantMode ? Visibility.Collapsed : Visibility.Visible;

        // Theme
        ThemeComboBox.SelectedIndex = _settings.Theme switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0
        };

        SelectItemByTag(OrbFontComboBox, _settings.OrbFont);
        StartWithWindowsToggle.IsOn = _settings.StartWithWindows;

        // Version
        var version = typeof(App).Assembly.GetName().Version;
        VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";
    }

    private void UpdateApiKeyBox()
    {
        var apiKey = _settings.GetCurrentApiKey();
        ApiKeyBox.Password = apiKey;
    }

    private void UpdateModels()
    {
        var provider = _settings.ApiProvider;

        // Transcription models
        var transcriptionModels = _transcriptionService.GetAvailableModels(provider);
        TranscriptionModelComboBox.Items.Clear();
        foreach (var model in transcriptionModels)
        {
            TranscriptionModelComboBox.Items.Add(new ComboBoxItem { Content = model, Tag = model });
        }
        SelectModelInComboBox(TranscriptionModelComboBox, _settings.TranscriptionModel);

        // Rewording models
        var rewordingModels = _rewordingService.GetAvailableModels(provider);
        RewordingModelComboBox.Items.Clear();
        foreach (var model in rewordingModels)
        {
            RewordingModelComboBox.Items.Add(new ComboBoxItem { Content = model, Tag = model });
        }
        SelectModelInComboBox(RewordingModelComboBox, _settings.RewordingModel);
    }

    private void LoadLanguages()
    {
        LanguageComboBox.Items.Clear();
        foreach (var lang in SupportedLanguages.All)
        {
            var display = $"{lang.FlagEmoji} {lang.Name}";
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = display, Tag = lang.Code });
        }

        SelectItemByTag(LanguageComboBox, _settings.TranscriptionLanguage);
    }

    private static void SelectModelInComboBox(ComboBox comboBox, string model)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (item.Tag?.ToString() == model)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static void SelectItemByTag(ComboBox comboBox, string tag)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private void OnApiProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        var index = ApiProviderComboBox.SelectedIndex;
        _settings.ApiProvider = (ApiProvider)index;

        CustomHostPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

        UpdateApiKeyBox();
        UpdateModels();
        _settings.Save();
    }

    private void OnApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var key = ApiKeyBox.Password;
        switch (_settings.ApiProvider)
        {
            case ApiProvider.OpenAI:
                _settings.OpenAiApiKey = key;
                break;
            case ApiProvider.Groq:
                _settings.GroqApiKey = key;
                break;
            case ApiProvider.Custom:
                _settings.CustomApiKey = key;
                break;
        }
        _settings.Save();
    }

    private async void OnApiKeyLinkClick(object sender, RoutedEventArgs e)
    {
        var url = _settings.ApiProvider switch
        {
            ApiProvider.OpenAI => "https://platform.openai.com/api-keys",
            ApiProvider.Groq => "https://console.groq.com/keys",
            _ => "https://platform.openai.com/api-keys"
        };

        await Launcher.LaunchUriAsync(new Uri(url));
    }

    private void OnCustomHostChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        _settings.CustomApiHost = CustomHostBox.Text;
        _settings.Save();
    }

    private void OnTranscriptionModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (TranscriptionModelComboBox.SelectedItem is ComboBoxItem item)
        {
            _settings.TranscriptionModel = item.Tag?.ToString() ?? TranscriptionModels.Whisper1;
            _settings.Save();
        }
    }

    private void OnRewordingModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (RewordingModelComboBox.SelectedItem is ComboBoxItem item)
        {
            _settings.RewordingModel = item.Tag?.ToString() ?? RewordingModels.Gpt4oMini;
            _settings.Save();
        }
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (LanguageComboBox.SelectedItem is ComboBoxItem item)
        {
            _settings.TranscriptionLanguage = item.Tag?.ToString() ?? "detect";
            _settings.Save();
        }
    }

    private void OnHotkeyBoxFocus(object sender, RoutedEventArgs e)
    {
        HotkeyBox.Text = "Press a key combination...";
    }

    private void OnHotkeyBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (HotkeyBox.Text == "Press a key combination...")
        {
            HotkeyBox.Text = _settings.ActivationHotkey;
        }
    }

    private void OnHotkeyBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;

        var modifiers = new List<string>();

        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers.Add("Ctrl");
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers.Add("Alt");
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers.Add("Shift");
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) ||
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers.Add("Win");

        var key = e.Key.ToString();

        // Ignore modifier-only presses
        if (e.Key == VirtualKey.Control || e.Key == VirtualKey.Menu ||
            e.Key == VirtualKey.Shift || e.Key == VirtualKey.LeftWindows ||
            e.Key == VirtualKey.RightWindows)
        {
            return;
        }

        if (modifiers.Count > 0)
        {
            modifiers.Add(key);
            var hotkey = string.Join("+", modifiers);
            HotkeyBox.Text = hotkey;
            _settings.ActivationHotkey = hotkey;
            _settings.Save();

            // Move focus away
            FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
        }
    }

    private void OnCopilotKeyToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.UseCopilotKey = CopilotKeyToggle.IsOn;
        _settings.Save();
    }

    private void OnInstantModeToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.InstantMode = InstantModeToggle.IsOn;
        AnimationSpeedPanel.Visibility = InstantModeToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
        _settings.Save();
    }

    private void OnAutoEnterToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.AutoEnter = AutoEnterToggle.IsOn;
        _settings.Save();
    }

    private void OnAutoFormattingToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.AutoFormattingEnabled = AutoFormattingToggle.IsOn;
        _settings.Save();
    }

    private void OnAnimationSpeedChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading) return;

        _settings.AnimationSpeed = (int)AnimationSpeedSlider.Value;
        _settings.Save();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        var theme = ThemeComboBox.SelectedIndex switch
        {
            1 => "light",
            2 => "dark",
            _ => "system"
        };

        _settings.Theme = theme;
        _settings.Save();

        // TODO: Apply theme immediately
    }

    private void OnOrbFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (OrbFontComboBox.SelectedItem is ComboBoxItem item)
        {
            _settings.OrbFont = item.Tag?.ToString() ?? SettingsDefaults.OrbFont;
            _settings.Save();
        }
    }

    private void OnStartWithWindowsToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.StartWithWindows = StartWithWindowsToggle.IsOn;
        _settings.Save();

        // TODO: Update startup registry
    }

    private void OnManagePromptsClick(object sender, RoutedEventArgs e)
    {
        App.Current.ShowPromptsManager();
    }

    private void OnManageTargetAppsClick(object sender, RoutedEventArgs e)
    {
        App.Current.ShowTargetAppsManager();
    }

    private void OnViewUsageClick(object sender, RoutedEventArgs e)
    {
        App.Current.ShowUsageDashboard();
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "DictateForWindows");
        if (Directory.Exists(tempPath))
        {
            try
            {
                foreach (var file in Directory.GetFiles(tempPath))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }
}
