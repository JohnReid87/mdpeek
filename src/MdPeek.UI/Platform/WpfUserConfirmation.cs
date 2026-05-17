using System.Windows;


namespace MdPeek.UI.Platform;

internal sealed class WpfUserConfirmation : IUserConfirmation
{
    public bool Confirm(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }
}
