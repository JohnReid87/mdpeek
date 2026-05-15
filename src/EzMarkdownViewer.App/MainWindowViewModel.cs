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
    private readonly IUserNotification _userNotification;
    private readonly IFileAssociationRegistrar _fileAssociationRegistrar;
    private readonly NavigationHistory _history = new();
    private bool _navigatingHistory;

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
        IUserConfirmation userConfirmation,
        IUserNotification userNotification,
        IFileAssociationRegistrar fileAssociationRegistrar)
    {
        _folderPicker = folderPicker;
        _fileSystem = fileSystem;
        _markdownRenderer = markdownRenderer;
        _userConfirmation = userConfirmation;
        _userNotification = userNotification;
        _fileAssociationRegistrar = fileAssociationRegistrar;
    }

    /// <summary>
    /// Single-item collection wrapping <see cref="RootNode"/> so that the
    /// <c>TreeView.ItemsSource</c> binding can be a simple sequence.
    /// </summary>
    public IReadOnlyList<FolderNode> Roots =>
        RootNode is null ? Array.Empty<FolderNode>() : new[] { RootNode };

    public bool HasFolderOpen => RootNode is not null;

    public bool HasNoFolderOpen => RootNode is null;

    public bool CanGoBack => _history.CanGoBack;

    public bool CanGoForward => _history.CanGoForward;

    partial void OnSelectedNodeChanged(DirectoryTreeNode? value)
    {
        if (value is MarkdownFileNode file)
        {
            var displayed = LoadFile(file);
            if (displayed && !_navigatingHistory)
            {
                _history.Visit(file.FullPath);
                OnHistoryChanged();
            }
        }
    }

    private void OnHistoryChanged()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    private bool LoadFile(MarkdownFileNode file)
    {
        var path = file.FullPath;

        if (!_fileSystem.FileExists(path))
        {
            HtmlContent = _markdownRenderer.RenderError(
                "File not found",
                $"The file '{path}' could not be found. It may have been moved, renamed, or deleted since the folder was opened.");
            return true;
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
            return true;
        }

        if (size > LargeFileThresholdBytes)
        {
            var sizeMb = size / (double)(1024 * 1024);
            var confirmed = _userConfirmation.Confirm(
                "Large file",
                $"'{file.DisplayName}' is {sizeMb:F1} MB. Rendering large markdown documents can be slow. Continue?");

            if (!confirmed)
            {
                return false;
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
            return true;
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

        return true;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var path = _folderPicker.PickFolder();
        if (path is null)
        {
            return;
        }

        _history.Clear();
        OnHistoryChanged();

        var root = new FolderNode(path, _fileSystem);
        RootNode = root;
        WindowTitle = $"{root.DisplayName} — {AppName}";
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        var path = _history.Back();
        if (path is null)
        {
            return;
        }

        NavigateToHistoryEntry(path);
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward()
    {
        var path = _history.Forward();
        if (path is null)
        {
            return;
        }

        NavigateToHistoryEntry(path);
    }

    private void NavigateToHistoryEntry(string path)
    {
        _navigatingHistory = true;
        try
        {
            SelectedNode = new MarkdownFileNode(path);
        }
        finally
        {
            _navigatingHistory = false;
        }

        OnHistoryChanged();
    }

    /// <summary>
    /// Opens the tree at <paramref name="path"/>: if it is a directory, that
    /// directory becomes the root; if it is a <c>.md</c> file, its parent
    /// becomes the root and the file is pre-selected so it renders
    /// immediately. Returns <c>false</c> for anything else (missing path,
    /// non-markdown file, file in an inaccessible directory) so callers can
    /// fall back to other startup behaviour.
    /// </summary>
    public bool TryOpenFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (_fileSystem.DirectoryExists(path))
        {
            _history.Clear();
            OnHistoryChanged();

            var root = new FolderNode(path, _fileSystem);
            RootNode = root;
            WindowTitle = $"{root.DisplayName} — {AppName}";
            return true;
        }

        if (!_fileSystem.FileExists(path) ||
            !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent) || !_fileSystem.DirectoryExists(parent))
        {
            return false;
        }

        _history.Clear();
        OnHistoryChanged();

        var parentRoot = new FolderNode(parent, _fileSystem);
        RootNode = parentRoot;
        WindowTitle = $"{parentRoot.DisplayName} — {AppName}";
        SelectedNode = new MarkdownFileNode(path);
        return true;
    }

    /// <summary>
    /// Restores the parts of <paramref name="settings"/> the view-model owns:
    /// opens <see cref="AppSettings.LastFolder"/> if it still exists, expands
    /// the previously-expanded folders, and re-selects
    /// <see cref="AppSettings.LastSelectedFile"/> if it is still present.
    /// Missing paths are silently skipped — folders or files removed between
    /// launches fall back to defaults rather than surfacing errors.
    /// </summary>
    public void ApplyStartupSettings(AppSettings settings)
    {
        if (settings.LastFolder is null || !_fileSystem.DirectoryExists(settings.LastFolder))
        {
            return;
        }

        _history.Clear();
        OnHistoryChanged();

        var root = new FolderNode(settings.LastFolder, _fileSystem);
        RootNode = root;
        WindowTitle = $"{root.DisplayName} — {AppName}";

        if (settings.ExpandedFolders.Count > 0)
        {
            var expanded = new HashSet<string>(settings.ExpandedFolders, StringComparer.OrdinalIgnoreCase);
            ApplyExpansion(root, expanded);
        }

        if (settings.LastSelectedFile is not null && _fileSystem.FileExists(settings.LastSelectedFile))
        {
            SelectedNode = new MarkdownFileNode(settings.LastSelectedFile);
        }
    }

    /// <summary>
    /// Captures the view-model's current state onto <paramref name="settings"/>:
    /// the open folder, the selected markdown file (if any), and the set of
    /// expanded folders.
    /// </summary>
    public void PopulateSettingsForSave(AppSettings settings)
    {
        settings.LastFolder = RootNode?.FullPath;
        settings.LastSelectedFile = SelectedNode is MarkdownFileNode file ? file.FullPath : null;
        settings.ExpandedFolders = RootNode is null
            ? new List<string>()
            : CollectExpandedFolders(RootNode).ToList();
    }

    private static void ApplyExpansion(FolderNode folder, HashSet<string> expandedPaths)
    {
        if (!expandedPaths.Contains(folder.FullPath))
        {
            return;
        }

        folder.IsExpanded = true;
        foreach (var child in folder.Children.OfType<FolderNode>())
        {
            ApplyExpansion(child, expandedPaths);
        }
    }

    private static IEnumerable<string> CollectExpandedFolders(FolderNode folder)
    {
        if (!folder.IsExpanded)
        {
            yield break;
        }

        yield return folder.FullPath;
        foreach (var child in folder.Children.OfType<FolderNode>())
        {
            foreach (var path in CollectExpandedFolders(child))
            {
                yield return path;
            }
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (RootNode is null)
        {
            return;
        }

        var rootPath = RootNode.FullPath;
        var selectedFilePath = SelectedNode is MarkdownFileNode file ? file.FullPath : null;
        var expandedFolders = new HashSet<string>(
            CollectExpandedFolders(RootNode),
            StringComparer.OrdinalIgnoreCase);

        if (!_fileSystem.DirectoryExists(rootPath))
        {
            _history.Clear();
            OnHistoryChanged();
            RootNode = null;
            SelectedNode = null;
            HtmlContent = null;
            WindowTitle = AppName;
            return;
        }

        var newRoot = new FolderNode(rootPath, _fileSystem);
        RootNode = newRoot;
        WindowTitle = $"{newRoot.DisplayName} — {AppName}";

        if (expandedFolders.Count > 0)
        {
            ApplyExpansion(newRoot, expandedFolders);
        }

        if (selectedFilePath is not null && _fileSystem.FileExists(selectedFilePath))
        {
            SelectedNode = new MarkdownFileNode(selectedFilePath);
        }
        else
        {
            SelectedNode = null;
            HtmlContent = null;
        }
    }

    [RelayCommand]
    private void RegisterFileAssociation()
    {
        try
        {
            _fileAssociationRegistrar.Register();
        }
        catch (Exception ex)
        {
            _userNotification.Notify(
                "Registration failed",
                $"Could not register the .md file association: {ex.Message}");
            return;
        }

        _userNotification.Notify(
            "Registered",
            $"{AppName} is now registered as a handler for .md files. To make it the default, open Windows Settings → Apps → Default apps and pick it for the .md extension.");
    }

    [RelayCommand]
    private void UnregisterFileAssociation()
    {
        try
        {
            _fileAssociationRegistrar.Unregister();
        }
        catch (Exception ex)
        {
            _userNotification.Notify(
                "Unregistration failed",
                $"Could not unregister the .md file association: {ex.Message}");
            return;
        }

        _userNotification.Notify(
            "Unregistered",
            $"{AppName} has been removed from the list of registered handlers for .md files.");
    }
}
