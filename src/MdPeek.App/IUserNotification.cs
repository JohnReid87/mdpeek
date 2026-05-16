namespace MdPeek.App;

public interface IUserNotification
{
    /// <summary>
    /// Shows a modal informational message with a single dismiss button.
    /// Used to report the outcome of an action when no follow-up choice is
    /// required (distinct from <see cref="IUserConfirmation"/>, which asks a
    /// yes/no question).
    /// </summary>
    void Notify(string title, string message);
}
