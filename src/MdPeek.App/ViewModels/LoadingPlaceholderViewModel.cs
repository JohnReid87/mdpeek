
namespace MdPeek.App.ViewModels;

/// <summary>
/// Sentinel child shown in <see cref="FolderNodeViewModel.DisplayChildren"/>
/// while the folder's real children are being enumerated on a background
/// thread. Having a non-empty <c>DisplayChildren</c> keeps the
/// <see cref="System.Windows.Controls.TreeViewItem"/> chevron visible so the
/// user can still see the folder is collapsible while it loads. Replaced
/// in-place once enumeration completes.
/// </summary>
public sealed class LoadingPlaceholderViewModel : DirectoryTreeNodeViewModel
{
    public static LoadingPlaceholderViewModel Instance { get; } = new();

    private LoadingPlaceholderViewModel()
        : base(new PlaceholderNode())
    {
    }

    private sealed class PlaceholderNode : DirectoryTreeNode
    {
        public PlaceholderNode()
            : base("Loading…", string.Empty)
        {
        }
    }
}
