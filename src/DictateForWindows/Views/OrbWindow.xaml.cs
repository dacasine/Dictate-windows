using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using DictateForWindows.Controls;
using DictateForWindows.Core.Services.TextInjection;
using DictateForWindows.Helpers;
using DictateForWindows.ViewModels;
using Windows.Foundation;
using WinUIEx;

namespace DictateForWindows.Views;

/// <summary>
/// Full-screen transparent overlay window hosting the Orb dictation UI.
/// Never steals focus from the target application. Clicks on transparent
/// areas pass through to the windows behind.
/// </summary>
public sealed partial class OrbWindow : Window
{
    public OrbViewModel ViewModel { get; }
    private readonly ITextInjector _textInjector;

    public bool IsVisible { get; private set; }

    // Drag tracking
    private Point? _dragStart;
    private const double DragThreshold = 40;

    // Overlay infrastructure
    private NativeMethods.SUBCLASSPROC? _subclassProc;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _keyboardHookId;
    private volatile List<Rect> _hitTestRects = new();
    private double _dpiScale = 1.0;

    public OrbWindow()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<OrbViewModel>();
        _textInjector = App.Current.Services.GetRequiredService<ITextInjector>();
        RootGrid.DataContext = ViewModel;

        ConfigureWindow();

        // Pointer events on the orb (works without focus)
        OrbElement.PointerPressed += OnOrbPointerPressed;
        OrbElement.PointerMoved += OnOrbPointerMoved;
        OrbElement.PointerReleased += OnOrbPointerReleased;

        // ViewModel events
        ViewModel.RequestHide += OnRequestHide;
        ViewModel.RequestClose += OnRequestClose;
        ViewModel.RequestImplosion += OnRequestImplosion;
        ViewModel.RequestDissolve += OnRequestDissolve;
        ViewModel.RequestScreenshotCapture += OnRequestScreenshotCapture;
        ViewModel.RequestOpenSettings += OnRequestOpenSettings;

