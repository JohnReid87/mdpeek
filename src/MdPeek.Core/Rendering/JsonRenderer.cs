using System.Net;
using System.Text;
using System.Text.Json;

namespace MdPeek.Core.Rendering;

public sealed class JsonRenderer : IDocumentRenderer
{
    private static readonly IReadOnlyList<string> _supportedExtensions = [".json"];

    private const string DarkStylesheetResourceName = "MdPeek.Core.Resources.dark.css";
    private const string LightStylesheetResourceName = "MdPeek.Core.Resources.light.css";

    private readonly IFileSystem _fileSystem;
    private readonly string _darkStylesheet;
    private readonly string _lightStylesheet;

    /// <inheritdoc />
    public bool IsDarkTheme { get; set; } = true;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public JsonRenderer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _darkStylesheet = LoadEmbeddedStylesheet(DarkStylesheetResourceName);
        _lightStylesheet = LoadEmbeddedStylesheet(LightStylesheetResourceName);
    }

    /// <inheritdoc />
    public async Task<RenderResult> RenderAsync(string filePath, CancellationToken cancellationToken)
    {
        var json = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        var html = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var body = $"<pre class=\"json\">{RenderElement(doc.RootElement, 0)}</pre>";
                return WrapInDocument(body);
            }
            catch (JsonException ex)
            {
                return WrapInDocument(BuildErrorBody("Invalid JSON", ex.Message));
            }
        }, cancellationToken).ConfigureAwait(false);

        return new RenderResult.Html(html);
    }

    /// <inheritdoc />
    public RenderResult RenderError(string title, string detail) =>
        new RenderResult.Html(WrapInDocument(BuildErrorBody(title, detail)));

    private static string BuildErrorBody(string title, string detail) =>
        $"""
        <div class="error">
        <h1>{WebUtility.HtmlEncode(title)}</h1>
        <p>{WebUtility.HtmlEncode(detail)}</p>
        </div>
        """;

    private static string RenderElement(JsonElement element, int indent) =>
        element.ValueKind switch
        {
            JsonValueKind.Object  => RenderObject(element, indent),
            JsonValueKind.Array   => RenderArray(element, indent),
            JsonValueKind.String  => Span("json-string", WebUtility.HtmlEncode(element.GetRawText())),
            JsonValueKind.Number  => Span("json-number", element.GetRawText()),
            JsonValueKind.True    => Span("json-bool", "true"),
            JsonValueKind.False   => Span("json-bool", "false"),
            JsonValueKind.Null    => Span("json-null", "null"),
            _                     => WebUtility.HtmlEncode(element.GetRawText()),
        };

    private static string RenderObject(JsonElement element, int indent)
    {
        var properties = element.EnumerateObject().ToList();
        if (properties.Count == 0)
            return Punct("{}");

        var sb = new StringBuilder();
        sb.Append(Punct("{"));
        for (var i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            sb.Append('\n');
            sb.Append(Pad(indent + 1));
            sb.Append(Span("json-key", WebUtility.HtmlEncode(JsonSerializer.Serialize(prop.Name))));
            sb.Append(Punct(": "));
            sb.Append(RenderElement(prop.Value, indent + 1));
            if (i < properties.Count - 1)
                sb.Append(Punct(","));
        }
        sb.Append('\n');
        sb.Append(Pad(indent));
        sb.Append(Punct("}"));
        return sb.ToString();
    }

    private static string RenderArray(JsonElement element, int indent)
    {
        var items = element.EnumerateArray().ToList();
        if (items.Count == 0)
            return Punct("[]");

        var sb = new StringBuilder();
        sb.Append(Punct("["));
        for (var i = 0; i < items.Count; i++)
        {
            sb.Append('\n');
            sb.Append(Pad(indent + 1));
            sb.Append(RenderElement(items[i], indent + 1));
            if (i < items.Count - 1)
                sb.Append(Punct(","));
        }
        sb.Append('\n');
        sb.Append(Pad(indent));
        sb.Append(Punct("]"));
        return sb.ToString();
    }

    private static string Span(string cls, string content) =>
        $"<span class=\"{cls}\">{content}</span>";

    private static string Punct(string text) => Span("json-punctuation", text);

    private static string Pad(int indent) => new string(' ', indent * 2);

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
        var assembly = typeof(JsonRenderer).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found in {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
