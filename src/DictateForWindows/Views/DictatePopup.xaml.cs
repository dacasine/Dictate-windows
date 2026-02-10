using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using DictateForWindows.Core.Services.TextInjection;
using DictateForWindows.ViewModels;
using WinUIEx;

namespace DictateForWindows.Views;

/// <summary>
/// Popup window for dictation interface.
/// </summary>
public sealed partial class DictatePopup : Window
{
    public DictatePopupViewModel ViewModel { get; }
    private readonly ITextInjector _textInjector;

    public bool IsVisible { get; private set; }

    public DictatePopup()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<DictatePopupViewModel>();
        _textInjector = App.Current.Services.GetRequiredService<ITextInjector>();
        RootGrid.DataContext = ViewModel;

        // Configure window style
        ConfigureWindow();

        // Handle keyboard shortcuts
        RootGrid.KeyDown += OnKeyDown;

        // Handle focus loss
        Activated += OnActivated;

        // Hide popup when text is ready to inject
        ViewModel.RequestHide += OnRequestHide;
    }

    private void OnRequestHide(object? sender, EventArgs e)
    {
        // Hide the popup window so focus returns to target app
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, 0); // SW_HIDE
        IsVisible = false;
    }

    private void ConfigureWindow()
    {
        // Make the window borderless and always on top
        this.SetIsAlwaysOnTop(true);
        this.SetIsResizable(false);
        this.SetIsMinimizable(false);
        this.SetIsMaximizable(false);

        // Set window size
        this.SetWindowSize(400, 200);

        // Remove title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);
    }

    /// <summary>
    /// Show the popup at the current cursor position.
    /// </summary>
    public void ShowAtCursor()
    {
        // Save the current foreground window BEFORE we take focus
        _textInjector.SaveTargetWindow();

        var cursorPos = GetCursorPosition();
        var screenBounds = GetScreenBounds();

        // Calculate position (offset from cursor)
        int x = cursorPos.X + 10;
        int y = cursorPos.Y + 10;

        // Ensure popup stays within screen bounds
        var windowWidth = 400;
        var windowHeight = 200;

        if (x + windowWidth > screenBounds.Right)
        {
            x = screenBounds.Right - windowWidth - 10;
        }

        if (y + windowHeight > screenBounds.Bottom)
        {
            y = cursorPos.Y - windowHeight - 10;
        }

        // Move window
        this.Move(x, y);

        // Activate and show
        Activate();
        IsVisible = true;

        // Always start recording immediately when popup opens
        ViewModel.StartRecording();
    }

    /// <summary>
    /// Hide the popup.
    /// </summary>
    public new void Hide()
    {
        IsVisible = false;

        // Cancel any ongoing recording
        if (ViewModel.IsRecording)
        {
            ViewModel.CancelRecording();
        }

        // Hide window
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, 0); // SW_HIDE
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // Close popup when it loses focus (unless recording)
            if (!ViewModel.IsRecording && !ViewModel.IsProcessing)
            {
                Hide();
            }
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                // Cancel or close
                if (ViewModel.IsRecording)
                {
                    ViewModel.CancelRecording();
                }
                else
                {
                    Hide();
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Enter:
            case Windows.System.VirtualKey.Space:
                // Toggle recording
                ViewModel.ToggleRecording();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.P:
                // Pause/Resume
                if (ViewModel.IsRecording)
                {
                    ViewModel.TogglePause();
                }
                e.Handled = true;
                break;
        }
    }

    private static POINT GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return point;
    }

    private static RECT GetScreenBounds()
    {
        var hwnd = NativeMethods.GetDesktopWindow();
        NativeMethods.GetWindowRect(hwnd, out var rect);
        return rect;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