        // Sync panel visibility and update hit-test rects
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.ShowDirectionalHints):
                    DirectionalHints.Visibility = ViewModel.ShowDirectionalHints
                        ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ViewModel.ShowPromptsGrid):
                    PromptsGrid.Visibility = ViewModel.ShowPromptsGrid
                        ? Visibility.Visible : Visibility.Collapsed;
                    ScheduleHitTestUpdate();
                    break;
                case nameof(ViewModel.ShowContextCards):
                    ContextCardsPanel.Visibility = ViewModel.ShowContextCards
                        ? Visibility.Visible : Visibility.Collapsed;
                    ScheduleHitTestUpdate();
                    break;
                case nameof(ViewModel.ShowTargetApps):
                    if (ViewModel.ShowTargetApps)
                    {
                        PopulateTargetApps();
                        TargetAppsGrid.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TargetAppsGrid.Visibility = Visibility.Collapsed;
                    }
                    ScheduleHitTestUpdate();
                    break;
                case nameof(ViewModel.ActiveTargetAppIndex):
                    UpdateTargetAppHighlight();
                    break;
                case nameof(ViewModel.StatusText):
                    OrbElement.Status = ViewModel.StatusText;
                    break;
                case nameof(ViewModel.TimerText):
                    OrbElement.Timer = ViewModel.TimerText;
                    break;
                case nameof(ViewModel.AudioLevel):
                    OrbElement.AudioLevel = ViewModel.AudioLevel;
                    break;
                case nameof(ViewModel.IsRecording):
                    OrbElement.IsRecording = ViewModel.IsRecording;
                    break;
                case nameof(ViewModel.IsPaused):
                    OrbElement.IsPaused = ViewModel.IsPaused;
                    break;
                case nameof(ViewModel.IsProcessing):
                    OrbElement.IsProcessing = ViewModel.IsProcessing;
                    break;
                case nameof(ViewModel.OrbAccentColor):
                    OrbElement.AccentColor = ViewModel.OrbAccentColor;
                    break;
                case nameof(ViewModel.ActivePromptName):
                    // Handled by x:Bind on DirectionalHints.PromptLabel
                    break;
                case nameof(ViewModel.ActivePromptIndex):
                    UpdatePromptHighlight();
                    break;
            }
        });
    }

    private void ConfigureWindow()
    {
        // Borderless, always on top
        this.SetIsAlwaysOnTop(true);
        this.SetIsResizable(false);
        this.SetIsMinimizable(false);
        this.SetIsMaximizable(false);

        // Remove title bar
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
        }
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);

        // Transparent backdrop
        SystemBackdrop = new TransparentTintBackdrop();

        // Win32 overlay styles + subclass
        ConfigureOverlayStyles();
    }

    /// <summary>
    /// Apply Win32 styles for a true overlay: borderless, no taskbar entry,
    /// never steals focus, hit-test transparent on empty areas.
    /// </summary>
    private void ConfigureOverlayStyles()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Strip caption and frame
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
        style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME |
                    NativeMethods.WS_SYSMENU | NativeMethods.WS_MINIMIZEBOX |
                    NativeMethods.WS_MAXIMIZEBOX);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);

        // Tool window (no taskbar) + no-activate (never steals focus)
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        // Recalculate frame
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

        // Subclass for WM_NCHITTEST hit-test transparency
        _subclassProc = SubclassProc;
        NativeMethods.SetWindowSubclass(hwnd, _subclassProc, NativeMethods.SUBCLASS_ID, 0);
    }

    #region Hit-Test Transparency

    /// <summary>
    /// Window subclass procedure. Intercepts WM_NCHITTEST to return HTTRANSPARENT
    /// for areas outside interactive UI elements, allowing clicks to pass through.
    /// </summary>
    private IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        IntPtr uIdSubclass, uint dwRefData)
    {
        if (uMsg == NativeMethods.WM_NCHITTEST && IsVisible)
        {
            // Extract screen coordinates from lParam
            int screenX = (short)(lParam.ToInt64() & 0xFFFF);
            int screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            var pt = new NativeMethods.POINT { X = screenX, Y = screenY };
            NativeMethods.ScreenToClient(hWnd, ref pt);

            // Convert physical pixels to DIPs (XAML coordinate space)
            double dipX = pt.X / _dpiScale;
            double dipY = pt.Y / _dpiScale;
            var dipPoint = new Point(dipX, dipY);

            // Check cached interactive zones
            var rects = _hitTestRects;
            foreach (var rect in rects)
            {
                if (rect.Contains(dipPoint))
                {
                    return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
                }
            }

            // Nothing interactive here — pass click through
            return (IntPtr)NativeMethods.HTTRANSPARENT;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Schedule a hit-test rect update after layout has settled.
    /// </summary>
    private async void ScheduleHitTestUpdate()
    {
        await Task.Delay(60);
        UpdateHitTestRects();
    }

    /// <summary>
    /// Rebuild the list of interactive rectangles in DIP coordinates.
    /// </summary>
    private void UpdateHitTestRects()
    {
        if (Content?.XamlRoot != null)
            _dpiScale = Content.XamlRoot.RasterizationScale;

        var rects = new List<Rect>();

        // The orb is always interactive
        AddElementRect(OrbElement, rects, padding: 20);

        // Prompts grid
        if (PromptsGrid.Visibility == Visibility.Visible)
            AddElementRect(PromptsGrid, rects, padding: 8);

        // Context cards
        if (ContextCardsPanel.Visibility == Visibility.Visible)
            AddElementRect(ContextCardsPanel, rects, padding: 8);

        // Target apps
        if (TargetAppsGrid.Visibility == Visibility.Visible)
            AddElementRect(TargetAppsGrid, rects, padding: 8);

        _hitTestRects = rects;
    }

    private void AddElementRect(FrameworkElement element, List<Rect> rects, double padding = 0)
    {
        try
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return;

            var transform = element.TransformToVisual(Content as UIElement);
            var topLeft = transform.TransformPoint(new Point(0, 0));
            rects.Add(new Rect(
                topLeft.X - padding,
                topLeft.Y - padding,
                element.ActualWidth + padding * 2,
                element.ActualHeight + padding * 2));
        }
        catch
        {
            // Element might not be in the visual tree yet
        }
    }

    #endregion

    #region Global Keyboard Hook

    private void InstallKeyboardHook()
    {
        if (_keyboardHookId != IntPtr.Zero) return;

        _keyboardProc = KeyboardHookCallback;
        _keyboardHookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _keyboardProc,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    private void UninstallKeyboardHook()
    {
        if (_keyboardHookId == IntPtr.Zero) return;

        NativeMethods.UnhookWindowsHookEx(_keyboardHookId);
        _keyboardHookId = IntPtr.Zero;
        _keyboardProc = null;
    }

    /// <summary>
    /// Low-level keyboard hook. Captures control keys when the orb is visible
    /// and forwards them to the ViewModel. Other keys pass through to the target app.
    /// </summary>
    private static readonly string HookLogPath = Path.Combine(Path.GetTempPath(), "DictateForWindows", "dictate.log");
    private static void HookLog(string msg)
    {
        try { File.AppendAllText(HookLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [Hook] {msg}\n"); } catch { }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsVisible && wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            HookLog($"KeyDown vk=0x{vkCode:X2} phase={ViewModel.CurrentPhase} isRec={ViewModel.IsRecording} isPaused={ViewModel.IsPaused}");

            switch (vkCode)
            {
                case NativeMethods.VK_ESCAPE:
                    DispatcherQueue.TryEnqueue(() => ViewModel.HandleEscape());
                    return (IntPtr)1; // consumed

                case NativeMethods.VK_RETURN:
                    DispatcherQueue.TryEnqueue(() => ViewModel.Confirm());
                    return (IntPtr)1;

                case NativeMethods.VK_UP:
                    DispatcherQueue.TryEnqueue(() => ViewModel.NavigateUp());
                    return (IntPtr)1;

                case NativeMethods.VK_DOWN:
                    DispatcherQueue.TryEnqueue(() => ViewModel.NavigateDown());
                    return (IntPtr)1;

                case NativeMethods.VK_LEFT:
                    DispatcherQueue.TryEnqueue(() => ViewModel.NavigateLeft());
                    return (IntPtr)1;

                case NativeMethods.VK_RIGHT:
                    DispatcherQueue.TryEnqueue(() => ViewModel.NavigateRight());
                    return (IntPtr)1;

                case NativeMethods.VK_SPACE:
                    if (ViewModel.IsRecording || ViewModel.IsPaused)
                    {
                        DispatcherQueue.TryEnqueue(() => ViewModel.TogglePause());
                        return (IntPtr)1;
                    }
                    break;
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    #endregion

    #region Show / Hide

    /// <summary>
    /// Show the orb as a full-screen transparent overlay on the current monitor.
    /// Does not steal focus from the target application.
    /// </summary>
    public async Task ShowAtCursorAsync()
    {
        // Save target window BEFORE any focus change
        _textInjector.SaveTargetWindow();

        // START RECORDING IMMEDIATELY — mic activation is the critical path
        ViewModel.StartRecording();

        // Capture selection from target window IN PARALLEL with mic startup
        var selectionTask = CaptureSelectionFromTargetAsync();

        // While clipboard capture runs, get monitor bounds (instant P/Invoke)
        var monitorBounds = GetCurrentMonitorBounds();
        int screenW = monitorBounds.Right - monitorBounds.Left;
        int screenH = monitorBounds.Bottom - monitorBounds.Top;

        // Await clipboard result BEFORE showing the window
        var selection = await selectionTask;

        // If no clipboard text, capture screenshot NOW (window still hidden — no flicker)
        if (string.IsNullOrWhiteSpace(selection))
        {
            await ViewModel.CaptureScreenshotAsync();
        }

        // Set context (screenshot already captured, won't trigger hide/show)
        ViewModel.SetSelectedContext(selection);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Position and size to cover the full monitor, topmost, without activating
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            monitorBounds.Left, monitorBounds.Top, screenW, screenH,
            NativeMethods.SWP_NOACTIVATE);

        // Show without stealing focus
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        IsVisible = true;

        // Cache DPI scale
        if (Content?.XamlRoot != null)
            _dpiScale = Content.XamlRoot.RasterizationScale;

        // Install keyboard hook for Esc/Enter/Arrows/Space
        InstallKeyboardHook();

        // Populate prompts grid (always visible during recording)
        PopulatePromptsGrid();

        // Play appear animation
        OrbElement.PlayAppear();

        // Update hit-test rects after layout settles
        _ = Task.Delay(80).ContinueWith(_ =>
            DispatcherQueue.TryEnqueue(UpdateHitTestRects),
            TaskScheduler.Default);
    }

    /// <summary>
    /// Dismiss the orb with dissolve animation.
    /// </summary>
    public void Dismiss()
    {
        ViewModel.CancelWithDissolve();
    }

    private void HideOverlay()
    {
        UninstallKeyboardHook();
        IsVisible = false;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    #endregion

    #region Pointer Handlers

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
            ViewModel.Confirm();
        }
        else if (absDy > absDx)
        {
            if (deltaY < -DragThreshold)
                ViewModel.NavigateUp();
            else if (deltaY > DragThreshold)
                ViewModel.NavigateDown();
        }
        else
        {
            if (deltaX < -DragThreshold)
                ViewModel.NavigateLeft();
            else if (deltaX > DragThreshold)
                ViewModel.NavigateRight();
        }

        e.Handled = true;
    }

    #endregion

    #region Prompts Grid

    private void PopulatePromptsGrid()
    {
        PromptsGrid.Children.Clear();
        PromptsGrid.RowDefinitions.Clear();

        var prompts = ViewModel.Prompts;
        if (prompts.Count == 0) return;

        int rowCount = (prompts.Count + 1) / 2;
        for (int r = 0; r < rowCount; r++)
        {
            PromptsGrid.RowDefinitions.Add(new Microsoft.UI.Xaml.Controls.RowDefinition
            {
                Height = Microsoft.UI.Xaml.GridLength.Auto
            });
        }

        for (int i = 0; i < prompts.Count; i++)
        {
            var promptVm = prompts[i];
            var lens = new PromptLens
            {
                PromptName = promptVm.Name,
                Tooltip = promptVm.Tooltip,
                CommandParameter = promptVm,
                Command = ViewModel.SelectPromptCommand,
                IsSelected = promptVm.IsActive
            };

            Microsoft.UI.Xaml.Controls.Grid.SetRow(lens, i / 2);
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(lens, i % 2);
            PromptsGrid.Children.Add(lens);
        }
    }

    private void UpdatePromptHighlight()
    {
        for (int i = 0; i < PromptsGrid.Children.Count; i++)
        {
            if (PromptsGrid.Children[i] is PromptLens lens)
            {
                lens.IsSelected = i == ViewModel.ActivePromptIndex;
            }
        }
    }

    #endregion

    #region Target Apps

    private void PopulateTargetApps()
    {
        TargetAppsGrid.Children.Clear();
        TargetAppsGrid.RowDefinitions.Clear();

        var apps = ViewModel.TargetApps;
        if (apps.Count == 0) return;

        int rowCount = (apps.Count + 1) / 2; // ceiling division
        for (int r = 0; r < rowCount; r++)
        {
            TargetAppsGrid.RowDefinitions.Add(new Microsoft.UI.Xaml.Controls.RowDefinition { Height = Microsoft.UI.Xaml.GridLength.Auto });
        }

        for (int i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            var card = new TargetAppCard
            {
                AppName = app.Name,
                IconGlyph = app.IconGlyph,
                CommandParameter = app,
                Command = ViewModel.SelectTargetAppCommand,
                IsSelected = i == ViewModel.ActiveTargetAppIndex ||
                             ViewModel.SelectedTargetApp?.Id == app.Id
            };

            Microsoft.UI.Xaml.Controls.Grid.SetRow(card, i / 2);
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(card, i % 2);
            TargetAppsGrid.Children.Add(card);
        }
    }

    private void UpdateTargetAppHighlight()
    {
        for (int i = 0; i < TargetAppsGrid.Children.Count; i++)
        {
            if (TargetAppsGrid.Children[i] is TargetAppCard card)
            {
                card.IsSelected = i == ViewModel.ActiveTargetAppIndex;
            }
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
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);

        await Task.Delay(150);

        await ViewModel.CaptureScreenshotAsync();

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
    }

    #endregion

    #region Selection Capture

    private async Task<string?> CaptureSelectionFromTargetAsync()
    {
        try
        {
            var dataPackage = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            string? originalText = null;
            if (dataPackage.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                originalText = await dataPackage.GetTextAsync();
            }

            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
            NativeMethods.keybd_event(NativeMethods.VK_C_KEY, 0, 0, 0);
            NativeMethods.keybd_event(NativeMethods.VK_C_KEY, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);

            await Task.Delay(100);

            var newData = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (newData.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                var text = await newData.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text) && text != originalText)
                {
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
            // Clipboard access can fail
        }

        return null;
    }

    #endregion

    #region ViewModel Event Handlers

    private void OnRequestHide(object? sender, EventArgs e)
    {
        HideOverlay();
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        HideOverlay();
    }

    private void OnRequestImplosion(object? sender, EventArgs e)
    {
        OrbElement.PlayImplosion();
    }

    private void OnRequestDissolve(object? sender, EventArgs e)
    {
        OrbElement.PlayDissolve(() =>
        {
            DispatcherQueue.TryEnqueue(() => HideOverlay());
        });
    }

    private void OnRequestOpenSettings(object? sender, EventArgs e)
    {
        HideOverlay();
        ViewModel.CancelRecording();
        App.Current.ShowSettings();
    }

    #endregion

    #region Monitor Detection

    /// <summary>
    /// Get the bounds of the monitor where the cursor currently is.
    /// </summary>
    private static NativeMethods.RECT GetCurrentMonitorBounds()
    {
        NativeMethods.GetCursorPos(out var cursorPos);
        var hMonitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            return monitorInfo.rcMonitor;
        }

        // Fallback: primary monitor via desktop window
        var hwnd = NativeMethods.GetDesktopWindow();
        NativeMethods.GetWindowRect(hwnd, out var rect);
        return rect;
    }

    #endregion

    #region P/Invoke

    internal static class NativeMethods
    {
        // Window styles
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_MAXIMIZEBOX = 0x00010000;

        // Extended styles
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        // Hit-test
        public const uint WM_NCHITTEST = 0x0084;
        public const int HTTRANSPARENT = -1;

        // ShowWindow commands
        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;

        // SetWindowPos
        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;

        // Keyboard
        public const byte VK_CONTROL = 0x11;
        public const byte VK_C_KEY = 0x43;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_RETURN = 0x0D;
        public const int VK_SPACE = 0x20;
        public const int VK_LEFT = 0x25;
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;

        // Keyboard hook
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;

        // Subclass
        public const uint SUBCLASS_ID = 1;

        // Monitor
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // Delegates
        public delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, uint dwRefData);

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // user32.dll
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
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // comctl32.dll (subclassing)
        [DllImport("comctl32.dll")]
        public static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            uint uIdSubclass, uint dwRefData);

        [DllImport("comctl32.dll")]
        public static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            uint uIdSubclass);

        [DllImport("comctl32.dll")]
        public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        // kernel32.dll
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Structs
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
    }

    #endregion
}
