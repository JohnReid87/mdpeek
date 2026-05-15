using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EzMarkdownViewer.Core;

namespace EzMarkdownViewer.App;

public partial class MainWindowViewModel : ObservableObject
{
    private const string AppName = "ez-markdown-viewer";

    /// <summary>
    /// Files larger than this are gated behind a confirmation prompt before
    /// being read and rendered, since both the file read and the WebView2
    /// payload grow proportionally and large documents can be slow.
    /// </summary>
    internal const long LargeFileThresholdBytes = 5L * 1024 * 1024;

    private readonly IFolderPicker _folderPicker;
    private readonly IFileSystem _fileSystem;
    private readonly IMarkdownRenderer _markdownRenderer;
    private readonly IUserConfirmation _userConfirmation;

    [ObservableProperty]
    private string _windowTitle = AppName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Roots))]
    [NotifyPropertyChangedFor(nameof(HasFolderOpen))]
    [NotifyPropertyChangedFor(nameof(HasNoFolderOpen))]
    private FolderNode? _rootNode;

    [ObservableProperty]
    private DirectoryTreeNode? _selectedNode;

    [ObservableProperty]
    private string? _htmlContent;

    public MainWindowViewModel(
        IFolderPicker folderPicker,
        IFileSystem fileSystem,
        IMarkdownRenderer markdownRenderer,
        IUserConfirmation userConfirmation)
    {
        _folderPicker = folderPicker;
        _fileSystem = fileSystem;
        _markdownRenderer = markdownRenderer;
        _userConfirmation = userConfirmation;
    }

    /// <summary>
    /// Single-item collection wrapping <see cref="RootNode"/> so that the
    /// <c>TreeView.ItemsSource</c> binding can be a simple sequence.
    /// </summary>
    public IReadOnlyList<FolderNode> Roots =>
        RootNode is null ? Array.Empty<FolderNode>() : new[] { RootNode };

    public bool HasFolderOpen => RootNode is not null;

    public bool HasNoFolderOpen => RootNode is null;

    partial void OnSelectedNodeChanged(DirectoryTreeNode? value)
    {
        if (value is MarkdownFileNode file)
        {
            LoadFile(file);
        }
    }

    private void LoadFile(MarkdownFileNode file)
    {
        var path = file.FullPath;

        if (!_fileSystem.FileExists(path))
        {
            HtmlContent = _markdownRenderer.RenderError(
                "File not found",
                $"The file '{path}' could not be found. It may have been moved, renamed, or deleted since the folder was opened.");
            return;
        }

        long size;
        try
        {
            size = _fileSystem.GetFileSizeBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HtmlContent = _markdownRenderer.RenderError(
                "Could not read file",
                $"The file '{path}' could not be opened: {ex.Message}");
            return;
        }

        if (size > LargeFileThresholdBytes)
        {
            var sizeMb = size / (double)(1024 * 1024);
            var confirmed = _userConfirmation.Confirm(
                "Large file",
                $"'{file.DisplayName}' is {sizeMb:F1} MB. Rendering large markdown documents can be slow. Continue?");

            if (!confirmed)
            {
                return;
            }
        }

        string text;
        try
        {
            text = _fileSystem.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HtmlContent = _markdownRenderer.RenderError(
                "Could not read file",
                $"The file '{path}' could not be opened: {ex.Message}");
            return;
        }

        try
        {
            HtmlContent = _markdownRenderer.Render(text);
        }
        catch (Exception ex)
        {
            HtmlContent = _markdownRenderer.RenderError(
                "Could not render markdown",
                $"The file '{path}' could not be rendered as markdown: {ex.Message}");
        }
    }

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
