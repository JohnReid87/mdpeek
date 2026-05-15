namespace EzMarkdownViewer.Core;

public interface IMarkdownRenderer
{
    /// <summary>
    /// Renders a markdown document to a complete HTML document with the
    /// embedded dark stylesheet applied. The result is suitable for passing
    /// directly to a browser host such as WebView2's NavigateToString.
    /// </summary>
    /// <param name="markdown">The markdown source to render.</param>
    /// <returns>A complete HTML document as a string.</returns>
    string Render(string markdown);

    /// <summary>
    /// Renders an in-pane error message as a complete HTML document with
    /// the embedded dark stylesheet applied. Used to surface failures
    /// (missing file, IO error, parse failure) in the content pane in the
    /// same visual style as a rendered document.
    /// </summary>
    /// <param name="title">A short heading describing the error category.</param>
    /// <param name="detail">A longer human-readable explanation.</param>
    /// <returns>A complete HTML document as a string.</returns>
    string RenderError(string title, string detail);
}
