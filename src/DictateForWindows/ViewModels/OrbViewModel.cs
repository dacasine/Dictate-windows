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
using DictateForWindows.Core.Services;
using DictateForWindows.Core.Services.Prosody;
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
    Cancelling,
    Branching
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
    private readonly IProsodyAnalyzer _prosodyAnalyzer;
    private readonly ProsodyFormatter _prosodyFormatter = new();
    private readonly HesitationAnalyzer _hesitationAnalyzer = new();
    private readonly EmotionAnalyzer _emotionAnalyzer = new();
    private readonly ContextualFormatter _contextualFormatter = new();
    private readonly DispatcherQueue _dispatcherQueue;

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
    private bool _showPromptsGrid;

    [ObservableProperty]
    private bool _showDirectionalHints;

    [ObservableProperty]
    private bool _showContextPanel;

    [ObservableProperty]
    private string _selectedContext = string.Empty;

    [ObservableProperty]
    private string _activePromptName = "Default";

    [ObservableProperty]
    private int _activePromptIndex;

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

    // Voice Branching
    [ObservableProperty]
    private bool _showBranchComparison;

    [ObservableProperty]
    private int _activeBranchIndex;

    [ObservableProperty]
    private string _branchStatusText = string.Empty;

    #endregion

    public ObservableCollection<PromptItemViewModel> Prompts { get; } = new();
    public ObservableCollection<ModelItemViewModel> AvailableModels { get; } = new();
    public ObservableCollection<Core.Models.TargetApp> TargetApps { get; } = new();
    public ObservableCollection<VoiceBranch> Branches { get; } = new();

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

    /// <summary>
    /// Raised when the user wants to open settings (Left arrow during recording).
    /// </summary>
    public event EventHandler? RequestOpenSettings;

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
        ITargetAppService targetAppService,
        IProsodyAnalyzer prosodyAnalyzer)
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
        _prosodyAnalyzer = prosodyAnalyzer;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _recordingService.StateChanged += OnRecordingStateChanged;
        _recordingService.Progress += OnRecordingProgress;

        LoadPrompts();
        LoadModels();
        LoadTargetApps();
    }

    partial void OnCurrentPhaseChanged(OrbPhase value)
    {
        ShowDirectionalHints = value == OrbPhase.Recording || value == OrbPhase.Paused;
    }

    #region Prompts

    private void LoadPrompts()
    {
        Prompts.Clear();

        // Insert Default prompt at position 0
        var defaultPrompt = new PromptModel
        {
            Id = SpecialPromptIds.Default,
            Name = "Default",
            Prompt = string.Empty,
            Position = -100
        };
        Prompts.Add(new PromptItemViewModel(defaultPrompt, OnPromptClicked));

        var prompts = _promptsRepository.GetAllForKeyboard();
        foreach (var prompt in prompts)
        {
            // Filter out special buttons (Add, SelectAll, Instant)
            if (prompt.Id < 0) continue;

            var vm = new PromptItemViewModel(prompt, OnPromptClicked);
            Prompts.Add(vm);
        }

        // Restore persisted active prompt
        var savedPromptId = _settings.GetInt(Core.Constants.SettingsKeys.ActivePromptId, Core.Constants.SettingsDefaults.ActivePromptId);
        var savedPrompt = Prompts.FirstOrDefault(p => p.Id == savedPromptId) ?? Prompts[0];
        SetActivePrompt(savedPrompt, persist: false);
    }

    private void OnPromptClicked(PromptModel prompt)
    {
        if (IsRecording || IsPaused)
        {
            var vm = Prompts.FirstOrDefault(p => p.Id == prompt.Id);
            if (vm != null) SetActivePrompt(vm);
        }
        else if (!string.IsNullOrEmpty(TranscriptionResult))
        {
            _ = ApplyPromptAsync(prompt);
        }
    }

    private void SetActivePrompt(PromptItemViewModel prompt, bool persist = true)
    {
        // Deactivate all
        foreach (var p in Prompts)
            p.IsActive = false;

        // Activate selected
        prompt.IsActive = true;
        ActivePromptName = prompt.Name;
        ActivePromptIndex = Prompts.IndexOf(prompt);

        // Keep default accent — no color change per prompt

        if (persist)
        {
            _settings.SetInt(Core.Constants.SettingsKeys.ActivePromptId, prompt.Id);
            _settings.Save();
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
        if (CurrentPhase == OrbPhase.PromptSelection && Prompts.Count > 0)
        {
            // Move up by 2 (row jump in 2-column prompt grid)
            var newIndex = ActivePromptIndex - 2;
            if (newIndex < 0)
            {
                // Already at top row — wrap or stay
                newIndex = (newIndex + Prompts.Count) % Prompts.Count;
            }
            SetActivePrompt(Prompts[newIndex], persist: false);
        }
        else if (CurrentPhase == OrbPhase.ContextView)
        {
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection && TargetApps.Count > 0)
        {
            ActiveTargetAppIndex = (ActiveTargetAppIndex - 2 + TargetApps.Count) % TargetApps.Count;
        }
        else if ((CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused) && Prompts.Count > 0)
        {
            // Open prompts grid
            ShowPromptsGrid = true;
            CurrentPhase = OrbPhase.PromptSelection;
        }
    }

    [RelayCommand]
    public void NavigateDown()
    {
        if (CurrentPhase == OrbPhase.PromptSelection && Prompts.Count > 0)
        {
            // Move down by 2 in prompt grid
            var newIndex = (ActivePromptIndex + 2) % Prompts.Count;
            SetActivePrompt(Prompts[newIndex], persist: false);
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection && TargetApps.Count > 0)
        {
            ActiveTargetAppIndex = (ActiveTargetAppIndex + 2) % TargetApps.Count;
        }
        else if ((CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused))
        {
            // Down = show context cards
            ShowContextCards = true;
            ShowPromptsGrid = false;
            ShowTargetApps = false;
            ShowContextPanel = false;
            ActiveContextCardIndex = 0;
            CurrentPhase = OrbPhase.ContextView;
        }
    }

    [RelayCommand]
    public void NavigateLeft()
    {
        if (CurrentPhase == OrbPhase.PromptSelection && Prompts.Count > 0)
        {
            // Move left within prompt grid
            if (ActivePromptIndex % 2 == 0)
            {
                // Already on left column — dismiss prompts grid, return to recording
                ShowPromptsGrid = false;
                CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
            }
            else
            {
                SetActivePrompt(Prompts[ActivePromptIndex - 1], persist: false);
            }
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection && TargetApps.Count > 0)
        {
            if (ActiveTargetAppIndex % 2 == 0)
            {
                // On left column — dismiss target apps, return to recording
                DismissOverlays();
            }
            else
            {
                ActiveTargetAppIndex -= 1;
            }
        }
        else if (CurrentPhase == OrbPhase.ContextView)
        {
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused)
        {
            // Left = open settings
            RequestOpenSettings?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void NavigateRight()
    {
        if (CurrentPhase == OrbPhase.PromptSelection && Prompts.Count > 0)
        {
            // Move right within prompt grid
            if (ActivePromptIndex % 2 == 1 || ActivePromptIndex == Prompts.Count - 1)
            {
                // Already on right column or last item — dismiss prompts grid
                ShowPromptsGrid = false;
                CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
            }
            else
            {
                SetActivePrompt(Prompts[ActivePromptIndex + 1], persist: false);
            }
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection && TargetApps.Count > 0)
        {
            if (ActiveTargetAppIndex % 2 == 1 || ActiveTargetAppIndex == TargetApps.Count - 1)
            {
                // On right column or last item — dismiss target apps
                DismissOverlays();
            }
            else
            {
                ActiveTargetAppIndex += 1;
            }
        }
        else if (CurrentPhase == OrbPhase.ContextView)
        {
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.Recording || CurrentPhase == OrbPhase.Paused)
        {
            // Right = show target apps
            ShowTargetApps = true;
            ShowContextCards = false;
            ShowContextPanel = false;
            ShowPromptsGrid = false;
            ActiveTargetAppIndex = 0;
            CurrentPhase = OrbPhase.TargetAppSelection;
        }
    }

    [RelayCommand]
    public void Confirm()
    {
        if (CurrentPhase == OrbPhase.PromptSelection)
        {
            // Confirm prompt selection — persist and return to recording
            if (ActivePromptIndex >= 0 && ActivePromptIndex < Prompts.Count)
            {
                SetActivePrompt(Prompts[ActivePromptIndex]);
            }
            ShowPromptsGrid = false;
            CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection)
        {
            if (ActiveTargetAppIndex >= 0 && ActiveTargetAppIndex < TargetApps.Count)
            {
                SelectedTargetApp = TargetApps[ActiveTargetAppIndex];
                OrbAccentColor = Color.FromArgb(255, 0, 150, 200);
            }
            DismissOverlays();
            if (IsRecording || IsPaused)
            {
                BeginProcessing();
            }
        }
        else if (CurrentPhase == OrbPhase.ContextView)
        {
            ToggleActiveContext();
            DismissOverlays();
        }
        else if (IsRecording || IsPaused)
        {
            BeginProcessing();
        }
    }

    /// <summary>
    /// Transition from recording to processing: hide prompts, show status, stop recording.
    /// The orb stays visible during processing and hides just before text injection.
    /// </summary>
    private void BeginProcessing()
    {
        ShowPromptsGrid = false;
        ShowPromptArc = false;
        ShowTargetApps = false;
        ShowContextCards = false;
        ShowContextPanel = false;
        StatusText = "Transcribing...";
        CurrentPhase = OrbPhase.Processing;
        StopRecording();
    }

    private void DismissOverlays()
    {
        ShowContextCards = false;
        ShowTargetApps = false;
        ShowPromptsGrid = false;
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

        // Clicking a target app while recording → stop + transcribe + send
        if (IsRecording || IsPaused)
        {
            DismissOverlays();
            CurrentPhase = OrbPhase.Confirming;
            RequestImplosion?.Invoke(this, EventArgs.Empty);
            StopRecording();
        }
    }

    [RelayCommand]
    public void SelectPrompt(PromptItemViewModel? prompt)
    {
        if (prompt == null) return;

        SetActivePrompt(prompt);
        ShowPromptsGrid = false;
        CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
    }

    [RelayCommand]
    public void DismissPrompts()
    {
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

        // Update clipboard context card (active by default if has content)
        ClipboardContext = new ContextSource
        {
            Type = ContextSourceType.Clipboard,
            Text = text ?? string.Empty,
            IsActive = !string.IsNullOrWhiteSpace(text)
        };

        // Screenshot is now captured by OrbWindow before showing,
        // so no need to trigger hide/show flicker here.
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
            IsActive = ocrResult.IsSuccess, // Active by default if OCR succeeded
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

        // Reload data that may have changed in settings between sessions
        LoadPrompts();
        LoadModels();
        LoadTargetApps();

        // Reset per-session state (no target app persistence between sessions)
        SelectedTargetApp = null;

        CurrentPhase = OrbPhase.Appearing;

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
        else
        {
            Log("No audio path returned — closing overlay");
            RequestClose?.Invoke(this, EventArgs.Empty);
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
        ResetState();
    }

    public void CancelWithDissolve()
    {
        CurrentPhase = OrbPhase.Cancelling;
        RequestDissolve?.Invoke(this, EventArgs.Empty);
        CancelRecording();
    }

    /// <summary>
    /// Phase-aware Escape handler:
    /// - PromptSelection/TargetAppSelection → dismiss sub-menu, return to recording
    /// - Otherwise → cancel and dissolve the entire overlay
    /// </summary>
    public void HandleEscape()
    {
        if (CurrentPhase == OrbPhase.PromptSelection)
        {
            ShowPromptsGrid = false;
            CurrentPhase = IsRecording ? OrbPhase.Recording : (IsPaused ? OrbPhase.Paused : OrbPhase.Recording);
        }
        else if (CurrentPhase == OrbPhase.TargetAppSelection)
        {
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.ContextView)
        {
            DismissOverlays();
        }
        else if (CurrentPhase == OrbPhase.Branching)
        {
            // Cancel branch comparison — go back to showing last branch text
            ShowBranchComparison = false;
            _isBranching = false;
            if (Branches.Count > 0)
            {
                TranscriptionResult = Branches.Last().Text;
                CurrentPhase = OrbPhase.Processing;
            }
        }
        else
        {
            CancelWithDissolve();
        }
    }

    #endregion

    #region Transcription & Rewording

    private async Task TranscribeAsync(string audioPath)
    {
        Log($"TranscribeAsync starting with path: {audioPath}");
        IsProcessing = true;
        CurrentPhase = OrbPhase.Processing;
        StatusText = "Transcribing...";

        try
        {
            var ct = _cancellationTokenSource?.Token ?? default;

            // Run transcription and prosody analysis in parallel (prosody uses WAV, transcription uses M4A)
            var transcriptionTask = _transcriptionService.TranscribeAsync(audioPath, ct);

            var wavPath = _recordingService.TempWavPath;
            var prosodyEnabled = _settings.GetBool(Core.Constants.SettingsKeys.ProsodyFormatting, false);
            Task<ProsodyResult>? prosodyTask = null;

            if (prosodyEnabled && !string.IsNullOrEmpty(wavPath) && File.Exists(wavPath))
            {
                Log($"Prosody analysis starting on: {wavPath}");
                prosodyTask = _prosodyAnalyzer.AnalyzeAsync(wavPath, ct);
            }

            var result = await transcriptionTask;
            var prosodyResult = prosodyTask != null ? await prosodyTask : null;

            // Clean up WAV file now that both are done
            _recordingService.CleanupTempWav();

            Log($"TranscribeAsync result: IsSuccess={result.IsSuccess}, Prosody={prosodyResult?.IsSuccess}");

            if (result.IsSuccess)
            {
                var model = _settings.TranscriptionModel;
                var provider = _settings.ApiProvider;
                _usageRepository.RecordUsage(model, result.DurationMs, 0, 0, provider);

                var text = result.Text;

                // Apply prosody-based formatting if enabled and analysis succeeded
                if (prosodyResult is { IsSuccess: true } && result.Segments is { Count: > 0 })
                {
                    Log("Applying prosody formatting...");
                    text = _prosodyFormatter.ApplyFormatting(text, result.Segments, prosodyResult);
                    Log($"Prosody formatting applied. Pauses={prosodyResult.Pauses.Count}, BaselinePitch={prosodyResult.BaselinePitchHz:F1}Hz");
                }

                // Hesitation intelligence: analyze fillers, self-corrections, fatigue, topic changes
                var hesitationEnabled = _settings.GetBool(Core.Constants.SettingsKeys.HesitationAnalysis, false);
                if (hesitationEnabled)
                {
                    Log("Running hesitation analysis...");
                    var hesitation = _hesitationAnalyzer.Analyze(text, result.Segments, prosodyResult, result.DetectedLanguage);
                    Log($"Hesitation: fluency={hesitation.OverallFluencyScore:P0}, fillers={hesitation.FillerCount}, corrections={hesitation.SelfCorrectionCount}, fatigue={hesitation.FatigueLevel:P0}");

                    // Append summary footer (non-destructive — user can see analysis inline)
                    if (hesitation.FillerCount > 0 || hesitation.SelfCorrectionCount > 0 || hesitation.FatigueLevel > 0.3f)
                    {
                        text += hesitation.BuildSummaryFooter();
                    }
                }

                // Emotional watermarking: detect emotions from prosody and embed metadata
                var emotionEnabled = _settings.GetBool(Core.Constants.SettingsKeys.EmotionalWatermarking, false);
                EmotionResult? emotionResult = null;
                if (emotionEnabled && prosodyResult is { IsSuccess: true })
                {
                    Log("Running emotion analysis...");
                    emotionResult = _emotionAnalyzer.Analyze(result.Segments, prosodyResult);
                    Log($"Emotion: dominant={emotionResult.DominantEmotion} ({emotionResult.DominantConfidence:P0}), valence={emotionResult.Valence:F2}, arousal={emotionResult.Arousal:F2}");

                    if (emotionResult.ShouldWarn)
                    {
                        Log($"Emotion warning: {emotionResult.WarningMessage}");
                    }

                    // Append emotion summary footer
                    text += emotionResult.BuildSummaryFooter();
                }

                if (_settings.AutoFormattingEnabled)
                {
                    ProcessingText = "Formatting...";
                    var formatResult = await _rewordingService.ApplyAutoFormattingAsync(text, _cancellationTokenSource?.Token ?? default);
                    if (formatResult.IsSuccess)
                    {
                        text = formatResult.Text;
                    }
                }

                // Apply active prompt if one is selected (not Default)
                var activePrompt = Prompts.FirstOrDefault(p => p.IsActive);
                if (activePrompt != null && activePrompt.Id != SpecialPromptIds.Default)
                {
                    var prompt = _promptsRepository.Get(activePrompt.Id);
                    if (prompt != null)
                    {
                        StatusText = "Applying model...";

                        var context = BuildContextForRewording();
                        var promptText = prompt.Prompt;
                        if (!string.IsNullOrEmpty(context))
                        {
                            promptText = $"{promptText}\n\n{context}";
                        }

                        var rewordResult = await _rewordingService.RewordAsync(
                            text,
                            promptText,
                            RewordingService.DefaultSystemPrompt,
                            _cancellationTokenSource?.Token ?? default);

                        if (rewordResult.IsSuccess)
                        {
                            text = rewordResult.Text;
                            _usageRepository.RecordUsage(_settings.RewordingModel, 0,
                                rewordResult.InputTokens, rewordResult.OutputTokens, _settings.ApiProvider);
                        }
                    }
                }

                // Contextual auto-formatting: adjust style based on selected target app
                var contextualEnabled = _settings.GetBool(Core.Constants.SettingsKeys.ContextualFormatting, false);
                if (contextualEnabled && SelectedTargetApp != null)
                {
                    var stylePrompt = _contextualFormatter.GetSystemPromptForApp(SelectedTargetApp);
                    if (stylePrompt != null)
                    {
                        Log($"Contextual formatting for {SelectedTargetApp.Name} (profile={SelectedTargetApp.StyleProfileId})");
                        StatusText = "Styling...";
                        var styleResult = await _rewordingService.RewordAsync(
                            text,
                            text, // prompt = the text itself, system prompt drives the transformation
                            stylePrompt,
                            _cancellationTokenSource?.Token ?? default);

                        if (styleResult.IsSuccess)
                        {
                            text = styleResult.Text;
                            _usageRepository.RecordUsage(_settings.RewordingModel, 0,
                                styleResult.InputTokens, styleResult.OutputTokens, _settings.ApiProvider);
                            Log($"Contextual formatting applied: {text.Length} chars");
                        }
                    }
                }

                TranscriptionResult = text;
                HasResult = true;
                StatusText = "Done";

                // Voice branching: if we're in a branching session, store and show comparison
                if (HandleBranchingResult(text))
                    return;

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
                Log($"Transcription failed: {result.Error} — closing overlay");
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            Log("Transcription cancelled — closing overlay");
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log($"Exception in TranscribeAsync: {ex.Message} — closing overlay");
            StatusText = $"Error: {ex.Message}";
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ApplyPromptAsync(PromptModel prompt)
    {
        IsProcessing = true;
        CurrentPhase = OrbPhase.Processing;
        ProcessingText = "Applying model...";

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

    #region Voice Branching

    private bool _isBranching;

    /// <summary>
    /// Save current transcription as a branch and re-record an alternative.
    /// </summary>
    [RelayCommand]
    public void BranchRecording()
    {
        if (string.IsNullOrEmpty(TranscriptionResult)) return;

        var branchingEnabled = _settings.GetBool(Core.Constants.SettingsKeys.VoiceBranching, false);
        if (!branchingEnabled) return;

        // Save current text as a branch
        Branches.Add(new VoiceBranch
        {
            Index = Branches.Count,
            Text = TranscriptionResult,
            CreatedAt = DateTime.Now
        });

        _isBranching = true;
        ActiveBranchIndex = Branches.Count - 1;
        BranchStatusText = $"Branch {Branches.Last().Label} saved — recording alternative...";
        Log($"Voice branch created: {Branches.Last().Label}");

        // Reset to recording phase
        TranscriptionResult = string.Empty;
        HasResult = false;
        ShowBranchComparison = false;
        StartRecording();
    }

    /// <summary>
    /// Select a branch by index and set it as the active transcription result.
    /// </summary>
    [RelayCommand]
    public void SelectBranch(int index)
    {
        if (index < 0 || index >= Branches.Count) return;

        ActiveBranchIndex = index;
        TranscriptionResult = Branches[index].Text;
        BranchStatusText = $"Selected branch {Branches[index].Label}";
        Log($"Voice branch selected: {Branches[index].Label}");
    }

    /// <summary>
    /// Toggle comparison view showing all branches.
    /// </summary>
    [RelayCommand]
    public void ToggleBranchComparison()
    {
        if (Branches.Count < 2) return;
        ShowBranchComparison = !ShowBranchComparison;
        CurrentPhase = ShowBranchComparison ? OrbPhase.Branching : OrbPhase.Processing;
    }

    /// <summary>
    /// Confirm the currently selected branch and proceed to injection.
    /// </summary>
    [RelayCommand]
    public async Task ConfirmBranch()
    {
        if (Branches.Count == 0) return;

        var selectedText = ActiveBranchIndex >= 0 && ActiveBranchIndex < Branches.Count
            ? Branches[ActiveBranchIndex].Text
            : TranscriptionResult;

        TranscriptionResult = selectedText;
        ShowBranchComparison = false;
        _isBranching = false;
        Log($"Voice branch confirmed: {Branches[ActiveBranchIndex].Label}");

        // Proceed to injection
        if (SelectedTargetApp != null)
        {
            await _targetAppService.SendToAppAsync(SelectedTargetApp, selectedText);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            RequestHide?.Invoke(this, EventArgs.Empty);
            await Task.Delay(100);
            await InjectTextAsync(selectedText);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// If branching is active after transcription, store result and show comparison.
    /// Returns true if branching handled the result (caller should not inject).
    /// </summary>
    private bool HandleBranchingResult(string text)
    {
        if (!_isBranching) return false;

        Branches.Add(new VoiceBranch
        {
            Index = Branches.Count,
            Text = text,
            CreatedAt = DateTime.Now
        });

        ActiveBranchIndex = Branches.Count - 1;
        TranscriptionResult = text;
        HasResult = true;
        ShowBranchComparison = true;
        CurrentPhase = OrbPhase.Branching;
        BranchStatusText = $"Comparing {Branches.Count} branches — pick one to inject";
        _isBranching = false;
        Log($"Voice branch B created, showing comparison");

        return true;
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
        ShowPromptsGrid = false;
        ShowDirectionalHints = false;
        ShowContextPanel = false;
        ShowContextCards = false;
        ShowTargetApps = false;
        SelectedTargetApp = null;
        TranscriptionResult = string.Empty;
        HasResult = false;
        OrbAccentColor = Color.FromArgb(255, 0, 120, 212);
        ClipboardContext = ContextSource.Empty(ContextSourceType.Clipboard);
        ScreenshotContext = ContextSource.Empty(ContextSourceType.Screenshot);
        Branches.Clear();
        ShowBranchComparison = false;
        ActiveBranchIndex = 0;
        BranchStatusText = string.Empty;
        _isBranching = false;
    }
}
