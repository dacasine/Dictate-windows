using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DictateForWindows.ViewModels;

/// <summary>
/// ViewModel for the main (hidden) window.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [RelayCommand]
    public void ShowPopup()
    {
        App.Current.ShowPopup();
    }

    [RelayCommand]
    public void ShowSettings()
    {
        App.Current.ShowSettings();
    }

    [RelayCommand]
    public void Exit()
    {
        App.Current.Exit();
    }
}
