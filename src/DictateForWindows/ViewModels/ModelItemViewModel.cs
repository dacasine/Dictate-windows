using CommunityToolkit.Mvvm.ComponentModel;

namespace DictateForWindows.ViewModels;

public partial class ModelItemViewModel : ObservableObject
{
    public string ModelId { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ModelItemViewModel(string modelId)
    {
        ModelId = modelId;
        DisplayName = GetShortName(modelId);
    }

    private static string GetShortName(string modelId) => modelId switch
    {
        "gpt-4o" => "4o",
        "gpt-4o-mini" => "4o-m",
        "gpt-4.1" => "4.1",
        "gpt-4.1-mini" => "4.1m",
        "gpt-4.1-nano" => "4.1n",
        "o4-mini" => "o4m",
        "o3" => "o3",
        "o3-mini" => "o3m",
        "claude-sonnet-4-5-20250929" => "Son",
        "claude-haiku-4-5-20251001" => "Hai",
        "llama-3.3-70b-versatile" => "Ll70",
        "llama-3.1-8b-instant" => "Ll8",
        "whisper-large-v3" => "wh3",
        "whisper-large-v3-turbo" => "whT",
        _ when modelId.Length > 5 => modelId[..5],
        _ => modelId
    };
}
