using System.Net;

namespace MdPeek.Core.Rendering;

public sealed class PdfRenderer : IDocumentRenderer
{
    private static readonly IReadOnlyList<string> _supportedExtensions = [".pdf"];

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    /// <inheritdoc />
    public bool IsDarkTheme { get; set; } = true;

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, CancellationToken cancellationToken)
        => Task.FromResult<RenderResult>(new RenderResult.Navigate(new Uri(filePath)));

    /// <inheritdoc />
    public RenderResult RenderError(string title, string detail)
        => new RenderResult.Html(
            $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"></head>
            <body>
            <h1>{WebUtility.HtmlEncode(title)}</h1>
            <p>{WebUtility.HtmlEncode(detail)}</p>
            </body>
            </html>
            """);
}
