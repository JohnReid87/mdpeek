namespace MdPeek.Core.IO;

/// <summary>
/// A leaf node representing a single <c>.md</c> file. The display name is the
/// file name including the <c>.md</c> extension, matching how the file
/// appears in Windows Explorer.
/// </summary>
public sealed class DocumentFileNode : DirectoryTreeNode
{
    public DocumentFileNode(string fullPath)
        : base(Path.GetFileName(fullPath), fullPath)
    {
    }
}
