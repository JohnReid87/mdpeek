namespace EzMarkdownViewer.Core;

/// <summary>
/// A leaf node representing a single <c>.md</c> file. The display name
/// strips the <c>.md</c> extension.
/// </summary>
public sealed class MarkdownFileNode : DirectoryTreeNode
{
    public MarkdownFileNode(string fullPath)
        : base(Path.GetFileNameWithoutExtension(fullPath), fullPath)
    {
    }
}
