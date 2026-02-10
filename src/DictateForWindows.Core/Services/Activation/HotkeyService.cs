using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Services.Activation;

/// <summary>
/// Service for registering and handling global hotkeys.
/// Uses RegisterHotKey Windows API for system-wide hotkey registration.
/// </summary>
public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly ILogger<HotkeyService>? _logger;
    private readonly Dictionary<int, HotkeyRegistration> _registrations = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private bool _disposed;

    // Message-only window handle for receiving hotkey messages
    private IntPtr _windowHandle;
    private HotkeyWindowProc? _wndProc;
    private delegate IntPtr HotkeyWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int WM_HOTKEY = 0x0312;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public HotkeyService(ILogger<HotkeyService>? logger = null)
    {
        _logger = logger;
        CreateMessageWindow();
    }

    /// <summary>
    /// Register a global hotkey.
    /// </summary>
    /// <param name="modifiers">Modifier keys (Ctrl, Alt, Shift, Win).</param>
    /// <param name="key">Virtual key code.</param>
    /// <param name="name">Optional name for the hotkey.</param>
    /// <returns>Registration ID, or -1 if registration failed.</returns>
    public int RegisterHotkey(HotkeyModifiers modifiers, Keys key, string? name = null)
    {
        lock (_lock)
        {
            var id = _nextId++;

            if (!NativeMethods.RegisterHotKey(_windowHandle, id, (uint)modifiers, (uint)key))
            {
                var error = Marshal.GetLastWin32Error();
                _logger?.LogWarning("Failed to register hotkey {Name} ({Modifiers}+{Key}): error {Error}",
                    name ?? id.ToString(), modifiers, key, error);
                return -1;
            }

            _registrations[id] = new HotkeyRegistration
            {
                Id = id,
                Modifiers = modifiers,
                Key = key,
                Name = name
            };

            _logger?.LogInformation("Registered hotkey {Name}: {Modifiers}+{Key}",
                name ?? id.ToString(), modifiers, key);

            return id;
        }
    }

    /// <summary>
    /// Register a hotkey from a string representation (e.g., "Win+Shift+D").
    /// </summary>
    public int RegisterHotkey(string hotkeyString, string? name = null)
    {
        if (!TryParseHotkeyString(hotkeyString, out var modifiers, out var key))
        {
            _logger?.LogWarning("Invalid hotkey string: {Hotkey}", hotkeyString);
            return -1;
        }

        return RegisterHotkey(modifiers, key, name ?? hotkeyString);
    }

    /// <summary>
    /// Unregister a hotkey.
    /// </summary>
    public bool UnregisterHotkey(int id)
    {
        lock (_lock)
        {
            if (!_registrations.ContainsKey(id))
            {
                return false;
            }

            if (!NativeMethods.UnregisterHotKey(_windowHandle, id))
            {
                var error = Marshal.GetLastWin32Error();
                _logger?.LogWarning("Failed to unregister hotkey {Id}: error {Error}", id, error);
                return false;
            }

            _registrations.Remove(id);
            _logger?.LogInformation("Unregistered hotkey {Id}", id);
            return true;
        }
    }

    /// <summary>
    /// Unregister all hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        lock (_lock)
        {
            foreach (var id in _registrations.Keys.ToList())
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
            _registrations.Clear();
        }
    }

    /// <summary>
    /// Check if a hotkey combination is already registered.
    /// </summary>
    public bool IsRegistered(HotkeyModifiers modifiers, Keys key)
    {
        lock (_lock)
        {
            return _registrations.Values.Any(r => r.Modifiers == modifiers && r.Key == key);
        }
    }

    /// <summary>
    /// Parse a hotkey string like "Win+Shift+D" into modifiers and key.
    /// </summary>
    public static bool TryParseHotkeyString(string hotkeyString, out HotkeyModifiers modifiers, out Keys key)
    {
        modifiers = HotkeyModifiers.None;
        key = Keys.None;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var lowerPart = part.ToLowerInvariant();

            switch (lowerPart)
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "win":
                case "windows":
                case "super":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    // Try to parse as key
                    if (Enum.TryParse<Keys>(part, true, out var parsedKey))
                    {
                        key = parsedKey;
                    }
                    else if (part.Length == 1)
                    {
                        // Single character
                        key = (Keys)char.ToUpperInvariant(part[0]);
                    }
                    break;
            }
        }

        return key != Keys.None;
    }

    /// <summary>
    /// Format a hotkey to a display string.
    /// </summary>
    public static string FormatHotkey(HotkeyModifiers modifiers, Keys key)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(HotkeyModifiers.Win))
            parts.Add("Win");
        if (modifiers.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");

        parts.Add(key.ToString());

        return string.Join("+", parts);
    }

    private void CreateMessageWindow()
    {
        // We need a window to receive WM_HOTKEY messages
        // In WinUI 3, we'll need to handle this differently
        // For now, we'll use a message-only window

        _wndProc = WndProc;

        var wndClass = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = "DictateHotkeyWindow_" + Guid.NewGuid().ToString("N")
        };

        var atom = NativeMethods.RegisterClass(ref wndClass);
        if (atom == 0)
        {
            _logger?.LogError("Failed to register window class");
            return;
        }

        _windowHandle = NativeMethods.CreateWindowEx(
            0, wndClass.lpszClassName, "", 0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_windowHandle == IntPtr.Zero)
        {
            _logger?.LogError("Failed to create message window");
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            lock (_lock)
            {
                if (_registrations.TryGetValue(id, out var registration))
                {
                    HotkeyPressed?.Invoke(this, new HotkeyEventArgs(registration));
                }
            }
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();

        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }
}

/// <summary>
/// Hotkey modifier keys.
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}

/// <summary>
/// Virtual key codes.
/// </summary>
public enum Keys : uint
{
    None = 0,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    D0 = 0x30,
    D1 = 0x31,
    D2 = 0x32,
    D3 = 0x33,
    D4 = 0x34,
    D5 = 0x35,
    D6 = 0x36,
    D7 = 0x37,
    D8 = 0x38,
    D9 = 0x39,
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    Space = 0x20,
    Enter = 0x0D,
    Tab = 0x09,
    Escape = 0x1B,
    Back = 0x08,
    Delete = 0x2E,
    Insert = 0x2D,
    Home = 0x24,
    End = 0x23,
    PageUp = 0x21,
    PageDown = 0x22,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    OemTilde = 0xC0,
    OemPlus = 0xBB,
    OemMinus = 0xBD,
    OemPeriod = 0xBE,
    OemComma = 0xBC
}

/// <summary>
/// Event args for hotkey activation.
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    public int Id { get; }
    public HotkeyModifiers Modifiers { get; }
    public Keys Key { get; }
    public string? Name { get; }

    public HotkeyEventArgs(HotkeyRegistration registration)
    {
        Id = registration.Id;
        Modifiers = registration.Modifiers;
        Key = registration.Key;
        Name = registration.Name;
    }
}

/// <summary>
/// Represents a registered hotkey.
/// </summary>
public class HotkeyRegistration
{
    public int Id { get; set; }
    public HotkeyModifiers Modifiers { get; set; }
    public Keys Key { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Interface for hotkey service.
/// </summary>
public interface IHotkeyService
{
    event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    int RegisterHotkey(HotkeyModifiers modifiers, Keys key, string? name = null);
    int RegisterHotkey(string hotkeyString, string? name = null);
    bool UnregisterHotkey(int id);
    void UnregisterAll();
    bool IsRegistered(HotkeyModifiers modifiers, Keys key);
}
