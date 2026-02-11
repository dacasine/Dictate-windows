using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Audio;
using DictateForWindows.Core.Services.Ocr;
using DictateForWindows.Core.Services.Rewording;
using DictateForWindows.Core.Services.ScreenCapture;
using DictateForWindows.Core.Services.Settings;
using DictateForWindows.Core.Services.TargetApp;
using DictateForWindows.Core.Services.TextInjection;
using DictateForWindows.Core.Services.Transcription;
using Microsoft.UI.Dispatching;
using Windows.UI;

namespace DictateForWindows.ViewModels;

/// <summary>
/// Phases of the orb lifecycle.
/// </summary>
public enum OrbPhase
{
    Hidden,
    Appearing,
    Recording,
    Paused,
    PromptSelection,
    ContextView,
    ModelSelection,
    TargetAppSelection,
    Processing,
    Confirming,
    Cancelling
}

/// <summary>
/// ViewModel for the Orb dictation UI.
/// Adapts the DictatePopupViewModel logic with directional interactions and phase-based state.
/// </summary>
public partial class OrbViewModel : ObservableObject
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "dictate.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [Orb] {message}\n");
        }
        catch { }
    }

    private readonly ISettingsService _settings;
    private readonly IAudioRecordingService _recordingService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IRewordingService _rewordingService;
    private readonly ITextInjector _textInjector;
    private readonly IPromptsRepository _promptsRepository;
    private readonly IUsageRepository _usageRepository;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IOcrService _ocrService;
    private readonly ITargetAppService _targetAppService;
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly List<int> _queuedPromptIds = new();
    private CancellationTokenSource? _cancellationTokenSource;

    #region Observable Properties

    [ObservableProperty]
    private OrbPhase _currentPhase = OrbPhase.Hidden;

    [ObservableProperty]
    private double _audioLevel;

    [ObservableProperty]
    private string _timerText = "00:00";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingText = "Processing...";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _transcriptionResult = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _showPromptArc;

    [ObservableProperty]
    private bool _showContextPanel;

    [ObservableProperty]
    private string _selectedContext = string.Empty;

    [ObservableProperty]
    private PromptItemViewModel? _activeFilter;

    [ObservableProperty]
    private Color _orbAccentColor = Color.FromArgb(255, 0, 120, 212);

    // Context cards
    [ObservableProperty]
    private ContextSource _clipboardContext = ContextSource.Empty(ContextSourceType.Clipboard);

    [ObservableProperty]
    private ContextSource _screenshotContext = ContextSource.Empty(ContextSourceType.Screenshot);

    [ObservableProperty]
    private bool _showContextCards;

    [ObservableProperty]
    private int _activeContextCardIndex;

    // Model arc
    [ObservableProperty]
    private bool _showModelArc;

    [ObservableProperty]
    private ModelItemViewModel? _selectedModel;

    [ObservableProperty]
    private int _activeModelIndex;

    [ObservableProperty]
    private double _modelArcOpacity = 0.35;

    // Target apps
    [ObservableProperty]
    private bool _showTargetApps;

    [ObservableProperty]
    private Core.Models.TargetApp? _selectedTargetApp;

    #endregion

    public ObservableCollection<PromptItemViewModel> Prompts { get; } = new();
    public ObservableCollection<ModelItemViewModel> AvailableModels { get; } = new();
    public ObservableCollection<Core.Models.TargetApp> TargetApps { get; } = new();

    /// <summary>
    /// Raised when the orb window should be hidden (before text injection).
    /// </summary>
    public event EventHandler? RequestHide;

    /// <summary>
    /// Raised when the orb dismiss animation is complete and window should close.
    /// </summary>
    public event EventHandler? RequestClose;

    /// <summary>
    /// Raised when an implosion (confirm) animation should play.
    /// </summary>
    public event EventHandler? RequestImplosion;

    /// <summary>
    /// Raised when a dissolve (cancel) animation should play.
    /// </summary>
    public event EventHandler? RequestDissolve;

    /// <summary>
    /// Raised when a screenshot capture is needed (orb must hide first).
    /// </summary>
    public event EventHandler? RequestScreenshotCapture;

    public bool InstantRecording => _settings.GetBool("dictate_instant_recording", false);

    public OrbViewModel(
        ISettingsService settings,
        IAudioRecordingService recordingService,
        ITranscriptionService transcriptionService,
        IRewordingService rewordingService,
        ITextInjector textInjector,
        IPromptsRepository promptsRepository,
        IUsageRepository usageRepository,
        IScreenCaptureService screenCaptureService,
        IOcrService ocrService,
        ITargetAppService targetAppService)
    {
        _settings = settings;
        _recordingService = recordingService;
        _transcriptionService = transcriptionService;
        _rewordingService = rewordingService;
        _textInjector = textInjector;
        _promptsRepository = promptsRepository;
        _usageRepository = usageRepository;
        _screenCaptureService = screenCaptureService;
        _ocrService = ocrService;
        _targetAppService = targetAppService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _recordingService.StateChanged += OnRecordingStateChanged;
        _recordingService.Progress += OnRecordingProgress;

        LoadPrompts();
        LoadModels();
        LoadTargetApps();
    }

    #region Prompts

    private void LoadPrompts()
    {
        Prompts.Clear();
        var prompts = _promptsRepository.GetAllForKeyboard();
        foreach (var prompt in prompts)
        {
            // Filter out special buttons for the arc (Add, SelectAll, Instant)
            if (prompt.Id < 0) continue;

            var vm = new PromptItemViewModel(prompt, OnPromptClicked);
            Prompts.Add(vm);
        }
    }

    private void OnPromptClicked(PromptModel prompt)
    {
        if (IsRecording || IsPaused)
        {
            ToggleQueuedPrompt(prompt.Id);
        }
        else if (!string.IsNullOrEmpty(TranscriptionResult))
        {
            _ = ApplyPromptAsync(prompt);
        }
    }

    private void ToggleQueuedPrompt(int promptId)
    {
        if (_queuedPromptIds.Contains(promptId))
        {
            _queuedPromptIds.Remove(promptId);
        }
        else
        {
            _queuedPromptIds.Add(promptId);
        }
        UpdateQueuedPromptIndicators();
    }

    private void UpdateQueuedPromptIndicators()
    {
        foreach (var prompt in Prompts)
        {
            var queueIndex = _queuedPromptIds.IndexOf(prompt.Id);
            prompt.QueuePosition = queueIndex >= 0 ? queueIndex + 1 : 0;
        }
    }

    #endregion

    #region Models

    private void LoadModels()
    {
        AvailableModels.Clear();

        // Add rewording models based on provider
        var provider = _settings.ApiProvider;
        var models = provider switch
        {
            ApiProvider.OpenAI => new[] { "gpt-4o", "gpt-4o-mini", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", "o4-mini", "o3-mini" },
            ApiProvider.Groq => new[] { "llama-3.3-70b-versatile", "llama-3.1-8b-instant" },
            _ => new[] { _settings.RewordingModel }
        };

        foreach (var model in models)
        {
            var vm = new ModelItemViewModel(model);
            if (model == _settings.RewordingModel)
            {
                vm.IsSelected = true;
                SelectedModel = vm;
            }
            AvailableModels.Add(vm);
        }
    }

    #endregion

    #region Target Apps

    private void LoadTargetApps()
    {
        TargetApps.Clear();
        var apps = _targetAppService.GetAll();
        foreach (var app in apps)
        {
            TargetApps.Add(app);
        }
    }

    #endregion

    #region Directional Commands

    [ObservableProperty]
    private int _activeTargetAppIndex;

    [RelayCommand]
    public void NavigateUp()
    {
        if (CurrentPhase == OrbPhase.ContextView)
        {
            // Return to center from context view
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection && TargetApps.Count > 0)
        {
            // Cycle up within target apps
            ActiveTargetAppIndex = (ActiveTargetAppIndex - 1 + TargetApps.Count) % TargetApps.Count;
        }
        else if (CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused)
        {
            // Up = show prompt arc
            ShowPromptArc = true;
            ShowContextCards = false;
            ShowTargetApps = false;
            ShowContextPanel = false;
            CurrentPhase = OrbPhase.PromptSelection;
        }
    }

    [RelayCommand]
    public void NavigateDown()
    {
        if (CurrentPhase == OrbPhase.PromptSelection)
        {
            // Return to center from prompt selection
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection && TargetApps.Count > 0)
        {
            // Cycle down within target apps
            ActiveTargetAppIndex = (ActiveTargetAppIndex + 1) % TargetApps.Count;
        }
        else if (CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused)
        {
            // Down = show context cards
            ShowContextCards = true;
            ShowTargetApps = false;
            ShowPromptArc = false;
            ShowContextPanel = true;
            CurrentPhase = OrbPhase.ContextView;
        }
    }

    [RelayCommand]
    public void NavigateLeft()
    {
        if (CurrentPhase == OrbPhase.ContextView || CurrentPhase == OrbPhase.TargetAppSelection ||
            CurrentPhase == OrbPhase.PromptSelection)
        {
            // Return to center from any overlay
            DismissOverlays();
        }
    }

    [RelayCommand]
    public void NavigateRight()
    {
        if (CurrentPhase == OrbPhase.PromptSelection || CurrentPhase == OrbPhase.ContextView)
        {
            // First return to center from any overlay
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused)
        {
            // Right = show target apps
            ShowTargetApps = true;
            ShowContextCards = false;
            ShowPromptArc = false;
            ShowContextPanel = false;
            ActiveTargetAppIndex = 0;
            CurrentPhase = OrbPhase.TargetAppSelection;
        }
    }

    [RelayCommand]
    public void Confirm()
    {
        if (CurrentPhase == OrbPhase.TargetAppSelection)
        {
            // Confirm target app selection via keyboard
            if (ActiveTargetAppIndex >= 0 && ActiveTargetAppIndex < TargetApps.Count)
            {
                SelectTargetApp(TargetApps[ActiveTargetAppIndex]);
            }
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.ContextView)
        {
            // Toggle active context
            ToggleActiveContext();
            DismissOverlays();
        }
        else if (IsRecording || IsPaused)
        {
            CurrentPhase = OrbPhase.Confirming;
            RequestImplosion?.Invoke(this, EventArgs.Empty);
            StopRecording();
        }
        else if (CurrentPhase == OrbPhase.PromptSelection)
        {
            DismissPrompts();
        }
    }

    private void DismissOverlays()
    {
        ShowContextCards = false;
        ShowTargetApps = false;
        ShowPromptArc = false;
        ShowContextPanel = false;
        CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
    }

    [RelayCommand]
    public void SelectTargetApp(Core.Models.TargetApp? app)
    {
        if (app == null)
        {
            SelectedTargetApp = null;
            return;
        }

        SelectedTargetApp = app;
        OrbAccentColor = Color.FromArgb(255, 0, 150, 200); // Cyan tint for target app
    }

    [RelayCommand]
    public void SelectPrompt(PromptItemViewModel? prompt)
    {
        if (prompt == null) return;

        ActiveFilter = prompt;

        // Queue the selected prompt
        if (!_queuedPromptIds.Contains(prompt.Id))
        {
            _queuedPromptIds.Add(prompt.Id);
            UpdateQueuedPromptIndicators();
        }

        // Update orb color to reflect the filter
        OrbAccentColor = Color.FromArgb(255, 0, 180, 100);

        // Return to recording with filter active
        ShowPromptArc = false;
        CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
    }

    [RelayCommand]
    public void DismissPrompts()
    {
        ShowPromptArc = false;
        ActiveFilter = null;
        CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
    }

    [RelayCommand]
    public void DismissContext()
    {
        ShowContextPanel = false;
        ShowContextCards = false;
        CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
    }

    [RelayCommand]
    public void ToggleContextCard(ContextSource? source)
    {
        if (source == null) return;
        source.IsActive = !source.IsActive;
        // Trigger property changed notifications
        OnPropertyChanged(nameof(ClipboardContext));
        OnPropertyChanged(nameof(ScreenshotContext));
    }

    #endregion

    #region Context

    /// <summary>
    /// Set the captured selection context from the target window.
    /// </summary>
    public void SetSelectedContext(string? text)
    {
        SelectedContext = text ?? string.Empty;

        // Update clipboard context card
        ClipboardContext = new ContextSource
        {
            Type = ContextSourceType.Clipboard,
            Text = text ?? string.Empty,
            IsActive = !string.IsNullOrWhiteSpace(text)
        };

        // If no clipboard content and auto-screenshot is enabled, trigger screenshot
        if (string.IsNullOrWhiteSpace(text) &&
            _settings.GetBool(Core.Constants.SettingsKeys.AutoScreenshotOnNoClipboard, true))
        {
            RequestScreenshotCapture?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Capture screenshot and OCR. Called by OrbWindow after hiding the orb.
    /// </summary>
    public async Task CaptureScreenshotAsync()
    {
        ScreenshotContext = new ContextSource
        {
            Type = ContextSourceType.Screenshot,
            IsLoading = true
        };

        var captureResult = await _screenCaptureService.CaptureFullScreenAsync();
        if (!captureResult.IsSuccess)
        {
            ScreenshotContext = new ContextSource
            {
                Type = ContextSourceType.Screenshot,
                IsActive = false
            };
            return;
        }

        ScreenshotContext = new ContextSource
        {
            Type = ContextSourceType.Screenshot,
            ThumbnailPath = captureResult.ImagePath,
            IsLoading = true
        };

        // Run OCR
        var ocrResult = await _ocrService.ExtractTextAsync(captureResult.ImagePath!);
        ScreenshotContext = new ContextSource
        {
            Type = ContextSourceType.Screenshot,
            Text = ocrResult.IsSuccess ? ocrResult.Text : string.Empty,
            ThumbnailPath = captureResult.ImagePath,
            IsActive = ocrResult.IsSuccess,
            IsLoading = false
        };
    }

    private void ToggleActiveContext()
    {
        if (ActiveContextCardIndex == 0)
        {
            ClipboardContext.IsActive = !ClipboardContext.IsActive;
            OnPropertyChanged(nameof(ClipboardContext));
        }
        else
        {
            ScreenshotContext.IsActive = !ScreenshotContext.IsActive;
            OnPropertyChanged(nameof(ScreenshotContext));
        }
    }

    /// <summary>
    /// Build combined context string for rewording prompts.
    /// Provides the LLM with structured context about what the user sees/has copied.
    /// </summary>
    public string BuildContextForRewording()
    {
        var parts = new List<string>();

        if (ClipboardContext.IsActive && ClipboardContext.HasContent)
        {
            parts.Add($"The user has the following text selected/copied:\n\"\"\"\n{ClipboardContext.Text}\n\"\"\"");
        }

        if (ScreenshotContext.IsActive && ScreenshotContext.HasContent)
        {
            parts.Add($"For additional context, here is the OCR transcription of what currently appears on the user's screen:\n\"\"\"\n{ScreenshotContext.Text}\n\"\"\"");
        }

        if (parts.Count == 0) return string.Empty;

        return "--- USER CONTEXT ---\n" + string.Join("\n\n", parts) + "\n--- END CONTEXT ---\n\nUse the context above to better understand the user's intent and produce a more relevant response.";
    }

    #endregion

    #region Recording

    public async void StartRecording()
    {
        Log("StartRecording called");
        if (IsRecording) return;

        CurrentPhase = OrbPhase.Appearing;

        _queuedPromptIds.Clear();
        UpdateQueuedPromptIndicators();

        // Add auto-apply prompts to queue
        var autoApplyIds = _promptsRepository.GetAutoApplyIds();
        _queuedPromptIds.AddRange(autoApplyIds);

        _cancellationTokenSource = new CancellationTokenSource();

        StatusText = "Starting...";
        try
        {
            var success = await _recordingService.StartRecordingAsync();
            if (!success)
            {
                StatusText = "Failed to start recording";
                Log("Recording failed to start");
            }
        }
        catch (Exception ex)
        {
            Log($"Exception in StartRecording: {ex}");
            StatusText = $"Error: {ex.Message}";
        }
    }

    public async void StopRecording()
    {
        Log("StopRecording called");
        if (!IsRecording && !IsPaused) return;

        StatusText = "Stopping...";
        var audioPath = await _recordingService.StopRecordingAsync();

        if (audioPath != null)
        {
            await TranscribeAsync(audioPath);
        }
    }

    [RelayCommand]
    public void ToggleRecording()
    {
        if (IsRecording || IsPaused)
        {
            Confirm();
        }
        else
        {
            StartRecording();
        }
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (IsPaused)
        {
            _recordingService.ResumeRecording();
        }
        else if (IsRecording)
        {
            _recordingService.PauseRecording();
        }
    }

    public void CancelRecording()
    {
        _cancellationTokenSource?.Cancel();
        _recordingService.CancelRecording();
        _queuedPromptIds.Clear();
        UpdateQueuedPromptIndicators();
        ResetState();
    }

    public void CancelWithDissolve()
    {
        CurrentPhase = OrbPhase.Cancelling;
        RequestDissolve?.Invoke(this, EventArgs.Empty);
        CancelRecording();
    }

    #endregion

    #region Transcription & Rewording

    private async Task TranscribeAsync(string audioPath)
    {
        Log($"TranscribeAsync starting with path: {audioPath}");
        IsProcessing = true;
        CurrentPhase = OrbPhase.Processing;
        ProcessingText = "Transcribing...";
        StatusText = "Sending to API...";

        try
        {
            var result = await _transcriptionService.TranscribeAsync(audioPath, _cancellationTokenSource?.Token ?? default);
            Log($"TranscribeAsync result: IsSuccess={result.IsSuccess}");

            if (result.IsSuccess)
            {
                var model = _settings.TranscriptionModel;
                var provider = _settings.ApiProvider;
                _usageRepository.RecordUsage(model, result.DurationMs, 0, 0, provider);

                var text = result.Text;

                if (_settings.AutoFormattingEnabled)
                {
                    ProcessingText = "Formatting...";
                    var formatResult = await _rewordingService.ApplyAutoFormattingAsync(text, _cancellationTokenSource?.Token ?? default);
                    if (formatResult.IsSuccess)
                    {
                        text = formatResult.Text;
                    }
                }

                if (_queuedPromptIds.Count > 0)
                {
                    text = await ProcessQueuedPromptsAsync(text);
                }

                TranscriptionResult = text;
                HasResult = true;
                StatusText = "Done";

                // Route to target app or inject via clipboard
                if (SelectedTargetApp != null)
                {
                    Log($"Sending to target app: {SelectedTargetApp.Name}");
                    await _targetAppService.SendToAppAsync(SelectedTargetApp, text);
                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Log("Requesting hide before injection...");
                    RequestHide?.Invoke(this, EventArgs.Empty);
                    await Task.Delay(100);

                    await InjectTextAsync(text);

                    // Close the orb after injection
                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                HandleTranscriptionError(result);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _queuedPromptIds.Clear();
        }
    }

    private async Task<string> ProcessQueuedPromptsAsync(string text)
    {
        var currentText = text;
        var context = BuildContextForRewording();

        foreach (var promptId in _queuedPromptIds)
        {
            var prompt = _promptsRepository.Get(promptId);
            if (prompt == null) continue;
            if (prompt.RequiresSelection && string.IsNullOrWhiteSpace(currentText)) continue;

            ProcessingText = $"Applying: {prompt.Name}";
            StatusText = $"Running prompt: {prompt.Name}";

            var promptText = prompt.Prompt;
            if (!string.IsNullOrEmpty(context))
            {
                promptText = $"{promptText}\n\n{context}";
            }

            var result = await _rewordingService.RewordAsync(
                currentText,
                promptText,
                RewordingService.DefaultSystemPrompt,
                _cancellationTokenSource?.Token ?? default);

            if (result.IsSuccess)
            {
                currentText = result.Text;
                var model = _settings.RewordingModel;
                var provider = _settings.ApiProvider;
                _usageRepository.RecordUsage(model, 0, result.InputTokens, result.OutputTokens, provider);
            }
        }
        return currentText;
    }

    private async Task ApplyPromptAsync(PromptModel prompt)
    {
        IsProcessing = true;
        CurrentPhase = OrbPhase.Processing;
        ProcessingText = $"Applying: {prompt.Name}";

        try
        {
            var context = BuildContextForRewording();
            var promptText = prompt.Prompt;
            if (!string.IsNullOrEmpty(context))
            {
                promptText = $"{promptText}\n\n{context}";
            }

            var result = await _rewordingService.RewordAsync(
                TranscriptionResult,
                promptText,
                RewordingService.DefaultSystemPrompt,
                _cancellationTokenSource?.Token ?? default);

            if (result.IsSuccess)
            {
                TranscriptionResult = result.Text;
                var model = _settings.RewordingModel;
                var provider = _settings.ApiProvider;
                _usageRepository.RecordUsage(model, 0, result.InputTokens, result.OutputTokens, provider);

                await InjectTextAsync(result.Text);
            }
            else
            {
                StatusText = $"Error: {result.Error}";
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task InjectTextAsync(string text)
    {
        Log($"InjectTextAsync called");
        var options = new TextInjectionOptions
        {
            AnimateTyping = !_settings.InstantMode,
            AnimationSpeedMs = _settings.AnimationSpeed,
            AutoEnter = _settings.AutoEnter
        };
        await _textInjector.InjectTextAsync(text, options);
    }

    private void HandleTranscriptionError(TranscriptionResult result)
    {
        StatusText = result.ErrorType switch
        {
            TranscriptionError.InvalidApiKey => "Invalid API key",
            TranscriptionError.QuotaExceeded => "Quota exceeded",
            TranscriptionError.ContentSizeLimit => "Audio too long",
            TranscriptionError.FormatNotSupported => "Format not supported",
            TranscriptionError.Timeout => "Request timed out",
            TranscriptionError.NetworkError => "Network error",
            _ => $"Error: {result.Error}"
        };
    }

    #endregion

    #region Recording Events

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsRecording = e.NewState == RecordingState.Recording || e.NewState == RecordingState.Paused;
            IsPaused = e.NewState == RecordingState.Paused;

            if (e.NewState == RecordingState.Recording)
            {
                CurrentPhase = OrbPhase.Recording;
            }
            else if (e.NewState == RecordingState.Paused)
            {
                CurrentPhase = OrbPhase.Paused;
            }

            StatusText = e.NewState switch
            {
                RecordingState.Idle => "Ready",
                RecordingState.Recording => "Recording...",
                RecordingState.Paused => "Paused",
                RecordingState.Processing => "Processing...",
                RecordingState.Error => e.ErrorMessage ?? "Error",
                _ => StatusText
            };
        });
    }

    private void OnRecordingProgress(object? sender, RecordingProgressEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            TimerText = $"{e.Duration:mm\\:ss}";
            AudioLevel = e.AudioLevel;
        });
    }

    #endregion

    private void ResetState()
    {
        CurrentPhase = OrbPhase.Hidden;
        StatusText = "Ready";
        TimerText = "00:00";
        AudioLevel = 0;
        IsRecording = false;
        IsPaused = false;
        IsProcessing = false;
        ShowPromptArc = false;
        ShowContextPanel = false;
        ShowContextCards = false;
        ShowTargetApps = false;
        ActiveFilter = null;
        SelectedTargetApp = null;
        TranscriptionResult = string.Empty;
        HasResult = false;
        OrbAccentColor = Color.FromArgb(255, 0, 120, 212);
        ClipboardContext = ContextSource.Empty(ContextSourceType.Clipboard);
        ScreenshotContext = ContextSource.Empty(ContextSourceType.Screenshot);
    }
}
