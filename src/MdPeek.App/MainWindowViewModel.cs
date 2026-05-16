using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MdPeek.Core;

namespace MdPeek.App;

public partial class MainWindowViewModel : ObservableObject
{
    private const string AppName = "mdpeek";

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
    /// <summary>
    /// Path → wrapper index for resolving an existing
    /// <see cref="MarkdownFileNodeViewModel"/> from the live tree when
    /// Back/Forward/<see cref="TryOpenFromPath"/> or settings restore need to
    /// re-select a file by path. Without this index those paths would have to
    /// allocate a stranger wrapper that is disconnected from the tree, so the
    /// <c>TreeView</c> selection highlight would not follow. Populated by
    /// <see cref="FolderNodeViewModel"/> as folder children are loaded;
    /// cleared whenever the tree is re-rooted.
    /// </summary>
    private readonly Dictionary<string, MarkdownFileNodeViewModel> _fileIndex =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _navigatingHistory;
    private bool _filterApplied;
    private Dictionary<string, bool>? _preFilterExpansion;

    /// <summary>
    /// Cancellation source for the most recently started file load. Selecting
    /// a different file mid-read or mid-render cancels the previous load so
    /// its completion does not race ahead and overwrite the new file's
    /// rendered output.
    /// </summary>
    private CancellationTokenSource? _loadCts;
    private Task? _loadTask;

