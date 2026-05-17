using System.Windows;


namespace MdPeek.UI.Platform;

internal sealed class WpfUserNotification : IUserNotification
{
    public void Notify(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        if (owner is null)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
