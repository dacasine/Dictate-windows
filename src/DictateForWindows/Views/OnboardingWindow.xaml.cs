using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using DictateForWindows.Core.Services.Settings;

namespace DictateForWindows.Views;

/// <summary>
/// Simplified onboarding wizard for first-time setup.
/// </summary>
public sealed partial class OnboardingWindow : Window
{
    private readonly ISettingsService _settings;

    public OnboardingWindow()
    {
        InitializeComponent();

        _settings = App.Current.Services.GetRequiredService<ISettingsService>();

        this.SetWindowSize(600, 450);
        this.Title = "Welcome to Dictate";
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        _settings.FirstRun = false;
        _settings.OnboardingComplete = true;
        _settings.Save();
        Close();
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }
}
