using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EzMarkdownViewer.App;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _windowTitle = "ez-markdown-viewer";

    [RelayCommand]
    private void OpenFolder()
    {
    }

    [RelayCommand]
    private void Refresh()
    {
    }

    [RelayCommand]
    private void RegisterFileAssociation()
    {
    }

    [RelayCommand]
    private void UnregisterFileAssociation()
    {
    }
}
