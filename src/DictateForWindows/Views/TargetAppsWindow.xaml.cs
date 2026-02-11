using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.TargetApp;

namespace DictateForWindows.Views;

public sealed partial class TargetAppsWindow : Window
{
    private readonly ITargetAppService _targetAppService;
    private TargetApp? _editingApp;

    public TargetAppsWindow()
    {
        InitializeComponent();

        _targetAppService = App.Current.Services.GetRequiredService<ITargetAppService>();

        SetWindowSize(550, 650);
        this.Title = "Export Targets";

        LoadApps();
    }

    private void LoadApps()
    {
        // Keep the description TextBlock (first child), remove the rest
        while (AppsListPanel.Children.Count > 1)
        {
            AppsListPanel.Children.RemoveAt(AppsListPanel.Children.Count - 1);
        }

        var apps = _targetAppService.GetAllIncludingDisabled();
        foreach (var app in apps)
        {
            AppsListPanel.Children.Add(CreateAppCard(app));
        }
    }

    private Border CreateAppCard(TargetApp app)
    {
        var border = new Border
        {
            Style = (Style)Application.Current.Resources["SettingsCardStyle"],
            Opacity = app.IsEnabled ? 1.0 : 0.5
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Icon
        var icon = new FontIcon
        {
            Glyph = app.IconGlyph,
            FontSize = 20,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);

        // Name + URL
        var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoPanel.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = app.DeepLinkPattern,
            Style = (Style)Application.Current.Resources["SettingsDescriptionStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 300
        });
        Grid.SetColumn(infoPanel, 1);

        // Action buttons
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        var toggleBtn = new ToggleSwitch
        {
            IsOn = app.IsEnabled,
            OnContent = "",
            OffContent = "",
            MinWidth = 0
        };
        var capturedApp = app;
        toggleBtn.Toggled += (s, e) =>
        {
            capturedApp.IsEnabled = toggleBtn.IsOn;
            _targetAppService.Update(capturedApp);
            border.Opacity = capturedApp.IsEnabled ? 1.0 : 0.5;
        };
        buttonsPanel.Children.Add(toggleBtn);

        var editBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE70F", FontSize = 14 },
            Padding = new Thickness(8),
            Tag = app
        };
        editBtn.Click += OnEditAppClick;
        buttonsPanel.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 14 },
            Padding = new Thickness(8),
            Tag = app.Id
        };
        deleteBtn.Click += OnDeleteAppClick;
        buttonsPanel.Children.Add(deleteBtn);

        Grid.SetColumn(buttonsPanel, 2);

        grid.Children.Add(icon);
        grid.Children.Add(infoPanel);
        grid.Children.Add(buttonsPanel);

        border.Child = grid;
        return border;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        _editingApp = null;
        EditDialogTitle.Text = "Add Export Target";
        EditNameBox.Text = "";
        EditUrlBox.Text = "";
        EditIconComboBox.SelectedIndex = 4; // Globe
        EditOverlay.Visibility = Visibility.Visible;
    }

    private void OnEditAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TargetApp app)
        {
            _editingApp = app;
            EditDialogTitle.Text = "Edit Export Target";
            EditNameBox.Text = app.Name;
            EditUrlBox.Text = app.DeepLinkPattern;

            // Try to match the icon
            for (int i = 0; i < EditIconComboBox.Items.Count; i++)
            {
                if (EditIconComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == app.IconGlyph)
                {
                    EditIconComboBox.SelectedIndex = i;
                    break;
                }
            }

            EditOverlay.Visibility = Visibility.Visible;
        }
    }

    private void OnDeleteAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appId)
        {
            _targetAppService.Delete(appId);
            LoadApps();
        }
    }

    private void OnEditCancel(object sender, RoutedEventArgs e)
    {
        EditOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnEditSave(object sender, RoutedEventArgs e)
    {
        var name = EditNameBox.Text.Trim();
        var url = EditUrlBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) return;

        var iconGlyph = "\uE71E";
        if (EditIconComboBox.SelectedItem is ComboBoxItem selectedIcon)
        {
            iconGlyph = selectedIcon.Tag?.ToString() ?? iconGlyph;
        }

        if (_editingApp != null)
        {
            _editingApp.Name = name;
            _editingApp.DeepLinkPattern = url;
            _editingApp.IconGlyph = iconGlyph;
            _targetAppService.Update(_editingApp);
        }
        else
        {
            var newApp = new TargetApp
            {
                Name = name,
                DeepLinkPattern = url,
                IconGlyph = iconGlyph,
                IsEnabled = true
            };
            _targetAppService.Add(newApp);
        }

        EditOverlay.Visibility = Visibility.Collapsed;
        LoadApps();
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }
}
