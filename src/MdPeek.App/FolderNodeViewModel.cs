using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using MdPeek.Core;

namespace MdPeek.App;

/// <summary>
/// View-model for a <see cref="FolderNode"/>. Owns the UI expansion state
/// and lazily wraps the underlying folder's children as they are loaded.
///
/// Children are enumerated on a background thread the first time the folder
/// is expanded, so opening folders on slow drives (network shares, OneDrive)
/// does not freeze the UI. <see cref="DisplayChildren"/> is the bindable
/// surface — it shows a <see cref="LoadingPlaceholderViewModel"/> while the
/// enumeration runs and is replaced in place once it completes.
/// </summary>
public sealed partial class FolderNodeViewModel : DirectoryTreeNodeViewModel
{
    private readonly FolderNode _folder;
    private readonly ObservableCollection<DirectoryTreeNodeViewModel> _displayChildren;
    private IReadOnlyList<DirectoryTreeNodeViewModel>? _children;
    private Task? _loadTask;
    private bool _suppressAutoLoad;

    public FolderNodeViewModel(FolderNode folder)
        : base(folder)
    {
        _folder = folder;
        _displayChildren = new ObservableCollection<DirectoryTreeNodeViewModel>
        {
            LoadingPlaceholderViewModel.Instance,
        };
    }

    public FolderNode Folder => _folder;

    /// <summary>
    /// Wrapped children of this folder. Triggers a synchronous disk read on
    /// first access via <see cref="FolderNode.Children"/>; subsequent reads
    /// return the cached wrappers. Used by VM-layer walks (filter, expansion
    /// restore) that need the full subtree.
    /// </summary>
    public IReadOnlyList<DirectoryTreeNodeViewModel> Children
    {
        get
        {
            if (_children is not null)
            {
                return _children;
            }

            var wrapped = _folder.Children.Select(Wrap).ToList();
            AdoptChildren(wrapped);
            return _children!;
        }
    }

    /// <summary>
    /// Wrapped children if they have already been loaded (either synchronously
    /// via <see cref="Children"/> or asynchronously via expansion), or
    /// <c>null</c> otherwise. Callers traversing the tree for bookkeeping use
    /// this to avoid triggering disk reads on folders the user has never
    /// opened.
    /// </summary>
    public IReadOnlyList<DirectoryTreeNodeViewModel>? LoadedChildren => _children;

    /// <summary>
    /// Bindable child collection for the WPF <c>TreeView</c>. Initially holds
    /// a single <see cref="LoadingPlaceholderViewModel"/> so the expand
    /// chevron is shown before enumeration runs; replaced with the real
    /// wrapped children once loading completes.
    /// </summary>
    public ObservableCollection<DirectoryTreeNodeViewModel> DisplayChildren => _displayChildren;

    /// <summary>
    /// Whether this folder's children are currently being enumerated on a
    /// background thread. The UI binds a progress indicator to this so the
    /// user sees activity while a slow drive is being read.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Whether this folder is currently expanded in the UI tree. Bound
    /// two-way to <c>TreeViewItem.IsExpanded</c> so persisted expanded-folder
    /// state can be inspected on shutdown, and so the filter logic can
    /// programmatically expand ancestors of matches. Flipping to <c>true</c>
    /// for the first time kicks off the background enumeration.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && _children is null && _loadTask is null && !_suppressAutoLoad)
        {
            _loadTask = LoadChildrenAsync();
        }
    }

    /// <summary>
    /// Enumerates this folder's children on a background thread and swaps
    /// them into <see cref="DisplayChildren"/> in place of the loading
    /// placeholder. Safe to call repeatedly: completes immediately if the
    /// children have already been loaded (sync or async), and returns the
    /// in-flight task if loading is already underway.
    /// </summary>
    public Task LoadChildrenAsync()
    {
        if (_children is not null)
        {
            return Task.CompletedTask;
        }

        return _loadTask ??= LoadChildrenCoreAsync();
    }

    private async Task LoadChildrenCoreAsync()
    {
        IsLoading = true;
        try
        {
            var wrapped = await Task.Run(() => _folder.Children.Select(Wrap).ToList())
                .ConfigureAwait(true);

            // A synchronous Children access may have populated _children
            // while the background task was running; if so, its wrappers
            // are authoritative (DisplayChildren has already been swapped).
            if (_children is null)
            {
                AdoptChildren(wrapped);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AdoptChildren(IReadOnlyList<DirectoryTreeNodeViewModel> wrapped)
    {
        _children = wrapped;
        _displayChildren.Clear();
        foreach (var child in wrapped)
        {
            _displayChildren.Add(child);
        }
    }

    /// <summary>
    /// Sets <see cref="IsExpanded"/> on this folder and every already-loaded
    /// descendant folder. Folders the user has not yet opened are left
    /// untouched, so this operation does not trigger any disk reads — the
    /// expansion-on-demand load is suppressed for this programmatic path.
    /// </summary>
    public void SetExpandedRecursive(bool isExpanded)
    {
        var previous = _suppressAutoLoad;
        _suppressAutoLoad = true;
        try
        {
            IsExpanded = isExpanded;
        }
        finally
        {
            _suppressAutoLoad = previous;
        }

        if (_children is null)
        {
            return;
        }
        foreach (var child in _children.OfType<FolderNodeViewModel>())
        {
            child.SetExpandedRecursive(isExpanded);
        }
    }
}
