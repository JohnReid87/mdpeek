using System.Net;

using Markdig;

namespace MdPeek.Core;

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private const string StylesheetResourceName = "MdPeek.Core.Resources.dark.css";

    private readonly MarkdownPipeline _pipeline;
    private readonly string _stylesheet;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        _stylesheet = LoadEmbeddedStylesheet();
    }

    /// <inheritdoc />
    public string Render(string markdown)
    {
        var body = Markdown.ToHtml(markdown, _pipeline);

        return WrapInDocument(body);
    }

    /// <inheritdoc />
    public string RenderError(string title, string detail)
    {
        var body = $"""
            <div class="error">
            <h1>{WebUtility.HtmlEncode(title)}</h1>
            <p>{WebUtility.HtmlEncode(detail)}</p>
            </div>
            """;

        return WrapInDocument(body);
    }

    private string WrapInDocument(string body) =>
        $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <style>
        {_stylesheet}
        </style>
        </head>
        <body>
        {body}
        </body>
        </html>
        """;

    private static string LoadEmbeddedStylesheet()
    {
        var assembly = typeof(MarkdownRenderer).Assembly;

        using var stream = assembly.GetManifestResourceStream(StylesheetResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{StylesheetResourceName}' was not found in {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