    [ObservableProperty]
    private string _windowTitle = AppName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Roots))]
    [NotifyPropertyChangedFor(nameof(HasFolderOpen))]
    [NotifyPropertyChangedFor(nameof(HasNoFolderOpen))]
    [NotifyPropertyChangedFor(nameof(CanGoUp))]
    [NotifyCanExecuteChangedFor(nameof(GoUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetAsRootCommand))]
    private FolderNodeViewModel? _rootNode;

    [ObservableProperty]
    private DirectoryTreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    private string? _htmlContent;

    [ObservableProperty]
    private string _filterText = string.Empty;

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
    public IReadOnlyList<FolderNodeViewModel> Roots =>
        RootNode is null ? Array.Empty<FolderNodeViewModel>() : new[] { RootNode };

    public bool HasFolderOpen => RootNode is not null;

    public bool HasNoFolderOpen => RootNode is null;

    public bool CanGoBack => _history.CanGoBack;

    public bool CanGoForward => _history.CanGoForward;

    public bool CanGoUp => TryGetParentDirectory(RootNode?.FullPath) is not null;

    partial void OnSelectedNodeChanged(DirectoryTreeNodeViewModel? oldValue, DirectoryTreeNodeViewModel? newValue)
    {
        // Mirror SelectedNode onto the node's own IsSelected so VM-driven
        // selection (Back/Forward, command-line open, settings restore)
        // flows out to TreeViewItem.IsSelected via the two-way binding —
        // the TreeView would otherwise have no way to follow programmatic
        // selection changes.
        if (oldValue is not null && !ReferenceEquals(oldValue, newValue))
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        if (newValue is MarkdownFileNodeViewModel file)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var recordHistory = !_navigatingHistory;
            _loadTask = LoadFileAsync(file.File, recordHistory, _loadCts.Token);
        }
    }

    /// <summary>
    /// The most recently started file load, or <c>null</c> if no file has been
    /// selected yet. Tests use this to await async completion before
    /// asserting; production code does not need it because the load is
    /// fire-and-forget.
    /// </summary>
    internal Task? CurrentLoadTask => _loadTask;

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnRootNodeChanged(FolderNodeViewModel? value)
    {
        // A new tree has no filter state to clear — only re-apply the filter
        // if it is currently non-empty (e.g. after Refresh rebuilds the tree).
        _filterApplied = false;
        _preFilterExpansion = null;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (RootNode is null)
        {
            return;
        }

        var filter = FilterText.Trim();
        if (filter.Length == 0)
        {
            if (!_filterApplied)
            {
                return;
            }
            ClearFilter(RootNode);
            if (_preFilterExpansion is not null)
            {
                RestoreExpansion(RootNode, _preFilterExpansion);
                _preFilterExpansion = null;
            }
            _filterApplied = false;
            return;
        }

        // Snapshot the user's manual expansion state the first time a filter
        // is applied to this tree, so that clearing the filter restores the
        // tree to how it looked before — instead of leaving every folder the
        // filter force-expanded permanently open.
        _preFilterExpansion ??= SnapshotExpansion(RootNode);

        FilterRecursive(RootNode, filter);
        _filterApplied = true;
    }

    /// <summary>
    /// Walks <paramref name="node"/> and its descendants applying
    /// <paramref name="filter"/>. A node matches if its display name contains
    /// the filter (case-insensitive); a folder is also visible if any
    /// descendant matches, and is force-expanded to reveal those matches.
    /// Returns whether <paramref name="node"/> ended up visible.
    /// </summary>
    private static bool FilterRecursive(DirectoryTreeNodeViewModel node, string filter)
    {
        var selfMatches = node.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (node is FolderNodeViewModel folder)
        {
            var anyChildVisible = false;
            foreach (var child in folder.Children)
            {
                if (FilterRecursive(child, filter))
                {
                    anyChildVisible = true;
                }
            }

            folder.IsVisible = selfMatches || anyChildVisible;
            if (anyChildVisible)
            {
                folder.IsExpanded = true;
            }
            return folder.IsVisible;
        }

        node.IsVisible = selfMatches;
        return selfMatches;
    }

    /// <summary>
    /// Restores <see cref="DirectoryTreeNodeViewModel.IsVisible"/> to
    /// <c>true</c> on <paramref name="node"/> and any descendants that were
    /// previously loaded.
    /// </summary>
    private static void ClearFilter(DirectoryTreeNodeViewModel node)
    {
        node.IsVisible = true;
        if (node is FolderNodeViewModel folder)
        {
            foreach (var child in folder.Children)
            {
                ClearFilter(child);
            }
        }
    }

    /// <summary>
    /// Captures the <see cref="FolderNodeViewModel.IsExpanded"/> state of
    /// every already-loaded folder under <paramref name="root"/>. Folders the
    /// user has not yet opened are not enumerated, since they default to
    /// collapsed and absence from the snapshot is treated as collapsed on
    /// restore.
    /// </summary>
    private static Dictionary<string, bool> SnapshotExpansion(FolderNodeViewModel root)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Walk(root, map);
        return map;

        static void Walk(FolderNodeViewModel folder, Dictionary<string, bool> map)
        {
            map[folder.FullPath] = folder.IsExpanded;
            if (folder.LoadedChildren is null)
            {
                return;
            }
            foreach (var child in folder.LoadedChildren.OfType<FolderNodeViewModel>())
            {
                Walk(child, map);
            }
        }
    }

    /// <summary>
    /// Restores <see cref="FolderNodeViewModel.IsExpanded"/> on every loaded
    /// folder under <paramref name="root"/> from <paramref name="snapshot"/>.
    /// Folders absent from the snapshot — typically those the filter
    /// force-loaded — are collapsed back, returning the tree to its
    /// pre-filter appearance.
    /// </summary>
    private static void RestoreExpansion(FolderNodeViewModel root, Dictionary<string, bool> snapshot)
    {
        Walk(root, snapshot);

        static void Walk(FolderNodeViewModel folder, Dictionary<string, bool> snapshot)
        {
            folder.IsExpanded = snapshot.TryGetValue(folder.FullPath, out var wasExpanded) && wasExpanded;
            if (folder.LoadedChildren is null)
            {
                return;
            }
            foreach (var child in folder.LoadedChildren.OfType<FolderNodeViewModel>())
            {
                Walk(child, snapshot);
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

    private async Task LoadFileAsync(MarkdownFileNode file, bool recordHistory, CancellationToken cancellationToken)
    {
        var path = file.FullPath;

        if (!_fileSystem.FileExists(path))
        {
            HtmlContent = _markdownRenderer.RenderError(
                "File not found",
                $"The file '{path}' could not be found. It may have been moved, renamed, or deleted since the folder was opened.");
            RecordHistoryVisit(recordHistory, path);
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
            RecordHistoryVisit(recordHistory, path);
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
            text = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            HtmlContent = _markdownRenderer.RenderError(
                "Could not read file",
                $"The file '{path}' could not be opened: {ex.Message}");
            RecordHistoryVisit(recordHistory, path);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string html;
        try
        {
            html = await _markdownRenderer.RenderAsync(text, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            HtmlContent = _markdownRenderer.RenderError(
                "Could not render markdown",
                $"The file '{path}' could not be rendered as markdown: {ex.Message}");
            RecordHistoryVisit(recordHistory, path);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        HtmlContent = html;
        RecordHistoryVisit(recordHistory, path);
    }

    private void RecordHistoryVisit(bool recordHistory, string path)
    {
        if (!recordHistory)
        {
            return;
        }

        _history.Visit(path);
        OnHistoryChanged();
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
        FilterText = string.Empty;

        RootNode = CreateRoot(path);
        WindowTitle = $"{RootNode.DisplayName} — {AppName}";
    }

    /// <summary>
    /// Re-roots the tree at <paramref name="folder"/>. Unlike Open Folder this
    /// keeps the back/forward navigation history intact — the user is drilling
    /// down within the same browsing session, not starting a new one. The
    /// filter is cleared so the new tree appears unfiltered.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetAsRoot))]
    private void SetAsRoot(FolderNodeViewModel? folder)
    {
        if (folder is null || !_fileSystem.DirectoryExists(folder.FullPath))
        {
            return;
        }

        FilterText = string.Empty;

        RootNode = CreateRoot(folder.FullPath);
        WindowTitle = $"{RootNode.DisplayName} — {AppName}";
    }

    private bool CanSetAsRoot(FolderNodeViewModel? folder) =>
        folder is not null &&
        RootNode is not null &&
        !string.Equals(folder.FullPath, RootNode.FullPath, StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp()
    {
        if (RootNode is null)
        {
            return;
        }

        var parent = TryGetParentDirectory(RootNode.FullPath);
        if (parent is null || !_fileSystem.DirectoryExists(parent))
        {
            return;
        }

        var selectedFilePath = SelectedNode is MarkdownFileNodeViewModel file ? file.FullPath : null;
        var expandedFolders = new HashSet<string>(
            CollectExpandedFolders(RootNode),
            StringComparer.OrdinalIgnoreCase);

        FilterText = string.Empty;
        var newRoot = CreateRoot(parent);
        RootNode = newRoot;
        WindowTitle = $"{newRoot.DisplayName} — {AppName}";

        // Expand the new root so the previous root (now a child) is visible,
        // then re-apply the user's prior expansion state to descendants so
        // Go Up adds a level above without collapsing what they had open.
        newRoot.IsExpanded = true;
        foreach (var child in newRoot.Children.OfType<FolderNodeViewModel>())
        {
            ApplyExpansion(child, expandedFolders);
        }

        if (selectedFilePath is null)
        {
            return;
        }

        ExpandToFile(newRoot, selectedFilePath);

        // Re-select the file in the new tree without recording a history entry —
        // Go Up does not change which file is being viewed, so it should not
        // count as a new visit.
        _navigatingHistory = true;
        try
        {
            SelectedNode = ResolveFileViewModel(selectedFilePath);
        }
        finally
        {
            _navigatingHistory = false;
        }
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
            SelectedNode = ResolveFileViewModel(path);
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
            FilterText = string.Empty;

            RootNode = CreateRoot(path);
            WindowTitle = $"{RootNode.DisplayName} — {AppName}";
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
        FilterText = string.Empty;

        RootNode = CreateRoot(parent);
        WindowTitle = $"{RootNode.DisplayName} — {AppName}";
        SelectedNode = ResolveFileViewModel(path);
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

        var root = CreateRoot(settings.LastFolder);
        RootNode = root;
        WindowTitle = $"{root.DisplayName} — {AppName}";

        if (settings.ExpandedFolders.Count > 0)
        {
            var expanded = new HashSet<string>(settings.ExpandedFolders, StringComparer.OrdinalIgnoreCase);
            ApplyExpansion(root, expanded);
        }

        if (settings.LastSelectedFile is not null && _fileSystem.FileExists(settings.LastSelectedFile))
        {
            SelectedNode = ResolveFileViewModel(settings.LastSelectedFile);
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
        settings.LastSelectedFile = SelectedNode is MarkdownFileNodeViewModel file ? file.FullPath : null;
        settings.ExpandedFolders = RootNode is null
            ? new List<string>()
            : CollectExpandedFolders(RootNode).ToList();
    }

    private FolderNodeViewModel CreateRoot(string fullPath)
    {
        _fileIndex.Clear();
        return new FolderNodeViewModel(new FolderNode(fullPath, _fileSystem), RegisterFileInIndex);
    }

    private void RegisterFileInIndex(MarkdownFileNodeViewModel file) =>
        _fileIndex[file.FullPath] = file;

    /// <summary>
    /// Selects the markdown file at <paramref name="fullPath"/> in the tree,
    /// recording a history visit. Called from the UI when the user clicks an
    /// internal .md hyperlink. Uses the same <see cref="ResolveFileViewModel"/>
    /// path taken by Back/Forward navigation so that the tree highlight follows
    /// when the file has already been loaded into the index.
    /// </summary>
    public void NavigateToMarkdownFileByPath(string fullPath) =>
        SelectedNode = ResolveFileViewModel(fullPath);

    /// <summary>
    /// Returns the live <see cref="MarkdownFileNodeViewModel"/> the tree
    /// already has for <paramref name="path"/>, or a fresh wrapper if the
    /// containing folder has not been loaded yet. Used by the navigation
    /// entry points so they re-select the existing tree wrapper rather than
    /// allocating a stranger node that would not be tracked by the
    /// <c>TreeView</c>.
    /// </summary>
    private MarkdownFileNodeViewModel ResolveFileViewModel(string path) =>
        _fileIndex.TryGetValue(path, out var existing)
            ? existing
            : new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

    /// <summary>
    /// Returns the parent directory of <paramref name="path"/>, or <c>null</c>
    /// if <paramref name="path"/> is itself a root (e.g. <c>C:\</c>) or is
    /// otherwise without a parent.
    /// </summary>
    private static string? TryGetParentDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    /// <summary>
    /// Walks <paramref name="root"/> down to the folder containing
    /// <paramref name="filePath"/>, expanding each ancestor folder so the file
    /// becomes visible in the tree.
    /// </summary>
    private static void ExpandToFile(FolderNodeViewModel root, string filePath)
    {
        var current = root;
        while (true)
        {
            current.IsExpanded = true;
            FolderNodeViewModel? next = null;
            foreach (var child in current.Children.OfType<FolderNodeViewModel>())
            {
                if (IsAncestorOf(child.FullPath, filePath))
                {
                    next = child;
                    break;
                }
            }
            if (next is null)
            {
                return;
            }
            current = next;
        }
    }

    private static bool IsAncestorOf(string folder, string filePath)
    {
        var folderNorm = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return filePath.StartsWith(folderNorm + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || filePath.StartsWith(folderNorm + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyExpansion(FolderNodeViewModel folder, HashSet<string> expandedPaths)
    {
        if (!expandedPaths.Contains(folder.FullPath))
        {
            return;
        }

        folder.IsExpanded = true;
        foreach (var child in folder.Children.OfType<FolderNodeViewModel>())
        {
            ApplyExpansion(child, expandedPaths);
        }
    }

    private static IEnumerable<string> CollectExpandedFolders(FolderNodeViewModel folder)
    {
        if (!folder.IsExpanded)
        {
            yield break;
        }

        yield return folder.FullPath;
        foreach (var child in folder.Children.OfType<FolderNodeViewModel>())
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
        var selectedFilePath = SelectedNode is MarkdownFileNodeViewModel file ? file.FullPath : null;
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

        var newRoot = CreateRoot(rootPath);
        RootNode = newRoot;
        WindowTitle = $"{newRoot.DisplayName} — {AppName}";

        if (expandedFolders.Count > 0)
        {
            ApplyExpansion(newRoot, expandedFolders);
        }

        if (selectedFilePath is not null && _fileSystem.FileExists(selectedFilePath))
        {
            SelectedNode = ResolveFileViewModel(selectedFilePath);
        }
        else
        {
            SelectedNode = null;
            HtmlContent = null;
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        RootNode?.SetExpandedRecursive(true);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        RootNode?.SetExpandedRecursive(false);
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
