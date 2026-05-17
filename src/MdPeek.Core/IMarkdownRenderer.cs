namespace MdPeek.Core;

public interface IMarkdownRenderer
{
    /// <summary>
    /// Controls which embedded stylesheet is applied to rendered output.
    /// <c>true</c> (the default) uses <c>dark.css</c>; <c>false</c> uses
    /// <c>light.css</c>. Set this before calling <see cref="RenderAsync"/>
    /// or <see cref="RenderError"/> to apply the desired theme.
    /// </summary>
    bool IsDarkTheme { get; set; }

    /// <summary>
    /// Renders a markdown document to a complete HTML document with the
    /// embedded stylesheet applied, on a background thread. The result
    /// is suitable for passing directly to a browser host such as
    /// WebView2's NavigateToString.
    /// </summary>
    /// <param name="markdown">The markdown source to render.</param>
    /// <param name="cancellationToken">Cancellation signal observed before
    /// and after the parse step.</param>
    /// <returns>A complete HTML document as a string.</returns>
    Task<string> RenderAsync(string markdown, CancellationToken cancellationToken);

    /// <summary>
    /// Renders an in-pane error message as a complete HTML document with
    /// the embedded stylesheet applied. Used to surface failures
    /// (missing file, IO error, parse failure) in the content pane in the
    /// same visual style as a rendered document.
    /// </summary>
    /// <param name="title">A short heading describing the error category.</param>
    /// <param name="detail">A longer human-readable explanation.</param>
    /// <returns>A complete HTML document as a string.</returns>
    string RenderError(string title, string detail);
}
