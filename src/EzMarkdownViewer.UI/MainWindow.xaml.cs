using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using EzMarkdownViewer.App;
using EzMarkdownViewer.Core;

namespace EzMarkdownViewer.UI;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _webViewReady;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
