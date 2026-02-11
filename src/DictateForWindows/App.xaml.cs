using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Services.Activation;
using DictateForWindows.Core.Services.Audio;
using DictateForWindows.Core.Services.Ocr;
using DictateForWindows.Core.Services.Rewording;
using DictateForWindows.Core.Services.ScreenCapture;
using DictateForWindows.Core.Services.Settings;
using DictateForWindows.Core.Services.TargetApp;
using DictateForWindows.Core.Services.TextInjection;
using DictateForWindows.Core.Services.Transcription;
using DictateForWindows.ViewModels;
using DictateForWindows.Views;

namespace DictateForWindows;

/// <summary>
/// Main application class for Dictate for Windows.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;
    private MainWindow? _mainWindow;
    private OrbWindow? _orb;
    private int _activationHotkeyId = -1;
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "startup.log");

    public static App Current => (App)Application.Current;

    public IServiceProvider Services => _host.Services;

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    public App()
    {
        Log("App constructor starting...");
        try
        {
            Log("Calling InitializeComponent...");
            InitializeComponent();
            Log("InitializeComponent done.");

            Log("Creating Host...");
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
            Log("Host created successfully.");
        }
        catch (Exception ex)
        {
            Log($"Exception in App constructor: {ex}");
            throw;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Settings
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        // Data repositories
        services.AddSingleton<IPromptsRepository, PromptsRepository>();
        services.AddSingleton<IUsageRepository, UsageRepository>();

        // Audio services
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IAudioRecordingService, AudioRecordingService>();

        // API services
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        services.AddSingleton<IRewordingService, RewordingService>();

        // Text injection
        services.AddSingleton<ITextInjector, ClipboardInjector>();
        services.AddSingleton<IUndoRedoManager, UndoRedoManager>();

        // Screen capture & OCR
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IOcrService, WindowsOcrService>();

        // Target apps
        services.AddSingleton<ITargetAppService, TargetAppService>();

        // Activation
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<ICopilotKeyService, CopilotKeyService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DictatePopupViewModel>();
        services.AddTransient<OrbViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PromptsViewModel>();
        services.AddTransient<UsageViewModel>();

        // HTTP client factory
        services.AddHttpClient();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("OnLaunched starting...");
        try
        {
            // Create the main window (hidden, hosts the app)
            Log("Creating MainWindow...");
            _mainWindow = new MainWindow();
            Log("MainWindow created.");

            Log("Getting settings service...");
            var settings = Services.GetRequiredService<ISettingsService>();
            Log($"Settings loaded. FirstRun={settings.FirstRun}");

            // Check for first run
            if (settings.FirstRun)
            {
                Log("Calling ShowOnboarding...");
                ShowOnboarding();
                Log("ShowOnboarding returned.");
            }
            else
            {
                Log("Calling InitializeHotkey...");
                InitializeHotkey();
                Log("Calling MinimizeToTray...");
                MinimizeToTray();
            }
            Log("OnLaunched completed successfully.");
        }
        catch (Exception ex)
        {
            Log($"Exception in OnLaunched: {ex}");
            throw;
        }
    }

    private void InitializeHotkey()
    {
        var hotkeyService = Services.GetRequiredService<IHotkeyService>();
        var settings = Services.GetRequiredService<ISettingsService>();

        hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Register the activation hotkey
        var hotkeyString = settings.ActivationHotkey;
        _activationHotkeyId = hotkeyService.RegisterHotkey(hotkeyString, "Activate Dictate");

        if (_activationHotkeyId == -1)
        {
            // Hotkey registration failed, show error
            ShowNotification("Hotkey Error", $"Failed to register hotkey: {hotkeyString}");
        }

        // Initialize Copilot key if enabled
        if (settings.UseCopilotKey)
        {
            var copilotService = Services.GetRequiredService<ICopilotKeyService>();
            copilotService.CopilotKeyPressed += OnCopilotKeyPressed;
            copilotService.Enable();
        }
    }

    private void OnCopilotKeyPressed(object? sender, EventArgs e)
    {
        ToggleOrConfirmOrb();
    }

    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        ToggleOrConfirmOrb();
    }

    private void ToggleOrConfirmOrb()
    {
        if (_orb == null || !_orb.IsVisible)
        {
            ShowPopup();
        }
        else
        {
            // Orb is visible: confirm (stop recording + transcribe) instead of cancel
            _orb.ViewModel.Confirm();
        }
    }

    public async void ShowPopup()
    {
        if (_orb == null)
        {
            _orb = new OrbWindow();
            _orb.Closed += (s, e) => _orb = null;
        }

        await _orb.ShowAtCursorAsync();
    }

    public void HidePopup()
    {
        _orb?.Dismiss();
    }

    private void ShowOnboarding()
    {
        Log("ShowOnboarding: Creating OnboardingWindow...");
        var onboardingWindow = new OnboardingWindow();
        onboardingWindow.Activate();
        Log("ShowOnboarding: Window activated.");

        onboardingWindow.Closed += (s, e) =>
        {
            Log("OnboardingWindow.Closed event fired.");
            try
            {
                Log("Initializing hotkey after onboarding...");
                InitializeHotkey();
                Log("Hotkey initialized.");

                Log("Minimizing to tray...");
                MinimizeToTray();
                Log("Minimized to tray. App should be running in background now.");
            }
            catch (Exception ex)
            {
                Log($"Exception in OnboardingWindow.Closed handler: {ex}");
            }
        };
    }

    public void ShowSettings()
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Activate();
    }

    public void ShowPromptsManager()
    {
        var promptsWindow = new PromptsWindow();
        promptsWindow.Activate();
    }

    public void ShowUsageDashboard()
    {
        var usageWindow = new UsageWindow();
        usageWindow.Activate();
    }

    public void ShowTargetAppsManager()
    {
        var targetAppsWindow = new TargetAppsWindow();
        targetAppsWindow.Activate();
    }

    private void MinimizeToTray()
    {
        // Keep the app running in background
        // The main window is hidden
        _mainWindow?.Hide();
    }

    public void ShowNotification(string title, string message)
    {
        // TODO: Implement Windows notification
    }

    public void Exit()
    {
        var hotkeyService = Services.GetRequiredService<IHotkeyService>();
        hotkeyService.UnregisterAll();

        var settings = Services.GetRequiredService<ISettingsService>();
        settings.Save();

        _orb?.Close();
        _mainWindow?.Close();

        Environment.Exit(0);
    }
}
