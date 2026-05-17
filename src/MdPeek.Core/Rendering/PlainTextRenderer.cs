using System.Net;
using System.Text;

namespace MdPeek.Core.Rendering;

public sealed class PlainTextRenderer : IDocumentRenderer
{
    private static readonly IReadOnlyList<string> _supportedExtensions = [".txt"];

    private const string DarkStylesheetResourceName = "MdPeek.Core.Resources.dark.css";
    private const string LightStylesheetResourceName = "MdPeek.Core.Resources.light.css";

    private readonly IFileSystem _fileSystem;
    private readonly string _darkStylesheet;
    private readonly string _lightStylesheet;

    /// <inheritdoc />
    public bool IsDarkTheme { get; set; } = true;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public PlainTextRenderer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _darkStylesheet = LoadEmbeddedStylesheet(DarkStylesheetResourceName);
        _lightStylesheet = LoadEmbeddedStylesheet(LightStylesheetResourceName);
    }

    /// <inheritdoc />
    public async Task<RenderResult> RenderAsync(string filePath, CancellationToken cancellationToken)
    {
        var bytes = await _fileSystem.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var text = Decode(bytes);
        var body = $"<pre class=\"plaintext\">{WebUtility.HtmlEncode(text)}</pre>";
        return new RenderResult.Html(WrapInDocument(body));
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

    private static string Decode(byte[] bytes)
    {
        // UTF-8 BOM: EF BB BF
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        // Try strict UTF-8 (no BOM); fall back to system ANSI encoding on failure
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Default.GetString(bytes);
        }
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
        var assembly = typeof(PlainTextRenderer).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found in {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
