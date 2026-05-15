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
}
