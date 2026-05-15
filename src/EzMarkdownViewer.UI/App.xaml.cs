using System.Windows;

using Microsoft.Extensions.DependencyInjection;

namespace EzMarkdownViewer.UI;

public partial class App : Application
{
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
    }
}
