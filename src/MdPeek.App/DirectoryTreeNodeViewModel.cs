using CommunityToolkit.Mvvm.ComponentModel;

using MdPeek.Core;

namespace MdPeek.App;

/// <summary>
/// View-model wrapper around a <see cref="DirectoryTreeNode"/>. Carries the
/// UI-only state (<see cref="IsVisible"/>, <see cref="IsSelected"/>, and
/// <see cref="FolderNodeViewModel.IsExpanded"/> on folders) so the Core domain
/// types can stay pure POCOs.
/// </summary>
public abstract partial class DirectoryTreeNodeViewModel : ObservableObject
{
    protected DirectoryTreeNodeViewModel(DirectoryTreeNode node)
    {
        Node = node;
    }

    public DirectoryTreeNode Node { get; }

    public string DisplayName => Node.DisplayName;

    public string FullPath => Node.FullPath;

    /// <summary>
    /// Whether this node is currently shown in the tree. Toggled by the
    /// filter logic; the UI binds <c>TreeViewItem.Visibility</c> to this.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Whether this node is the currently selected item in the tree. Bound
    /// two-way to <c>TreeViewItem.IsSelected</c> so the VM layer can drive
    /// selection programmatically (e.g. when restoring the last-selected file
    /// on startup or selecting the first match after a filter).
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
}
