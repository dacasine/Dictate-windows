namespace DictateForWindows.Core.Models;

/// <summary>
/// Represents an audio input device.
/// </summary>
public class AudioDevice
{
    /// <summary>
    /// Unique identifier for the device.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the device.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a Bluetooth device.
    /// </summary>
    public bool IsBluetooth { get; set; }

    /// <summary>
    /// Whether this is the default system device.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Device state (active, disabled, etc.).
    /// </summary>
    public AudioDeviceState State { get; set; }

    /// <summary>
    /// Number of input channels.
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; }
}

/// <summary>
/// State of an audio device.
/// </summary>
public enum AudioDeviceState
{
    Active,
    Disabled,
    NotPresent,
    Unplugged
}
