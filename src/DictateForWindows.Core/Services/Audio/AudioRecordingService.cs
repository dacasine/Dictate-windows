using NAudio.Wave;
using NAudio.MediaFoundation;
using DictateForWindows.Core.Models;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Audio;

/// <summary>
/// Service for recording audio from microphone using NAudio.
/// Records to M4A (AAC) format at 64kbps, 44.1kHz to match Android behavior.
/// </summary>
public class AudioRecordingService : IAudioRecordingService, IDisposable
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "audio.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private readonly ILogger<AudioRecordingService>? _logger;
    private readonly IAudioDeviceService _deviceService;

    private WaveInEvent? _waveIn;
    private MediaFoundationEncoder? _encoder;
    private FileStream? _outputStream;
    private WaveFileWriter? _waveWriter;
    private string? _tempWavPath;
    private string? _outputPath;

    private RecordingState _state = RecordingState.Idle;
    private DateTime _recordingStartTime;
    private TimeSpan _pausedDuration;
    private DateTime _pauseStartTime;
    private float _currentAudioLevel;
    private bool _disposed;

    private readonly System.Timers.Timer _progressTimer;
    private const int ProgressUpdateIntervalMs = 100;

    // Audio format settings (matching Android: AAC 64kbps @ 44.1kHz)
    private const int SampleRate = 44100;
    private const int Channels = 1; // Mono
    private const int BitsPerSample = 16;
    private const int AacBitrate = 64000;

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;
    public event EventHandler<RecordingProgressEventArgs>? Progress;

    public RecordingState State => _state;
    public TimeSpan Duration => GetCurrentDuration();
    public float AudioLevel => _currentAudioLevel;
    public string? OutputPath => _outputPath;

    public AudioRecordingService(IAudioDeviceService deviceService, ILogger<AudioRecordingService>? logger = null)
    {
        _deviceService = deviceService;
        _logger = logger;

        _progressTimer = new System.Timers.Timer(ProgressUpdateIntervalMs);
        _progressTimer.Elapsed += OnProgressTimerElapsed;

        // Initialize Media Foundation for AAC encoding
        MediaFoundationApi.Startup();
    }

    /// <summary>
    /// Start recording audio.
    /// </summary>
    /// <param name="outputPath">Path to save the M4A file.</param>
    /// <param name="deviceId">Optional device ID. If null, uses default device.</param>
    public async Task<bool> StartRecordingAsync(string? outputPath = null, string? deviceId = null)
    {
        Log($"StartRecordingAsync called. Current state: {_state}");
        if (_state != RecordingState.Idle)
        {
            Log($"Cannot start: already in state {_state}");
            _logger?.LogWarning("Cannot start recording: already in state {State}", _state);
            return false;
        }

        try
        {
            Log("Setting state to Initializing");
            SetState(RecordingState.Initializing);

            // Setup output path
            _outputPath = outputPath ?? GetDefaultOutputPath();
            _tempWavPath = Path.ChangeExtension(_outputPath, ".wav");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Get the recording device
            Log("Getting recording device...");
            var device = await GetRecordingDeviceAsync(deviceId);
            if (device == null)
            {
                Log("ERROR: No audio recording device available");
                throw new InvalidOperationException("No audio recording device available");
            }
            Log($"Got device: {device.Name} (ID: {device.Id})");

            // Setup WaveIn
            Log("Creating WaveInEvent...");
            _waveIn = new WaveInEvent
            {
                DeviceNumber = GetDeviceNumber(device.Id),
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            // Create WAV writer (we'll convert to M4A after recording)
            Log($"Creating WAV writer at: {_tempWavPath}");
            _waveWriter = new WaveFileWriter(_tempWavPath, _waveIn.WaveFormat);

            // Start recording
            Log("Starting WaveIn recording...");
            _waveIn.StartRecording();
            _recordingStartTime = DateTime.UtcNow;
            _pausedDuration = TimeSpan.Zero;
            _progressTimer.Start();

            SetState(RecordingState.Recording);
            Log($"Recording started successfully to {_outputPath}");
            _logger?.LogInformation("Recording started to {Path}", _outputPath);
            return true;
        }
        catch (Exception ex)
        {
            Log($"ERROR starting recording: {ex}");
            _logger?.LogError(ex, "Failed to start recording");
            SetState(RecordingState.Error, ex.Message);
            Cleanup();
            return false;
        }
    }

    /// <summary>
    /// Stop recording and save the audio file.
    /// </summary>
    public async Task<string?> StopRecordingAsync()
    {
        Log($"StopRecordingAsync called. Current state: {_state}");
        if (_state != RecordingState.Recording && _state != RecordingState.Paused)
        {
            Log($"Cannot stop: not recording (state: {_state})");
            _logger?.LogWarning("Cannot stop recording: not recording (state: {State})", _state);
            return null;
        }

        try
        {
            _progressTimer.Stop();
            Log("Setting state to Processing");
            SetState(RecordingState.Processing);

            // Stop wave recording
            Log("Stopping WaveIn...");
            _waveIn?.StopRecording();

            // Wait for recording to fully stop
            await Task.Delay(100);

            // Close the WAV writer
            Log("Disposing WAV writer...");
            _waveWriter?.Dispose();
            _waveWriter = null;

            // Convert WAV to M4A (AAC)
            Log($"Checking temp WAV: {_tempWavPath}, exists: {(_tempWavPath != null ? File.Exists(_tempWavPath) : false)}");
            if (_tempWavPath != null && _outputPath != null && File.Exists(_tempWavPath))
            {
                var wavFileInfo = new FileInfo(_tempWavPath);
                Log($"WAV file size: {wavFileInfo.Length} bytes");
                Log($"Converting to AAC: {_outputPath}");
                await ConvertToAacAsync(_tempWavPath, _outputPath);
                Log($"AAC conversion done. File exists: {File.Exists(_outputPath)}");

                // Delete temp WAV file
                try
                {
                    File.Delete(_tempWavPath);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
            else
            {
                Log("ERROR: WAV file not found, cannot convert to AAC");
            }

            var result = _outputPath;
            Log($"Recording saved to {result}");
            _logger?.LogInformation("Recording saved to {Path}", result);

            Cleanup();
            SetState(RecordingState.Idle);
            return result;
        }
        catch (Exception ex)
        {
            Log($"ERROR stopping recording: {ex}");
            _logger?.LogError(ex, "Failed to stop recording");
            SetState(RecordingState.Error, ex.Message);
            Cleanup();
            return null;
        }
    }

    /// <summary>
    /// Pause the current recording.
    /// </summary>
    public void PauseRecording()
    {
        if (_state != RecordingState.Recording)
        {
            return;
        }

        _waveIn?.StopRecording();
        _pauseStartTime = DateTime.UtcNow;
        _progressTimer.Stop();
        SetState(RecordingState.Paused);
        _logger?.LogInformation("Recording paused");
    }

    /// <summary>
    /// Resume a paused recording.
    /// </summary>
    public void ResumeRecording()
    {
        if (_state != RecordingState.Paused)
        {
            return;
        }

        _pausedDuration += DateTime.UtcNow - _pauseStartTime;
        _waveIn?.StartRecording();
        _progressTimer.Start();
        SetState(RecordingState.Recording);
        _logger?.LogInformation("Recording resumed");
    }

    /// <summary>
    /// Cancel the current recording without saving.
    /// </summary>
    public void CancelRecording()
    {
        if (_state == RecordingState.Idle)
        {
            return;
        }

        _progressTimer.Stop();
        _waveIn?.StopRecording();

        // Delete output files
        try
        {
            if (_tempWavPath != null && File.Exists(_tempWavPath))
            {
                File.Delete(_tempWavPath);
            }
            if (_outputPath != null && File.Exists(_outputPath))
            {
                File.Delete(_outputPath);
            }
        }
        catch
        {
            // Ignore deletion errors
        }

        Cleanup();
        SetState(RecordingState.Idle);
        _logger?.LogInformation("Recording cancelled");
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_waveWriter == null || _state != RecordingState.Recording)
        {
            return;
        }

        // Write audio data
        _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);

        // Calculate audio level for visualization
        _currentAudioLevel = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger?.LogError(e.Exception, "Recording stopped with error");
            SetState(RecordingState.Error, e.Exception.Message);
        }
    }

    private void OnProgressTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var duration = GetCurrentDuration();
        Progress?.Invoke(this, new RecordingProgressEventArgs(duration, _currentAudioLevel));
    }

    private TimeSpan GetCurrentDuration()
    {
        if (_state == RecordingState.Idle || _state == RecordingState.Error)
        {
            return TimeSpan.Zero;
        }

        var elapsed = DateTime.UtcNow - _recordingStartTime - _pausedDuration;
        if (_state == RecordingState.Paused)
        {
            elapsed -= DateTime.UtcNow - _pauseStartTime;
        }

        return elapsed;
    }

    private float CalculateAudioLevel(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded == 0) return 0;

        // Calculate RMS for 16-bit audio
        double sum = 0;
        int samples = bytesRecorded / 2;

        for (int i = 0; i < bytesRecorded; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            sum += sample * sample;
        }

        double rms = Math.Sqrt(sum / samples);
        return (float)Math.Min(1.0, rms / 32768.0 * 4); // Scale for better visualization
    }

    private async Task ConvertToAacAsync(string wavPath, string aacPath)
    {
        await Task.Run(() =>
        {
            using var reader = new WaveFileReader(wavPath);
            MediaFoundationEncoder.EncodeToAac(reader, aacPath, AacBitrate);
        });
    }

    private async Task<AudioDevice?> GetRecordingDeviceAsync(string? preferredDeviceId)
    {
        var devices = await _deviceService.GetInputDevicesAsync();
        var deviceList = devices.ToList();

        if (string.IsNullOrEmpty(preferredDeviceId))
        {
            return deviceList.FirstOrDefault(d => d.IsDefault) ?? deviceList.FirstOrDefault();
        }

        return deviceList.FirstOrDefault(d => d.Id == preferredDeviceId)
               ?? deviceList.FirstOrDefault(d => d.IsDefault)
               ?? deviceList.FirstOrDefault();
    }

    private int GetDeviceNumber(string deviceId)
    {
        // NAudio uses device numbers, not IDs
        // For now, return -1 to use default device
        // TODO: Implement proper device ID to number mapping
        return -1;
    }

    private static string GetDefaultOutputPath()
    {
        var tempPath = Path.GetTempPath();
        var dictateFolder = Path.Combine(tempPath, "DictateForWindows");
        Directory.CreateDirectory(dictateFolder);
        return Path.Combine(dictateFolder, "audio.m4a");
    }

    private void SetState(RecordingState newState, string? errorMessage = null)
    {
        var oldState = _state;
        _state = newState;
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(oldState, newState, errorMessage));
    }

    private void Cleanup()
    {
        _waveWriter?.Dispose();
        _waveWriter = null;

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _encoder?.Dispose();
        _encoder = null;

        _outputStream?.Dispose();
        _outputStream = null;

        _outputPath = null;
        _tempWavPath = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelRecording();
        _progressTimer.Dispose();
        MediaFoundationApi.Shutdown();
    }
}

/// <summary>
/// Interface for audio recording service.
/// </summary>
public interface IAudioRecordingService
{
    RecordingState State { get; }
    TimeSpan Duration { get; }
    float AudioLevel { get; }
    string? OutputPath { get; }

    event EventHandler<RecordingStateChangedEventArgs>? StateChanged;
    event EventHandler<RecordingProgressEventArgs>? Progress;

    Task<bool> StartRecordingAsync(string? outputPath = null, string? deviceId = null);
    Task<string?> StopRecordingAsync();
    void PauseRecording();
    void ResumeRecording();
    void CancelRecording();
}
