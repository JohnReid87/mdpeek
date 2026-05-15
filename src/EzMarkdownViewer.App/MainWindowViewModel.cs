using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EzMarkdownViewer.Core;

namespace EzMarkdownViewer.App;

public partial class MainWindowViewModel : ObservableObject
{
    private const string AppName = "ez-markdown-viewer";

    private readonly IFolderPicker _folderPicker;
    private readonly IFileSystem _fileSystem;

    [ObservableProperty]
    private string _windowTitle = AppName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Roots))]
    [NotifyPropertyChangedFor(nameof(HasFolderOpen))]
    [NotifyPropertyChangedFor(nameof(HasNoFolderOpen))]
    private FolderNode? _rootNode;

    public MainWindowViewModel(IFolderPicker folderPicker, IFileSystem fileSystem)
    {
        _folderPicker = folderPicker;
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Single-item collection wrapping <see cref="RootNode"/> so that the
    /// <c>TreeView.ItemsSource</c> binding can be a simple sequence.
    /// </summary>
    public IReadOnlyList<FolderNode> Roots =>
        RootNode is null ? Array.Empty<FolderNode>() : new[] { RootNode };

    public bool HasFolderOpen => RootNode is not null;

    public bool HasNoFolderOpen => RootNode is null;

    [RelayCommand]
    private void OpenFolder()
    {
        var path = _folderPicker.PickFolder();
        if (path is null)
        {
            return;
        }

        var root = new FolderNode(path, _fileSystem);
        RootNode = root;
        WindowTitle = $"{root.DisplayName} — {AppName}";
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
