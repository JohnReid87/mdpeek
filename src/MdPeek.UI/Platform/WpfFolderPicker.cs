using System.Windows;


using Microsoft.Win32;

namespace MdPeek.UI.Platform;

internal sealed class WpfFolderPicker : IFolderPicker
{
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Open Folder",
            Multiselect = false,
        };

        var owner = Application.Current?.MainWindow;
        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

        return result == true ? dialog.FolderName : null;
    }
}
