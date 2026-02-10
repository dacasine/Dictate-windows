using NAudio.CoreAudioApi;
using NAudio.Wave;
using DictateForWindows.Core.Models;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Audio;

/// <summary>
/// Service for enumerating and managing audio input devices.
/// </summary>
public class AudioDeviceService : IAudioDeviceService, IDisposable
{
    private readonly ILogger<AudioDeviceService>? _logger;
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private bool _disposed;

    public event EventHandler<AudioDevice>? DeviceAdded;
    public event EventHandler<string>? DeviceRemoved;
    public event EventHandler<AudioDevice>? DefaultDeviceChanged;

    public AudioDeviceService(ILogger<AudioDeviceService>? logger = null)
    {
        _logger = logger;
        _deviceEnumerator = new MMDeviceEnumerator();
    }

    /// <summary>
    /// Gets all available audio input devices.
    /// </summary>
    public Task<IEnumerable<AudioDevice>> GetInputDevicesAsync()
    {
        return Task.Run(() =>
        {
            var devices = new List<AudioDevice>();

            try
            {
                // Get default device first
                MMDevice? defaultDevice = null;
                try
                {
                    defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                }
                catch
                {
                    // No default device
                }

                // Enumerate all capture devices
                var mmDevices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

                foreach (var device in mmDevices)
                {
                    try
                    {
                        var audioDevice = ConvertToAudioDevice(device, device.ID == defaultDevice?.ID);
                        devices.Add(audioDevice);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to enumerate device {DeviceId}", device.ID);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enumerate audio devices");
            }

            // Also add WaveIn devices as fallback
            var waveInDevices = GetWaveInDevices(devices);
            foreach (var device in waveInDevices)
            {
                if (!devices.Any(d => d.Name.Contains(device.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    devices.Add(device);
                }
            }

            return devices.AsEnumerable();
        });
    }

    /// <summary>
    /// Gets Bluetooth audio input devices.
    /// </summary>
    public Task<IEnumerable<AudioDevice>> GetBluetoothDevicesAsync()
    {
        return Task.Run(async () =>
        {
            var allDevices = await GetInputDevicesAsync();
            return allDevices.Where(d => d.IsBluetooth);
        });
    }

    /// <summary>
    /// Gets the default audio input device.
    /// </summary>
    public Task<AudioDevice?> GetDefaultDeviceAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                return ConvertToAudioDevice(defaultDevice, true);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get default audio device");
                return null;
            }
        });
    }

    /// <summary>
    /// Gets a device by its ID.
    /// </summary>
    public Task<AudioDevice?> GetDeviceByIdAsync(string deviceId)
    {
        return Task.Run(() =>
        {
            try
            {
                var device = _deviceEnumerator.GetDevice(deviceId);
                if (device != null)
                {
                    var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    return ConvertToAudioDevice(device, device.ID == defaultDevice?.ID);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get device {DeviceId}", deviceId);
            }
            return null;
        });
    }

    /// <summary>
    /// Checks if a Bluetooth microphone is available and connected.
    /// </summary>
    public async Task<bool> IsBluetoothMicAvailableAsync()
    {
        var btDevices = await GetBluetoothDevicesAsync();
        return btDevices.Any(d => d.State == AudioDeviceState.Active);
    }

    /// <summary>
    /// Waits for a Bluetooth microphone to become available.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>The Bluetooth device if found within timeout, null otherwise.</returns>
    public async Task<AudioDevice?> WaitForBluetoothMicAsync(int timeoutMs = 2500)
    {
        var startTime = DateTime.UtcNow;

        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            var btDevices = await GetBluetoothDevicesAsync();
            var activeDevice = btDevices.FirstOrDefault(d => d.State == AudioDeviceState.Active);

            if (activeDevice != null)
            {
                _logger?.LogInformation("Bluetooth microphone found: {DeviceName}", activeDevice.Name);
                return activeDevice;
            }

            await Task.Delay(100);
        }

        _logger?.LogInformation("Bluetooth microphone not found within {Timeout}ms timeout", timeoutMs);
        return null;
    }

    private AudioDevice ConvertToAudioDevice(MMDevice device, bool isDefault)
    {
        var isBluetooth = IsBluetoothDevice(device);
        var state = ConvertDeviceState(device.State);

        // Get format info
        var format = device.AudioClient?.MixFormat;

        return new AudioDevice
        {
            Id = device.ID,
            Name = device.FriendlyName,
            IsBluetooth = isBluetooth,
            IsDefault = isDefault,
            State = state,
            Channels = format?.Channels ?? 1,
            SampleRate = format?.SampleRate ?? 44100
        };
    }

    private static bool IsBluetoothDevice(MMDevice device)
    {
        var name = device.FriendlyName?.ToLowerInvariant() ?? "";
        var id = device.ID?.ToLowerInvariant() ?? "";

        // Check common Bluetooth indicators
        return name.Contains("bluetooth") ||
               name.Contains("hands-free") ||
               name.Contains("headset") ||
               name.Contains("airpods") ||
               name.Contains("wireless") ||
               id.Contains("bth") ||
               id.Contains("bluetooth");
    }

    private static AudioDeviceState ConvertDeviceState(DeviceState state)
    {
        return state switch
        {
            DeviceState.Active => AudioDeviceState.Active,
            DeviceState.Disabled => AudioDeviceState.Disabled,
            DeviceState.NotPresent => AudioDeviceState.NotPresent,
            DeviceState.Unplugged => AudioDeviceState.Unplugged,
            _ => AudioDeviceState.NotPresent
        };
    }

    private IEnumerable<AudioDevice> GetWaveInDevices(List<AudioDevice> existingDevices)
    {
        var devices = new List<AudioDevice>();
        var deviceCount = WaveInEvent.DeviceCount;

        for (int i = 0; i < deviceCount; i++)
        {
            try
            {
                var caps = WaveInEvent.GetCapabilities(i);
                var name = caps.ProductName;

                // Skip if we already have this device from MMDevice enumeration
                if (existingDevices.Any(d => d.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                devices.Add(new AudioDevice
                {
                    Id = $"wavein:{i}",
                    Name = name,
                    IsBluetooth = name.ToLowerInvariant().Contains("bluetooth") ||
                                  name.ToLowerInvariant().Contains("headset"),
                    IsDefault = i == 0,
                    State = AudioDeviceState.Active,
                    Channels = caps.Channels,
                    SampleRate = 44100
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get WaveIn device {Index}", i);
            }
        }

        return devices;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _deviceEnumerator.Dispose();
    }
}

/// <summary>
/// Interface for audio device service.
/// </summary>
public interface IAudioDeviceService
{
    event EventHandler<AudioDevice>? DeviceAdded;
    event EventHandler<string>? DeviceRemoved;
    event EventHandler<AudioDevice>? DefaultDeviceChanged;

    Task<IEnumerable<AudioDevice>> GetInputDevicesAsync();
    Task<IEnumerable<AudioDevice>> GetBluetoothDevicesAsync();
    Task<AudioDevice?> GetDefaultDeviceAsync();
    Task<AudioDevice?> GetDeviceByIdAsync(string deviceId);
    Task<bool> IsBluetoothMicAvailableAsync();
    Task<AudioDevice?> WaitForBluetoothMicAsync(int timeoutMs = 2500);
}
