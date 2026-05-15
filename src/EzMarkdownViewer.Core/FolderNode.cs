namespace EzMarkdownViewer.Core;

/// <summary>
/// A directory in the tree. Children are loaded lazily on first access:
/// all immediate subfolders and immediate <c>.md</c> files, sorted
/// folders-first then alphabetical (case-insensitive).
/// </summary>
public sealed class FolderNode : DirectoryTreeNode
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
