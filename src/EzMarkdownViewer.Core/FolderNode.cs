using CommunityToolkit.Mvvm.ComponentModel;

namespace EzMarkdownViewer.Core;

/// <summary>
/// A directory in the tree. Children are loaded lazily on first access:
/// all immediate subfolders and immediate <c>.md</c> files, sorted
/// folders-first then alphabetical (case-insensitive).
/// </summary>
public sealed partial class FolderNode : DirectoryTreeNode
{
    private const string MarkdownSearchPattern = "*.md";

    private readonly IFileSystem _fileSystem;
    private IReadOnlyList<DirectoryTreeNode>? _children;

    public FolderNode(string fullPath, IFileSystem fileSystem)
        : base(GetFolderDisplayName(fullPath), fullPath)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Direct children of this folder. Populated on first access; subsequent
    /// reads return the cached list.
    /// </summary>
    public IReadOnlyList<DirectoryTreeNode> Children => _children ??= LoadChildren();

    /// <summary>
    /// Children if they have already been loaded, or <c>null</c> otherwise.
    /// Callers traversing the tree for bookkeeping (e.g. snapshotting
    /// expansion state) use this to avoid triggering disk reads on folders
    /// the user has never opened.
    /// </summary>
    public IReadOnlyList<DirectoryTreeNode>? LoadedChildren => _children;

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
        foreach (var child in _children.OfType<FolderNode>())
        {
            child.SetExpandedRecursive(isExpanded);
        }
    }

    private IReadOnlyList<DirectoryTreeNode> LoadChildren()
    {
        var folders = _fileSystem.EnumerateDirectories(FullPath)
            .Select(path => new FolderNode(path, _fileSystem))
            .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Cast<DirectoryTreeNode>();

        var files = _fileSystem.EnumerateFiles(FullPath, MarkdownSearchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new MarkdownFileNode(path))
            .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Cast<DirectoryTreeNode>();

        return folders.Concat(files).ToList();
    }

    private static string GetFolderDisplayName(string fullPath)
    {
        var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? fullPath : name;
    }
}
