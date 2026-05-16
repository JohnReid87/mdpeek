using CommunityToolkit.Mvvm.ComponentModel;

using MdPeek.Core;

namespace MdPeek.App;

/// <summary>
/// View-model for a <see cref="FolderNode"/>. Owns the UI expansion state
/// and lazily wraps the underlying folder's children as they are loaded.
/// </summary>
public sealed partial class FolderNodeViewModel : DirectoryTreeNodeViewModel
{
    private readonly FolderNode _folder;
    private IReadOnlyList<DirectoryTreeNodeViewModel>? _children;

    public FolderNodeViewModel(FolderNode folder)
        : base(folder)
    {
        _folder = folder;
    }

    public FolderNode Folder => _folder;

    /// <summary>
    /// Wrapped children of this folder. Triggers a disk read on first access
    /// via <see cref="FolderNode.Children"/>; subsequent reads return the
    /// cached wrappers.
    /// </summary>
    public IReadOnlyList<DirectoryTreeNodeViewModel> Children =>
        _children ??= _folder.Children.Select(Wrap).ToList();

    /// <summary>
    /// Wrapped children if the underlying folder has already loaded them, or
    /// <c>null</c> otherwise. Callers traversing the tree for bookkeeping use
    /// this to avoid triggering disk reads on folders the user has never
    /// opened.
    /// </summary>
    public IReadOnlyList<DirectoryTreeNodeViewModel>? LoadedChildren
    {
        get
        {
            var loaded = _folder.LoadedChildren;
            if (loaded is null)
            {
                return null;
            }
            return _children ??= loaded.Select(Wrap).ToList();
        }
    }

    /// <summary>
    /// Whether this folder is currently expanded in the UI tree. Bound
    /// two-way to <c>TreeViewItem.IsExpanded</c> so persisted expanded-folder
    /// state can be inspected on shutdown, and so the filter logic can
    /// programmatically expand ancestors of matches.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Sets <see cref="IsExpanded"/> on this folder and every already-loaded
    /// descendant folder. Folders the user has not yet opened are left
    /// untouched, so this operation does not trigger any disk reads.
    /// </summary>
    public void SetExpandedRecursive(bool isExpanded)
    {
        IsExpanded = isExpanded;
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
