using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace DictateForWindows.Core.Services.TextInjection;

/// <summary>
/// Service for injecting text into applications using clipboard + Ctrl+V.
/// Preserves and restores the original clipboard content.
/// </summary>
public class ClipboardInjector : ITextInjector
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "injection.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private readonly ILogger<ClipboardInjector>? _logger;
    private readonly SemaphoreSlim _clipboardLock = new(1, 1);

    private const int PostPasteDelayMs = 150; // Increased to ensure paste completes

    // Win32 imports for SendInput
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP_EVENT = 0x0002;

    // Store the target window to inject text into
    private IntPtr _targetWindow = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_RETURN = 0x0D;

    public ClipboardInjector(ILogger<ClipboardInjector>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Save the current foreground window as the target for text injection.
    /// Call this BEFORE showing any popup/overlay.
    /// </summary>
    public void SaveTargetWindow()
    {
        _targetWindow = GetForegroundWindow();
        Log($"Saved target window: {_targetWindow}");
    }

    /// <summary>
    /// Restore focus to the saved target window.
    /// </summary>
    private bool RestoreTargetWindow()
    {
        if (_targetWindow == IntPtr.Zero)
        {
            Log("No target window saved");
            return false;
        }

        Log($"Restoring focus to target window: {_targetWindow}");

        // Get thread IDs - GetWindowThreadProcessId RETURNS the thread ID
        var currentThreadId = GetCurrentThreadId();
        var targetThreadId = GetWindowThreadProcessId(_targetWindow, out _);

        Log($"Current thread: {currentThreadId}, Target thread: {targetThreadId}");

        // Attach thread input to allow SetForegroundWindow
        if (currentThreadId != targetThreadId)
        {
            AttachThreadInput(currentThreadId, targetThreadId, true);
        }

        var result = SetForegroundWindow(_targetWindow);

        if (currentThreadId != targetThreadId)
        {
            AttachThreadInput(currentThreadId, targetThreadId, false);
        }

        Log($"SetForegroundWindow result: {result}");
        return result;
    }

    /// <summary>
    /// Inject text into the currently focused application.
    /// </summary>
    public async Task<bool> InjectTextAsync(string text, TextInjectionOptions? options = null)
    {
        Log($"InjectTextAsync called with text: {text?.Substring(0, Math.Min(50, text?.Length ?? 0))}");
        options ??= new TextInjectionOptions();
        Log($"Options: AnimateTyping={options.AnimateTyping}, AnimationSpeedMs={options.AnimationSpeedMs}, AutoEnter={options.AutoEnter}");

        if (string.IsNullOrEmpty(text))
        {
            Log("Text is empty, returning true");
            return true;
        }

        Log("Waiting for clipboard lock...");
        await _clipboardLock.WaitAsync();
        Log("Got clipboard lock");
        try
        {
            // Save original clipboard content
            string? originalText = null;
            try
            {
                Log("Saving original clipboard content...");
                var content = Clipboard.GetContent();
                if (content.Contains(StandardDataFormats.Text))
                {
                    originalText = await content.GetTextAsync();
                    Log($"Original clipboard saved: {originalText?.Substring(0, Math.Min(20, originalText?.Length ?? 0))}...");
                }
            }
            catch (Exception ex)
            {
                Log($"Could not save original clipboard: {ex.Message}");
                _logger?.LogWarning(ex, "Could not save original clipboard");
            }

            try
            {
                // Restore focus to target window before pasting
                Log("Restoring target window focus...");
                RestoreTargetWindow();
                await Task.Delay(100); // Delay to ensure focus is fully set

                // Handle animated typing
                if (options.AnimateTyping && options.AnimationSpeedMs > 0)
                {
                    Log("Using animated typing...");
                    foreach (var c in text)
                    {
                        await SetClipboardTextAsync(c.ToString());
                        SendPaste();
                        await Task.Delay(options.AnimationSpeedMs);
                    }
                }
                else
                {
                    // Set text to clipboard and paste
                    Log("Setting clipboard text...");
                    await SetClipboardTextAsync(text);
                    Log("Sending Ctrl+V...");
                    SendPaste();
                }

                await Task.Delay(PostPasteDelayMs);
                Log("Paste sent successfully");

                // Auto-enter if requested
                if (options.AutoEnter)
                {
                    Log("Sending Enter...");
                    SendEnter();
                }

                Log("InjectTextAsync completed successfully");
                return true;
            }
            finally
            {
                // Don't restore clipboard - let the transcribed text stay available for manual paste if needed
                // This avoids race conditions where clipboard is restored before paste completes
                Log("Skipping clipboard restoration - text remains in clipboard");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR in InjectTextAsync: {ex}");
            _logger?.LogError(ex, "Failed to inject text");
            return false;
        }
        finally
        {
            _clipboardLock.Release();
            Log("Clipboard lock released");
        }
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
        await Task.Delay(10); // Small delay to ensure clipboard is set
    }

    private static void SendPaste()
    {
        Log("Using keybd_event for Ctrl+V...");

        // Use keybd_event which is more reliable than SendInput
        // Ctrl down
        keybd_event((byte)VK_CONTROL, 0x1D, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

        // Small delay between key presses
        Thread.Sleep(10);

        // V down
        keybd_event((byte)VK_V, 0x2F, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

        Thread.Sleep(10);

        // V up
        keybd_event((byte)VK_V, 0x2F, KEYEVENTF_KEYUP_EVENT, UIntPtr.Zero);

        Thread.Sleep(10);

        // Ctrl up
        keybd_event((byte)VK_CONTROL, 0x1D, KEYEVENTF_KEYUP_EVENT, UIntPtr.Zero);

        Log("keybd_event Ctrl+V sent");
    }

    private static void SendEnter()
    {
        var inputs = new INPUT[2];

        // Enter down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = VK_RETURN;

        // Enter up
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = VK_RETURN;
        inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Send backspace keystrokes.
    /// </summary>
    public void SendBackspace(int count = 1)
    {
        const ushort VK_BACK = 0x08;

        for (int i = 0; i < count; i++)
        {
            var inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = VK_BACK;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = VK_BACK;
            inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }
    }
}

/// <summary>
/// Options for text injection.
/// </summary>
public class TextInjectionOptions
{
    /// <summary>
    /// Whether to animate typing character by character.
    /// </summary>
    public bool AnimateTyping { get; set; }

    /// <summary>
    /// Delay between characters when animating (in ms).
    /// </summary>
    public int AnimationSpeedMs { get; set; } = 20;

    /// <summary>
    /// Whether to press Enter after injecting text.
    /// </summary>
    public bool AutoEnter { get; set; }
}

/// <summary>
/// Interface for text injection services.
/// </summary>
public interface ITextInjector
{
    /// <summary>
    /// Save the current foreground window as the target for injection.
    /// Call this BEFORE showing any UI that might take focus.
    /// </summary>
    void SaveTargetWindow();

    Task<bool> InjectTextAsync(string text, TextInjectionOptions? options = null);
    void SendBackspace(int count = 1);
}
