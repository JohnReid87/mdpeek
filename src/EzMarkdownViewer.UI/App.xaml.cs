using System.IO;
using System.Windows;
using System.Windows.Threading;

using EzMarkdownViewer.App;
using EzMarkdownViewer.Core;

using Microsoft.Extensions.DependencyInjection;

namespace EzMarkdownViewer.UI;

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
        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddSingleton<IFolderPicker, WpfFolderPicker>();
        services.AddSingleton<IUserConfirmation, WpfUserConfirmation>();
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(GetSettingsFilePath()));
        services.AddSingleton(startupOptions);
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "EzMarkdownViewer", "settings.json");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong: {e.Exception.Message}\n\nThe app will close.",
            "ez-markdown-viewer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown();
    }
}
