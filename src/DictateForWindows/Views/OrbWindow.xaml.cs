using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using DictateForWindows.Controls;
using DictateForWindows.Core.Services.TextInjection;
using DictateForWindows.Helpers;
using DictateForWindows.ViewModels;
using Windows.Foundation;
using WinUIEx;

namespace DictateForWindows.Views;

/// <summary>
/// Transparent, borderless window hosting the Orb dictation UI.
/// </summary>
public sealed partial class OrbWindow : Window
{
    public OrbViewModel ViewModel { get; }
    private readonly ITextInjector _textInjector;

    public bool IsVisible { get; private set; }

    // Drag tracking
    private Point? _dragStart;
    private const double DragThreshold = 40;

    public OrbWindow()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<OrbViewModel>();
        _textInjector = App.Current.Services.GetRequiredService<ITextInjector>();
        RootGrid.DataContext = ViewModel;

        ConfigureWindow();

        // Keyboard
        RootGrid.KeyDown += OnKeyDown;

        // Focus loss
        Activated += OnActivated;

        // ViewModel events
        ViewModel.RequestHide += OnRequestHide;
        ViewModel.RequestClose += OnRequestClose;
        ViewModel.RequestImplosion += OnRequestImplosion;
        ViewModel.RequestDissolve += OnRequestDissolve;
        ViewModel.RequestScreenshotCapture += OnRequestScreenshotCapture;
    }

    private void ConfigureWindow()
    {
        // Borderless, always on top
        this.SetIsAlwaysOnTop(true);
        this.SetIsResizable(false);
        this.SetIsMinimizable(false);
        this.SetIsMaximizable(false);

        // Window size
        this.SetWindowSize(600, 600);

        // Remove title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);

        // True transparent backdrop via WinUIEx
        SystemBackdrop = new WinUIEx.TransparentTintBackdrop();

        // Make window a toolwindow (no taskbar entry)
        MakeWindowToolWindow();
    }

    private void MakeWindowToolWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Set toolwindow style so window doesn't appear in taskbar
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// Show the orb centered on the current cursor position.
    /// </summary>
    public async Task ShowAtCursorAsync()
    {
        // Save target window BEFORE we take focus
        _textInjector.SaveTargetWindow();

        // Capture selection from target window
        var selection = await CaptureSelectionFromTargetAsync();
        ViewModel.SetSelectedContext(selection);

        // Position: center the 600x600 window on the cursor
        var cursorPos = GetCursorPosition();
        var screenBounds = GetScreenBounds();

        int x = cursorPos.X - 300;
        int y = cursorPos.Y - 300;

        // Keep within screen bounds
        if (x < screenBounds.Left) x = screenBounds.Left;
        if (y < screenBounds.Top) y = screenBounds.Top;
        if (x + 600 > screenBounds.Right) x = screenBounds.Right - 600;
        if (y + 600 > screenBounds.Bottom) y = screenBounds.Bottom - 600;

        this.Move(x, y);
        Activate();
        IsVisible = true;

        // Play appear animation
        OrbElement.PlayAppear();

        // Populate arcs
        PopulatePromptArc();
        PopulateModelArc();
        PopulateTargetApps();

        // Auto-start recording
        ViewModel.StartRecording();
    }

    /// <summary>
    /// Dismiss the orb with dissolve animation.
    /// </summary>
    public void Dismiss()
    {
        ViewModel.CancelWithDissolve();
    }

    #region Keyboard Handlers

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Up:
                ViewModel.NavigateUp();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Down:
                ViewModel.NavigateDown();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Left:
                ViewModel.NavigateLeft();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Right:
                ViewModel.NavigateRight();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Enter:
                ViewModel.Confirm();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                ViewModel.CancelWithDissolve();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Space:
                if (ViewModel.IsRecording || ViewModel.IsPaused)
                {
                    ViewModel.TogglePause();
                }
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Mouse/Pointer Handlers

    private void OnOrbPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid);
        _dragStart = point.Position;
        (sender as UIElement)?.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnOrbPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragStart == null) return;
        e.Handled = true;
    }

    private void OnOrbPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragStart == null) return;

        var point = e.GetCurrentPoint(RootGrid);
        var deltaX = point.Position.X - _dragStart.Value.X;
        var deltaY = point.Position.Y - _dragStart.Value.Y;
        _dragStart = null;
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);

        var absDx = Math.Abs(deltaX);
        var absDy = Math.Abs(deltaY);

        if (absDx < DragThreshold && absDy < DragThreshold)
        {
            // Tap/click on orb = confirm
            ViewModel.Confirm();
        }
        else if (absDy > absDx)
        {
            // Vertical dominant
            if (deltaY < -DragThreshold)
            {
                ViewModel.NavigateUp();
            }
            else if (deltaY > DragThreshold)
            {
                ViewModel.NavigateDown();
            }
        }
        else
        {
            // Horizontal dominant
            if (deltaX < -DragThreshold)
            {
                ViewModel.NavigateLeft();
            }
            else if (deltaX > DragThreshold)
            {
                ViewModel.NavigateRight();
            }
        }

        e.Handled = true;
    }

    #endregion

    #region Prompt Arc

    private void PopulatePromptArc()
    {
        PromptArcCanvas.Children.Clear();

        var prompts = ViewModel.Prompts;
        if (prompts.Count == 0) return;

        // Arc layout: radius 180px from center, -60° to +60°
        double centerX = 300;
        double centerY = 300;
        double radius = 180;
        double startAngle = -60;
        double endAngle = 60;
        double step = prompts.Count > 1 ? (endAngle - startAngle) / (prompts.Count - 1) : 0;

        for (int i = 0; i < prompts.Count; i++)
        {
            var promptVm = prompts[i];
            double angle = prompts.Count == 1 ? 0 : startAngle + i * step;
            double radians = (angle - 90) * Math.PI / 180; // -90 to make 0° = top

            double x = centerX + radius * Math.Cos(radians);
            double y = centerY + radius * Math.Sin(radians);

            var lens = new PromptLens
            {
                PromptName = promptVm.Name,
                Tooltip = promptVm.Tooltip,
                CommandParameter = promptVm,
                Command = ViewModel.SelectPromptCommand,
                IsSelected = ViewModel.ActiveFilter == promptVm
            };

            // Center the lens on the calculated position
            lens.Loaded += (s, e) =>
            {
                var el = (PromptLens)s!;
                Canvas.SetLeft(el, x - el.ActualWidth / 2);
                Canvas.SetTop(el, y - el.ActualHeight / 2);
            };

            // Fallback position before Loaded fires
            Canvas.SetLeft(lens, x - 30);
            Canvas.SetTop(lens, y - 16);

            PromptArcCanvas.Children.Add(lens);
        }
    }

    /// <summary>
    /// Animate the prompt arc elements in with staggered appear.
    /// </summary>
    private void AnimatePromptArcIn()
    {
        var elements = new List<UIElement>();
        foreach (var child in PromptArcCanvas.Children)
        {
            elements.Add(child);
        }
        CompositionHelper.PlayStaggeredAppear(elements);
    }

    #endregion

    #region Model Arc

    private void PopulateModelArc()
    {
        ModelArcCanvas.Children.Clear();

        var models = ViewModel.AvailableModels;
        if (models.Count == 0) return;

        // Arc layout: radius 200px from center, -50° to +50° (above the orb)
        double centerX = 300;
        double centerY = 300;
        double radius = 200;
        double startAngle = -50;
        double endAngle = 50;
        double step = models.Count > 1 ? (endAngle - startAngle) / (models.Count - 1) : 0;

        for (int i = 0; i < models.Count; i++)
        {
            var modelVm = models[i];
            double angle = models.Count == 1 ? 0 : startAngle + i * step;
            double radians = (angle - 90) * Math.PI / 180;

            double x = centerX + radius * Math.Cos(radians);
            double y = centerY + radius * Math.Sin(radians);

            var lens = new ModelLens
            {
                ModelName = modelVm.DisplayName,
                IsSubtle = !modelVm.IsSelected,
                IsSelected = modelVm.IsSelected,
                CommandParameter = modelVm,
                Command = ViewModel.SelectModelCommand
            };

            lens.Loaded += (s, e) =>
            {
                var el = (ModelLens)s!;
                Canvas.SetLeft(el, x - el.ActualWidth / 2);
                Canvas.SetTop(el, y - el.ActualHeight / 2);
            };

            Canvas.SetLeft(lens, x - 25);
            Canvas.SetTop(lens, y - 14);

            ModelArcCanvas.Children.Add(lens);
        }
    }

    #endregion

    #region Target Apps

    private void PopulateTargetApps()
    {
        TargetAppsPanel.Children.Clear();

        var apps = ViewModel.TargetApps;
        if (apps.Count == 0) return;

        foreach (var app in apps)
        {
            var card = new TargetAppCard
            {
                AppName = app.Name,
                IconGlyph = app.IconGlyph,
                CommandParameter = app,
                Command = ViewModel.SelectTargetAppCommand,
                IsSelected = ViewModel.SelectedTargetApp?.Id == app.Id
            };

            TargetAppsPanel.Children.Add(card);
        }
    }

    #endregion

    #region Screenshot Capture

    private async void OnRequestScreenshotCapture(object? sender, EventArgs e)
    {
        await CaptureScreenshotForContextAsync();
    }

    private async Task CaptureScreenshotForContextAsync()
    {
        // Hide orb temporarily for clean screenshot
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, 0); // SW_HIDE

        await Task.Delay(150); // Wait for window to fully hide

        // Capture screenshot + OCR
        await ViewModel.CaptureScreenshotAsync();

        // Re-show orb
        NativeMethods.ShowWindow(hwnd, 5); // SW_SHOW
        Activate();
    }

    #endregion

    #region Selection Capture

    private async Task<string?> CaptureSelectionFromTargetAsync()
    {
        try
        {
            // Save current clipboard
            var dataPackage = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            string? originalText = null;
            if (dataPackage.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                originalText = await dataPackage.GetTextAsync();
            }

            // Send Ctrl+C to the target window
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
            NativeMethods.keybd_event(NativeMethods.VK_C, 0, 0, 0);
            NativeMethods.keybd_event(NativeMethods.VK_C, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);

            await Task.Delay(100);

            // Read clipboard
            var newData = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (newData.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                var text = await newData.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text) && text != originalText)
                {
                    // Restore original clipboard
                    if (originalText != null)
                    {
                        var restorePackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                        restorePackage.SetText(originalText);
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(restorePackage);
                    }
                    return text;
                }
            }
        }
        catch
        {
            // Clipboard access can fail, that's OK
        }

        return null;
    }

    #endregion

    #region ViewModel Event Handlers

    private void OnRequestHide(object? sender, EventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, 0); // SW_HIDE
        IsVisible = false;
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        IsVisible = false;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, 0); // SW_HIDE
    }

    private void OnRequestImplosion(object? sender, EventArgs e)
    {
        OrbElement.PlayImplosion();
    }

    private void OnRequestDissolve(object? sender, EventArgs e)
    {
        OrbElement.PlayDissolve(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                IsVisible = false;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                NativeMethods.ShowWindow(hwnd, 0);
            });
        });
    }

    #endregion

    #region Window Events

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (!ViewModel.IsRecording && !ViewModel.IsProcessing && !ViewModel.IsPaused)
            {
                Dismiss();
            }
        }
    }

    #endregion

    #region P/Invoke

    private static NativeMethods.POINT GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return point;
    }

    private static NativeMethods.RECT GetScreenBounds()
    {
        var hwnd = NativeMethods.GetDesktopWindow();
        NativeMethods.GetWindowRect(hwnd, out var rect);
        return rect;
    }

    private static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const uint LWA_ALPHA = 0x02;
        public const byte VK_CONTROL = 0x11;
        public const byte VK_C = 0x43;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

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

    #endregion
}
