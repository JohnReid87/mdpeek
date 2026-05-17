using System.Net;

using Markdig;

namespace MdPeek.Core;

public sealed class MarkdownRenderer : IDocumentRenderer
{
    private static readonly IReadOnlyList<string> _supportedExtensions = [".md"];

    private const string DarkStylesheetResourceName = "MdPeek.Core.Resources.dark.css";
    private const string LightStylesheetResourceName = "MdPeek.Core.Resources.light.css";

    private readonly MarkdownPipeline _pipeline;
    private readonly IFileSystem _fileSystem;
    private readonly string _darkStylesheet;
    private readonly string _lightStylesheet;

    /// <inheritdoc />
    public bool IsDarkTheme { get; set; } = true;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public MarkdownRenderer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;

        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        _darkStylesheet = LoadEmbeddedStylesheet(DarkStylesheetResourceName);
        _lightStylesheet = LoadEmbeddedStylesheet(LightStylesheetResourceName);
    }

    /// <inheritdoc />
    public async Task<RenderResult> RenderAsync(string filePath, CancellationToken cancellationToken)
    {
        var markdown = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        var html = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = Markdown.ToHtml(markdown, _pipeline);
            cancellationToken.ThrowIfCancellationRequested();
            return WrapInDocument(body);
        }, cancellationToken).ConfigureAwait(false);

        return new RenderResult.Html(html);
    }

    /// <inheritdoc />
    public RenderResult RenderError(string title, string detail)
    {
        var body = $"""
            <div class="error">
            <h1>{WebUtility.HtmlEncode(title)}</h1>
            <p>{WebUtility.HtmlEncode(detail)}</p>
            </div>
            """;

        return new RenderResult.Html(WrapInDocument(body));
    }

    private string WrapInDocument(string body) =>
        $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <style>
        {(IsDarkTheme ? _darkStylesheet : _lightStylesheet)}
        </style>
        </head>
        <body>
        {body}
        </body>
        </html>
        """;

    private static string LoadEmbeddedStylesheet(string resourceName)
    {
        var assembly = typeof(MarkdownRenderer).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found in {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
