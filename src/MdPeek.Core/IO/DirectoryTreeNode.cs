namespace MdPeek.Core.IO;

/// <summary>
/// Base type for nodes in the directory tree. A node is either a
/// <see cref="FolderNode"/> or a <see cref="DocumentFileNode"/>.
/// </summary>
public abstract class DirectoryTreeNode
{
    protected DirectoryTreeNode(string displayName, string fullPath)
    {
        DisplayName = displayName;
        FullPath = fullPath;
    }

    /// <summary>The text shown for this node in the tree UI.</summary>
    public string DisplayName { get; }

    /// <summary>The absolute path this node represents on disk.</summary>
    public string FullPath { get; }
}
