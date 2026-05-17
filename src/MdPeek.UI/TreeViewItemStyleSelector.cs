using System.Windows;
using System.Windows.Controls;

using MdPeek.App;

namespace MdPeek.UI;

internal sealed class TreeViewItemStyleSelector : StyleSelector
{
    public Style? FolderStyle { get; set; }
    public Style? NodeStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container)
        => item is FolderNodeViewModel ? FolderStyle : NodeStyle;
}
