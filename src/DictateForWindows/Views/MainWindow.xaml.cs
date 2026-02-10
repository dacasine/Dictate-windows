using Microsoft.UI.Xaml;
using WinUIEx;

namespace DictateForWindows.Views;

/// <summary>
/// Hidden main window that hosts the application.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Defer hiding to after the window is created
        this.Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Only run once
        this.Activated -= OnActivated;

        // Hide window from taskbar and make it invisible
        try
        {
            this.SetIsShownInSwitchers(false);
            this.SetIsAlwaysOnTop(false);
        }
        catch
        {
            // Ignore WinUIEx errors
        }

        // Minimize to tray
        Hide();
    }

    public new void Hide()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            PInvoke.User32.ShowWindow(hwnd, PInvoke.User32.WindowShowStyle.SW_HIDE);
        }
        catch
        {
            // Ignore if window handle not available
        }
    }
}
