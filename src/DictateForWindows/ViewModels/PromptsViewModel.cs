using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Models;

namespace DictateForWindows.ViewModels;

/// <summary>
/// ViewModel for the prompts management window.
/// </summary>
public partial class PromptsViewModel : ObservableObject
{
    private readonly IPromptsRepository _repository;

    public ObservableCollection<PromptModel> Prompts { get; } = new();

    [ObservableProperty]
    private PromptModel? _selectedPrompt;

    public PromptsViewModel(IPromptsRepository repository)
    {
        _repository = repository;
        LoadPrompts();
    }

    private void LoadPrompts()
    {
        Prompts.Clear();
        foreach (var prompt in _repository.GetAll())
        {
            Prompts.Add(prompt);
        }
    }

    [RelayCommand]
    public void AddPrompt()
    {
        var prompt = new PromptModel
        {
            Position = Prompts.Count,
            Name = "New Prompt",
            Prompt = ""
        };

        var id = _repository.Add(prompt);
        prompt.Id = id;
        Prompts.Add(prompt);
        SelectedPrompt = prompt;
    }

    [RelayCommand]
    public void DeletePrompt(PromptModel prompt)
    {
        if (prompt.Id > 0)
        {
            _repository.Delete(prompt.Id);
            Prompts.Remove(prompt);
        }
    }

    [RelayCommand]
    public void SavePrompt(PromptModel prompt)
    {
        _repository.Update(prompt);
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadPrompts();
    }
}
