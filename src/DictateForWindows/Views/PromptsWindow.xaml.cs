using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Models;
using Windows.Storage.Pickers;

namespace DictateForWindows.Views;

/// <summary>
/// Window for managing custom prompts.
/// </summary>
public sealed partial class PromptsWindow : Window
{
    private readonly IPromptsRepository _promptsRepository;

    public PromptsWindow()
    {
        InitializeComponent();

        _promptsRepository = App.Current.Services.GetRequiredService<IPromptsRepository>();

        this.SetWindowSize(500, 600);
        this.Title = "Manage Prompts";

        LoadPrompts();
    }

    private void LoadPrompts()
    {
        var prompts = _promptsRepository.GetAll();
        PromptsListView.ItemsSource = prompts;
    }

    private async void OnAddPromptClick(object sender, RoutedEventArgs e)
    {
        await ShowPromptEditorAsync(null);
    }

    private async void OnEditPromptClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int id)
        {
            var prompt = _promptsRepository.Get(id);
            if (prompt != null)
            {
                await ShowPromptEditorAsync(prompt);
            }
        }
    }

    private async void OnDeletePromptClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int id)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Delete Prompt",
                Content = "Are you sure you want to delete this prompt?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _promptsRepository.Delete(id);
                LoadPrompts();
            }
        }
    }

    private async Task ShowPromptEditorAsync(PromptModel? prompt)
    {
        var isNew = prompt == null;
        prompt ??= new PromptModel { Position = _promptsRepository.Count() };

        var nameBox = new TextBox
        {
            Header = "Name",
            Text = prompt.Name,
            PlaceholderText = "Enter prompt name",
            Margin = new Thickness(0, 0, 0, 12)
        };

        var promptBox = new TextBox
        {
            Header = "Prompt",
            Text = prompt.Prompt,
            PlaceholderText = "Enter the prompt instruction",
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Height = 120,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var requiresSelectionToggle = new ToggleSwitch
        {
            Header = "Requires Selection",
            IsOn = prompt.RequiresSelection,
            OffContent = "No",
            OnContent = "Yes",
            Margin = new Thickness(0, 0, 0, 12)
        };

        var autoApplyToggle = new ToggleSwitch
        {
            Header = "Auto-Apply",
            IsOn = prompt.AutoApply,
            OffContent = "No",
            OnContent = "Yes"
        };

        var panel = new StackPanel { Width = 400 };
        panel.Children.Add(nameBox);
        panel.Children.Add(promptBox);
        panel.Children.Add(requiresSelectionToggle);
        panel.Children.Add(autoApplyToggle);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = isNew ? "Add Prompt" : "Edit Prompt",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            prompt.Name = nameBox.Text;
            prompt.Prompt = promptBox.Text;
            prompt.RequiresSelection = requiresSelectionToggle.IsOn;
            prompt.AutoApply = autoApplyToggle.IsOn;

            if (isNew)
            {
                _promptsRepository.Add(prompt);
            }
            else
            {
                _promptsRepository.Update(prompt);
            }

            LoadPrompts();
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                var import = JsonSerializer.Deserialize<PromptsExport>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (import?.Prompts != null)
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import Prompts",
                        Content = $"Found {import.Prompts.Count} prompts. Replace existing prompts or add to them?",
                        PrimaryButtonText = "Replace All",
                        SecondaryButtonText = "Add",
                        CloseButtonText = "Cancel"
                    };

                    var result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        _promptsRepository.ReplaceAll(import.Prompts);
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        foreach (var prompt in import.Prompts)
                        {
                            prompt.Position = _promptsRepository.Count();
                            _promptsRepository.Add(prompt);
                        }
                    }

                    LoadPrompts();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Error",
                    Content = $"Failed to import prompts: {ex.Message}",
                    CloseButtonText = "OK"
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("JSON", new[] { ".json" });
        picker.SuggestedFileName = "dictate_prompts";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            var prompts = _promptsRepository.GetAll();
            var export = new PromptsExport
            {
                Version = 1,
                Prompts = prompts.ToList()
            };

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await Windows.Storage.FileIO.WriteTextAsync(file, json);
        }
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private class PromptsExport
    {
        public int Version { get; set; }
        public List<PromptModel> Prompts { get; set; } = new();
    }
}
