using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Models;

namespace DictateForWindows.Views;

/// <summary>
/// Window for viewing API usage statistics.
/// </summary>
public sealed partial class UsageWindow : Window
{
    private readonly IUsageRepository _usageRepository;

    public UsageWindow()
    {
        InitializeComponent();

        _usageRepository = App.Current.Services.GetRequiredService<IUsageRepository>();

        this.SetWindowSize(450, 600);
        this.Title = "Usage Statistics";

        LoadUsage();
    }

    private void LoadUsage()
    {
        var usages = _usageRepository.GetAll();
        var totalCost = _usageRepository.GetTotalCost();
        var totalAudioMs = _usageRepository.GetTotalAudioTimeMs();

        TotalCostText.Text = $"${totalCost:F4}";
        TotalAudioText.Text = FormatDuration(totalAudioMs);

        var displayItems = usages.Select(u => new UsageDisplayItem(u)).ToList();
        UsageListView.ItemsSource = displayItems;
    }

    private static string FormatDuration(long milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        return $"{span.Minutes}:{span.Seconds:D2}";
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Reset Statistics",
            Content = "Are you sure you want to reset all usage statistics? This cannot be undone.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _usageRepository.Reset();
            LoadUsage();
        }
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private class UsageDisplayItem
    {
        private readonly UsageModel _model;

        public string ModelName => _model.ModelName;
        public string CostFormatted => $"${_model.TotalCost:F4}";

        public bool HasAudioTime => _model.AudioTimeMs > 0;
        public bool HasTokens => _model.InputTokens > 0 || _model.OutputTokens > 0;

        public string AudioTimeFormatted
        {
            get
            {
                var span = TimeSpan.FromMilliseconds(_model.AudioTimeMs);
                return $"{(int)span.TotalMinutes}:{span.Seconds:D2} audio";
            }
        }

        public string TokensFormatted => $"{_model.InputTokens + _model.OutputTokens:N0} tokens";

        public UsageDisplayItem(UsageModel model)
        {
            _model = model;
        }
    }
}
