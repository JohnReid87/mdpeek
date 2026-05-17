namespace MdPeek.Core.IO;

/// <summary>
/// A directory in the tree. Children are loaded lazily on first access:
/// all immediate subfolders and immediate document files matching the
/// registered search patterns, sorted folders-first then alphabetical
/// (case-insensitive).
/// </summary>
public sealed class FolderNode : DirectoryTreeNode
{
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<string> _searchPatterns;
    private IReadOnlyList<DirectoryTreeNode>? _children;

    public FolderNode(string fullPath, IFileSystem fileSystem, IReadOnlyList<string> searchPatterns)
        : base(GetFolderDisplayName(fullPath), fullPath)
    {
        _fileSystem = fileSystem;
        _searchPatterns = searchPatterns;
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

    private IReadOnlyList<DirectoryTreeNode> LoadChildren()
    {
        var folders = _fileSystem.EnumerateDirectories(FullPath)
            .Select(path => new FolderNode(path, _fileSystem, _searchPatterns))
            .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Cast<DirectoryTreeNode>();

        var files = _searchPatterns
            .SelectMany(pattern => _fileSystem.EnumerateFiles(FullPath, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new DocumentFileNode(path))
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
