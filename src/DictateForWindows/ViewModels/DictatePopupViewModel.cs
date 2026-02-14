using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Audio;
using DictateForWindows.Core.Services.Rewording;
using DictateForWindows.Core.Services.Settings;
using DictateForWindows.Core.Services.TextInjection;
using DictateForWindows.Core.Services.Transcription;
using Microsoft.UI.Dispatching;
using Windows.UI;

namespace DictateForWindows.ViewModels;

/// <summary>
/// ViewModel for the dictation popup.
/// </summary>
public partial class DictatePopupViewModel : ObservableObject
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "dictate.log");

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
    private readonly IAudioRecordingService _recordingService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IRewordingService _rewordingService;
    private readonly ITextInjector _textInjector;
    private readonly IPromptsRepository _promptsRepository;
    private readonly IUsageRepository _usageRepository;
    private readonly DispatcherQueue _dispatcherQueue;

    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _timerText = "00:00";

    [ObservableProperty]
    private bool _isTimerVisible;

    [ObservableProperty]
    private string _transcriptionResult = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingText = "Processing...";

    [ObservableProperty]
    private bool _isRecordButtonVisible = true;

    [ObservableProperty]
    private bool _showPauseButton;

    [ObservableProperty]
    private bool _showCancelButton;

    [ObservableProperty]
    private string _pauseButtonGlyph = "\uE769"; // Pause icon

    [ObservableProperty]
    private bool _showPromptsPanel = true;

    public ObservableCollection<PromptItemViewModel> Prompts { get; } = new();

    /// <summary>
    /// Event raised when the popup should be hidden (before text injection).
    /// </summary>
    public event EventHandler? RequestHide;

    public bool InstantRecording => _settings.GetBool("dictate_instant_recording", false);

    public DictatePopupViewModel(
        ISettingsService settings,
        IAudioRecordingService recordingService,
        ITranscriptionService transcriptionService,
        IRewordingService rewordingService,
        ITextInjector textInjector,
        IPromptsRepository promptsRepository,
        IUsageRepository usageRepository)
    {
        _settings = settings;
        _recordingService = recordingService;
        _transcriptionService = transcriptionService;
        _rewordingService = rewordingService;
        _textInjector = textInjector;
        _promptsRepository = promptsRepository;
        _usageRepository = usageRepository;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _recordingService.StateChanged += OnRecordingStateChanged;
        _recordingService.Progress += OnRecordingProgress;

        LoadPrompts();
    }

    private void LoadPrompts()
    {
        Prompts.Clear();
        var prompts = _promptsRepository.GetAllForKeyboard();

        foreach (var prompt in prompts)
        {
            var vm = new PromptItemViewModel(prompt, OnPromptClicked);
            Prompts.Add(vm);
        }
    }

    private void OnPromptClicked(PromptModel prompt)
    {
        switch (prompt.Id)
        {
            case SpecialPromptIds.Instant:
                // Toggle instant mode
                break;

            case SpecialPromptIds.SelectAll:
                // Select all text
                break;

            case SpecialPromptIds.Add:
                // Open prompts manager
                App.Current.ShowPromptsManager();
                break;

            default:
                // Set active prompt or apply prompt
                if (IsRecording)
                {
                    SetActivePrompt(prompt.Id);
                }
                else if (!string.IsNullOrEmpty(TranscriptionResult))
                {
                    _ = ApplyPromptAsync(prompt);
                }
                break;
        }
    }

    private void SetActivePrompt(int promptId)
    {
        foreach (var prompt in Prompts)
        {
            prompt.IsActive = prompt.Id == promptId;
        }
    }

    [RelayCommand]
    public void ToggleRecording()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    public async void StartRecording()
    {
        Log("StartRecording called");
        if (IsRecording)
        {
            Log("Already recording, returning");
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();

        StatusText = "Starting...";
        Log("Calling _recordingService.StartRecordingAsync()...");
        try
        {
            var success = await _recordingService.StartRecordingAsync();
            Log($"StartRecordingAsync returned: {success}");

            if (!success)
            {
                StatusText = "Failed to start recording";
                Log("Recording failed to start");
            }
            else
            {
                Log("Recording started successfully");
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
        if (!IsRecording)
        {
            Log("Not recording, returning");
            return;
        }

        StatusText = "Stopping...";
        Log("Calling _recordingService.StopRecordingAsync()...");
        var audioPath = await _recordingService.StopRecordingAsync();
        Log($"StopRecordingAsync returned: {audioPath ?? "null"}");

        if (audioPath != null)
        {
            await TranscribeAsync(audioPath);
        }
        else
        {
            Log("No audio path returned, cannot transcribe");
        }
    }

    public void CancelRecording()
    {
        _cancellationTokenSource?.Cancel();
        _recordingService.CancelRecording();
        ResetState();
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (IsPaused)
        {
            _recordingService.ResumeRecording();
        }
        else
        {
            _recordingService.PauseRecording();
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        if (IsRecording || IsProcessing)
        {
            CancelRecording();
        }
        else
        {
            // Clear result
            TranscriptionResult = string.Empty;
            HasResult = false;
            ResetState();
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        App.Current.ShowSettings();
    }

    private async Task TranscribeAsync(string audioPath)
    {
        Log($"TranscribeAsync starting with path: {audioPath}");
        IsProcessing = true;
        IsRecordButtonVisible = false;
        ProcessingText = "Transcribing...";
        StatusText = "Sending to API...";

        try
        {
            Log("Calling _transcriptionService.TranscribeAsync...");
            var result = await _transcriptionService.TranscribeAsync(audioPath, _cancellationTokenSource?.Token ?? default);
            Log($"TranscribeAsync result: IsSuccess={result.IsSuccess}, Text={result.Text?.Substring(0, Math.Min(50, result.Text?.Length ?? 0))}, Error={result.Error}");

            if (result.IsSuccess)
            {
                // Track usage
                var model = _settings.TranscriptionModel;
                var provider = _settings.ApiProvider;
                _usageRepository.RecordUsage(model, result.DurationMs, 0, 0, provider);

                var text = result.Text;

                // Apply auto-formatting if enabled
                if (_settings.AutoFormattingEnabled)
                {
                    ProcessingText = "Formatting...";
                    var formatResult = await _rewordingService.ApplyAutoFormattingAsync(text, _cancellationTokenSource?.Token ?? default);
                    if (formatResult.IsSuccess)
                    {
                        text = formatResult.Text;
                    }
                }

                // Apply active prompt if one is selected
                var activePrompt = Prompts.FirstOrDefault(p => p.IsActive);
                if (activePrompt != null && activePrompt.Id != SpecialPromptIds.Default)
                {
                    var prompt = _promptsRepository.Get(activePrompt.Id);
                    if (prompt != null)
                    {
                        ProcessingText = $"Applying: {prompt.Name}";
                        StatusText = $"Running prompt: {prompt.Name}";

                        var rewordResult = await _rewordingService.RewordAsync(
                            text,
                            prompt.Prompt,
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

                TranscriptionResult = text;
                HasResult = true;
                StatusText = "Done";

                // Hide the popup before injecting to restore focus to target window
                Log("Requesting popup hide before injection...");
                RequestHide?.Invoke(this, EventArgs.Empty);
                await Task.Delay(100); // Give time for popup to hide and focus to restore

                // Inject text
                await InjectTextAsync(text);
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
            IsRecordButtonVisible = true;
        }
    }

    private async Task ApplyPromptAsync(PromptModel prompt)
    {
        IsProcessing = true;
        ProcessingText = $"Applying: {prompt.Name}";

        try
        {
            var result = await _rewordingService.RewordAsync(
                TranscriptionResult,
                prompt.Prompt,
                RewordingService.DefaultSystemPrompt,
                _cancellationTokenSource?.Token ?? default);

            if (result.IsSuccess)
            {
                TranscriptionResult = result.Text;

                // Track usage
                var model = _settings.RewordingModel;
                var provider = _settings.ApiProvider;
                _usageRepository.RecordUsage(model, 0, result.InputTokens, result.OutputTokens, provider);

                // Inject text
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
        Log($"InjectTextAsync called with text: {text?.Substring(0, Math.Min(50, text?.Length ?? 0))}");
        var options = new TextInjectionOptions
        {
            AnimateTyping = !_settings.InstantMode,
            AnimationSpeedMs = _settings.AnimationSpeed,
            AutoEnter = _settings.AutoEnter
        };
        Log($"Injection options: AnimateTyping={options.AnimateTyping}, Speed={options.AnimationSpeedMs}, AutoEnter={options.AutoEnter}");

        var result = await _textInjector.InjectTextAsync(text, options);
        Log($"InjectTextAsync result: {result}");
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

        // Show resend button for retriable errors
        if (result.ErrorType == TranscriptionError.Timeout || result.ErrorType == TranscriptionError.NetworkError)
        {
            // TODO: Show resend button
        }
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsRecording = e.NewState == RecordingState.Recording || e.NewState == RecordingState.Paused;
            IsPaused = e.NewState == RecordingState.Paused;
            IsRecordButtonVisible = e.NewState == RecordingState.Idle || e.NewState == RecordingState.Recording || e.NewState == RecordingState.Paused;
            ShowPauseButton = IsRecording;
            ShowCancelButton = IsRecording;
            IsTimerVisible = IsRecording;
            PauseButtonGlyph = IsPaused ? "\uE768" : "\uE769"; // Play or Pause icon

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
        });
    }

    private void ResetState()
    {
        StatusText = "Ready";
        TimerText = "00:00";
        IsTimerVisible = false;
        IsRecording = false;
        IsPaused = false;
        IsProcessing = false;
        IsRecordButtonVisible = true;
        ShowPauseButton = false;
        ShowCancelButton = false;
    }
}

/// <summary>
/// ViewModel for a prompt item in the prompts panel.
/// </summary>
public partial class PromptItemViewModel : ObservableObject
{
    private readonly PromptModel _prompt;
    private readonly Action<PromptModel> _onClick;

    public int Id => _prompt.Id;
    public string Name => _prompt.Name;
    public string Tooltip => _prompt.Prompt;
    public bool RequiresSelection => _prompt.RequiresSelection;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private Color _backgroundColor = Color.FromArgb(255, 0, 120, 212);

    public PromptItemViewModel(PromptModel prompt, Action<PromptModel> onClick)
    {
        _prompt = prompt;
        _onClick = onClick;
    }

    [RelayCommand]
    public void Click()
    {
        _onClick(_prompt);
    }

    partial void OnIsActiveChanged(bool value)
    {
        BackgroundColor = value
            ? Color.FromArgb(255, 16, 185, 129) // Green when active (#10B981)
            : Color.FromArgb(255, 0, 120, 212);  // Blue default
    }
}
