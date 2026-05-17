namespace MdPeek.App.Abstractions;

public interface IUserConfirmation
{
    /// <summary>
    /// Shows a modal yes/no confirmation prompt. Returns <c>true</c> if the
    /// user confirmed, <c>false</c> if the user declined or dismissed the
    /// prompt.
    /// </summary>
    bool Confirm(string title, string message);
}
