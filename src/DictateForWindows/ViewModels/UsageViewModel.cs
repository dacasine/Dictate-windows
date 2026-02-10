using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictateForWindows.Core.Data;
using DictateForWindows.Core.Models;

namespace DictateForWindows.ViewModels;

/// <summary>
/// ViewModel for the usage statistics window.
/// </summary>
public partial class UsageViewModel : ObservableObject
{
    private readonly IUsageRepository _repository;

    public ObservableCollection<UsageModel> Usages { get; } = new();

    [ObservableProperty]
    private decimal _totalCost;

    [ObservableProperty]
    private string _totalCostFormatted = "$0.00";

    [ObservableProperty]
    private long _totalAudioMs;

    [ObservableProperty]
    private string _totalAudioFormatted = "0:00";

    public UsageViewModel(IUsageRepository repository)
    {
        _repository = repository;
        LoadUsage();
    }

    private void LoadUsage()
    {
        Usages.Clear();
        foreach (var usage in _repository.GetAll())
        {
            Usages.Add(usage);
        }

        TotalCost = _repository.GetTotalCost();
        TotalCostFormatted = $"${TotalCost:F4}";

        TotalAudioMs = _repository.GetTotalAudioTimeMs();
        var span = TimeSpan.FromMilliseconds(TotalAudioMs);
        TotalAudioFormatted = span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes}:{span.Seconds:D2}";
    }

    [RelayCommand]
    public void Reset()
    {
        _repository.Reset();
        LoadUsage();
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadUsage();
    }
}
