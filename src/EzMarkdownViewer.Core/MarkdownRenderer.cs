using Markdig;

namespace EzMarkdownViewer.Core;

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private const string StylesheetResourceName = "EzMarkdownViewer.Core.Resources.dark.css";

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

        return $"""
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
    }

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
