using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using MdPeek.App;

using Microsoft.Web.WebView2.Core;

namespace MdPeek.UI;

public partial class MainWindow : Window
{
    public static readonly RoutedCommand FocusFilterCommand = new(nameof(FocusFilterCommand), typeof(MainWindow));

    private readonly MainWindowViewModel _viewModel;
    private readonly ISettingsStore _settingsStore;
    private bool _webViewReady;
    private readonly Dictionary<string, double> _scrollPositions = new(StringComparer.OrdinalIgnoreCase);
    private string? _renderedPath;
    private double? _pendingScrollRestore;
    private bool _awaitingHistoryNavigation;

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

    // Standard 5-button-mouse convention: XButton1 = Back, XButton2 = Forward.
    // WPF's MouseAction enum has no XButton members, so the binding lives here
    // instead of in <Window.InputBindings>.
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        ICommand? command = e.ChangedButton switch
        {
            MouseButton.XButton1 => _viewModel.GoBackCommand,
            MouseButton.XButton2 => _viewModel.GoForwardCommand,
            _ => null,
        };

        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
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
        try
        {
            await ContentView.EnsureCoreWebView2Async();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowWebView2RuntimeMissingDialog();
            Application.Current.Shutdown();
            return;
        }

        ContentView.CoreWebView2.NavigationStarting += OnNavigationStarting;
        ContentView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _viewModel.HistoryNavigationStarting += OnHistoryNavigationStarting;
        _webViewReady = true;
        await RenderCurrentHtml();
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Only intercept the schemes we know how to handle. NavigateToString itself
        // fires NavigationStarting with a data: URI for the rendered HTML, so the
        // default must be "allow" — cancelling unknown schemes would kill our own
        // content load.
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (uri.Scheme == "file")
        {
            var localPath = uri.LocalPath; // decoded Windows path, e.g. C:\docs\file.md
            if (!localPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = true;

            var currentPath = (_viewModel.SelectedNode as MarkdownFileNodeViewModel)?.FullPath;
            if (!string.IsNullOrEmpty(uri.Fragment) &&
                string.Equals(localPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                // Same-file anchor link (#section): scroll in-page without navigating.
                var anchor = uri.Fragment.TrimStart('#');
                var escaped = anchor.Replace("\\", "\\\\").Replace("\"", "\\\"");
                _ = ContentView.CoreWebView2.ExecuteScriptAsync(
                    $"document.getElementById(\"{escaped}\")?.scrollIntoView({{behavior:'smooth'}});");
            }
            else
            {
                _viewModel.NavigateToMarkdownFileByPath(localPath);
            }
        }
        else if (uri.Scheme is "http" or "https")
        {
            e.Cancel = true;
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
        }
    }

    private void ShowWebView2RuntimeMissingDialog()
    {
        const string downloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

        var result = MessageBox.Show(
            this,
            "mdpeek requires the Microsoft Edge WebView2 Runtime to display rendered markdown, " +
            "but it was not found on this PC.\n\n" +
            "Open the download page now?",
            "WebView2 Runtime required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);

        if (result == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.HtmlContent))
        {
            _ = RenderCurrentHtml();
        }
    }

    private async Task RenderCurrentHtml()
    {
        if (!_webViewReady)
        {
            return;
        }

        // Capture the scroll position of the page being navigated away from.
        if (_renderedPath != null)
        {
            var scrollResult = await ContentView.CoreWebView2.ExecuteScriptAsync("window.scrollY");
            if (double.TryParse(scrollResult, NumberStyles.Float, CultureInfo.InvariantCulture, out var scrollY))
            {
                _scrollPositions[_renderedPath] = scrollY;
            }
        }

        var html = _viewModel.HtmlContent ?? string.Empty;

        // Inject a base URL so relative .md links and same-page #anchors resolve
        // to the current file's path rather than about:blank.
        if (_viewModel.SelectedNode is MarkdownFileNodeViewModel file)
        {
            var fileUri = new Uri(file.FullPath).AbsoluteUri;
            html = html.Replace("<head>", $"<head>\n<base href=\"{fileUri}\">", StringComparison.Ordinal);

            _pendingScrollRestore = (_awaitingHistoryNavigation && _scrollPositions.TryGetValue(file.FullPath, out var storedY))
                ? storedY
                : null;
            _renderedPath = file.FullPath;
        }
        else
        {
            _pendingScrollRestore = null;
            _renderedPath = null;
        }

        _awaitingHistoryNavigation = false;
        ContentView.NavigateToString(html);
    }

    private void OnHistoryNavigationStarting(object? sender, EventArgs e)
    {
        _awaitingHistoryNavigation = true;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_pendingScrollRestore is double scrollY)
        {
            _pendingScrollRestore = null;
            await ContentView.CoreWebView2.ExecuteScriptAsync(
                $"window.scrollTo(0, {scrollY.ToString(CultureInfo.InvariantCulture)});");
        }
    }

    private void DirectoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.SelectedNode = e.NewValue as DirectoryTreeNodeViewModel;
    }

    private void FocusFilter_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!FilterBox.IsVisible)
        {
            return;
        }

        FilterBox.Focus();
        FilterBox.SelectAll();
        e.Handled = true;
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
