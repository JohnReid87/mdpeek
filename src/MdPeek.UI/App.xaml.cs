using System.IO;
using System.Windows;
using System.Windows.Threading;

using MdPeek.App;
using MdPeek.Core;

using Microsoft.Extensions.DependencyInjection;

namespace MdPeek.UI;

public partial class App : Application
{
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var startupOptions = new StartupOptions
        {
            Path = e.Args.Length > 0 ? e.Args[0] : null,
        };

        var services = new ServiceCollection();
        ConfigureServices(services, startupOptions);
        _services = services.BuildServiceProvider();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services, StartupOptions startupOptions)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IDocumentRenderer, MarkdownRenderer>();
        services.AddSingleton<IDocumentRendererFactory>(sp =>
            new DocumentRendererFactory(sp.GetServices<IDocumentRenderer>()));
        services.AddSingleton<IFolderPicker, WpfFolderPicker>();
        services.AddSingleton<IUserConfirmation, WpfUserConfirmation>();
        services.AddSingleton<IUserNotification, WpfUserNotification>();
        services.AddSingleton<IFileAssociationRegistrar, WindowsFileAssociationRegistrar>();
        services.AddSingleton<ISettingsStore>(sp =>
            new JsonSettingsStore(GetSettingsFilePath(), sp.GetRequiredService<IFileSystem>()));
        services.AddSingleton(startupOptions);
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MdPeek", "settings.json");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong: {e.Exception.Message}\n\nThe app will close.",
            "mdpeek",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown();
    }
}
