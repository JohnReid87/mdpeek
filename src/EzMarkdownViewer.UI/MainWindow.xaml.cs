using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using EzMarkdownViewer.App;
using EzMarkdownViewer.Core;

namespace EzMarkdownViewer.UI;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISettingsStore _settingsStore;
    private bool _webViewReady;

    public MainWindow(
        MainWindowViewModel viewModel,
        ISettingsStore settingsStore,
        StartupOptions startupOptions)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        var settings = _settingsStore.Load();
        ApplyWindowSettings(settings);

        // A command-line path argument takes precedence over the persisted
        // last-folder so users can launch the app at a specific location.
        if (!_viewModel.TryOpenFromPath(startupOptions.Path))
        {
            _viewModel.ApplyStartupSettings(settings);
        }

        Closing += OnWindowClosing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private void ApplyWindowSettings(AppSettings settings)
    {
        if (settings.WindowWidth is { } w && w >= MinWidth)
        {
            Width = w;
        }

        if (settings.WindowHeight is { } h && h >= MinHeight)
        {
            Height = h;
        }

        if (settings.WindowLeft is { } left && settings.WindowTop is { } top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        if (settings.SplitterPosition is { } sp && sp >= TreeColumn.MinWidth)
        {
            TreeColumn.Width = new GridLength(sp);
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        var settings = new AppSettings();

        if (WindowState == WindowState.Maximized)
        {
            // RestoreBounds captures the un-maximized geometry, so the next
            // launch can restore the user's preferred size if they un-maximize.
            settings.WindowMaximized = true;
            settings.WindowWidth = RestoreBounds.Width;
            settings.WindowHeight = RestoreBounds.Height;
            settings.WindowLeft = RestoreBounds.Left;
            settings.WindowTop = RestoreBounds.Top;
        }
        else
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
        }

        settings.SplitterPosition = TreeColumn.ActualWidth;

        _viewModel.PopulateSettingsForSave(settings);

        _settingsStore.Save(settings);
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await ContentView.EnsureCoreWebView2Async();
        _webViewReady = true;
        RenderCurrentHtml();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.HtmlContent))
        {
            RenderCurrentHtml();
        }
    }

    private void RenderCurrentHtml()
    {
        if (!_webViewReady)
        {
            return;
        }

        ContentView.NavigateToString(_viewModel.HtmlContent ?? string.Empty);
    }

    private void DirectoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.SelectedNode = e.NewValue as DirectoryTreeNode;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }
}
