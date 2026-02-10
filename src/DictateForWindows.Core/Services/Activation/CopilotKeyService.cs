using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Activation;

/// <summary>
/// Service for intercepting the Copilot key (left Windows key pressed alone).
/// Uses a low-level keyboard hook to detect and suppress the default behavior.
/// </summary>
public class CopilotKeyService : ICopilotKeyService, IDisposable
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "copilot.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private readonly ILogger<CopilotKeyService>? _logger;
    private IntPtr _hookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private bool _disposed;
    private bool _isEnabled;

    // Track key state
    private bool _winKeyDown;
    private bool _otherKeyPressed;
    private DateTime _winKeyDownTime;

    // Delegate for the hook callback
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Win32 constants
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    public event EventHandler? CopilotKeyPressed;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (_isEnabled)
            {
                InstallHook();
            }
            else
            {
                UninstallHook();
            }
        }
    }

    public CopilotKeyService(ILogger<CopilotKeyService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enable the Copilot key hook.
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
    }

    /// <summary>
    /// Disable the Copilot key hook.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
    }

    private void InstallHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return; // Already installed
        }

        Log("Installing keyboard hook...");
        _hookProc = HookCallback;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, IntPtr.Zero, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Log($"Failed to install hook: error {error}");
            _logger?.LogError("Failed to install keyboard hook: error {Error}", error);
        }
        else
        {
            Log($"Hook installed successfully: {_hookHandle}");
            _logger?.LogInformation("Copilot key hook installed");
        }
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        Log("Uninstalling keyboard hook...");
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
        Log("Hook uninstalled");
        _logger?.LogInformation("Copilot key hook uninstalled");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var msgType = wParam.ToInt32();

            // Check for Windows key
            if (vkCode == VK_LWIN || vkCode == VK_RWIN)
            {
                if (msgType == WM_KEYDOWN || msgType == WM_SYSKEYDOWN)
                {
                    if (!_winKeyDown)
                    {
                        Log($"Win key down (vk={vkCode}) - SUPPRESSING");
                        _winKeyDown = true;
                        _otherKeyPressed = false;
                        _winKeyDownTime = DateTime.UtcNow;

                        // Suppress the KEYDOWN to prevent Windows from receiving it
                        return new IntPtr(1);
                    }
                }
                else if (msgType == WM_KEYUP || msgType == WM_SYSKEYUP)
                {
                    Log($"Win key up (vk={vkCode}), otherKeyPressed={_otherKeyPressed}");

                    bool shouldSuppress = false;

                    if (_winKeyDown && !_otherKeyPressed)
                    {
                        // Win key was pressed and released alone
                        var duration = DateTime.UtcNow - _winKeyDownTime;
                        Log($"Win key pressed alone for {duration.TotalMilliseconds}ms");

                        // Only trigger if held briefly (not a long press for Start menu)
                        if (duration.TotalMilliseconds < 500)
                        {
                            Log("Triggering CopilotKeyPressed event");
                            shouldSuppress = true;

                            // Fire event on UI thread
                            Task.Run(() => CopilotKeyPressed?.Invoke(this, EventArgs.Empty));
                        }
                    }

                    _winKeyDown = false;
                    _otherKeyPressed = false;

                    if (shouldSuppress)
                    {
                        // Suppress the key to prevent Start menu from opening
                        return new IntPtr(1);
                    }
                }
            }
            else if (_winKeyDown)
            {
                // Another key was pressed while Win is held
                if (msgType == WM_KEYDOWN || msgType == WM_SYSKEYDOWN)
                {
                    Log($"Other key pressed while Win held: vk={vkCode}");
                    _otherKeyPressed = true;

                    // Need to "replay" the Win key so Win+X shortcuts work
                    // Send a Win key down event
                    keybd_event((byte)VK_LWIN, 0, 0, UIntPtr.Zero);
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UninstallHook();
    }

    #region Native Methods

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    #endregion
}

/// <summary>
/// Interface for Copilot key service.
/// </summary>
public interface ICopilotKeyService
{
    event EventHandler? CopilotKeyPressed;
    bool IsEnabled { get; set; }
    void Enable();
    void Disable();
}
