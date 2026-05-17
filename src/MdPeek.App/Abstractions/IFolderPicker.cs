namespace MdPeek.App.Abstractions;

public interface IFolderPicker
{
    /// <summary>
    /// Shows a modal folder-picker dialog. Returns the absolute path of the
    /// chosen folder, or <c>null</c> if the user cancelled.
    /// </summary>
    string? PickFolder();
}
