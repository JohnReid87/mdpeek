using CommunityToolkit.Mvvm.ComponentModel;

namespace MdPeek.Core;

/// <summary>
/// Base type for nodes in the directory tree. A node is either a
/// <see cref="FolderNode"/> or a <see cref="MarkdownFileNode"/>.
/// </summary>
public abstract partial class DirectoryTreeNode : ObservableObject
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

    /// <summary>
    /// Whether this node is currently shown in the tree. Toggled by the
    /// filter logic; the UI binds <c>TreeViewItem.Visibility</c> to this.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;
}
