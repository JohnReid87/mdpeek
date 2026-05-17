using MdPeek.Core;

using FluentAssertions;

using NSubstitute;

namespace MdPeek.Core.Tests;

public class JsonRendererTests
{
    private readonly IFileSystem _fs = Substitute.For<IFileSystem>();
    private readonly JsonRenderer _sut;

    public JsonRendererTests()
    {
        _sut = new JsonRenderer(_fs);
    }

    private void GivenContent(string content) =>
        _fs.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(content);

    private static string HtmlOf(RenderResult result) =>
        result.Should().BeOfType<RenderResult.Html>().Which.Content;

    [Fact]
    public void SupportedExtensions_ContainsDotJson()
    {
        _sut.SupportedExtensions.Should().ContainSingle().Which.Should().Be(".json");
    }

    [Fact]
    public async Task RenderAsync_WrapsOutput_InHtmlShellWithEmbeddedStylesheet()
    {
        GivenContent("{}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<style>");
        html.Should().Contain("</style>");
        html.Should().Contain("<body>");
        html.Should().Contain("</html>");
    }

    [Fact]
    public async Task RenderAsync_EmbeddedStylesheet_AppliesDarkThemeByDefault()
    {
        GivenContent("{}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("--bg: #0d1117");
    }

    [Fact]
    public async Task RenderAsync_WhenIsDarkThemeIsFalse_AppliesLightTheme()
    {
        _sut.IsDarkTheme = false;
        GivenContent("{}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("--bg: #ffffff");
        html.Should().NotContain("--bg: #0d1117");
    }

    [Fact]
    public async Task RenderAsync_EmptyObject_RendersEmptyBraces()
    {
        GivenContent("{}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("class=\"json-punctuation\"");
        html.Should().Contain("{}");
    }

    [Fact]
    public async Task RenderAsync_EmptyArray_RendersEmptyBrackets()
    {
        GivenContent("[]");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("class=\"json-punctuation\"");
        html.Should().Contain("[]");
    }

    [Fact]
    public async Task RenderAsync_StringValue_RendersWithJsonStringSpan()
    {
        GivenContent("{\"key\": \"hello\"}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("<span class=\"json-string\">");
    }

    [Fact]
    public async Task RenderAsync_NumberValue_RendersWithJsonNumberSpan()
    {
        GivenContent("{\"key\": 42}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<span class=\"json-number\">42</span>");
    }

    [Fact]
    public async Task RenderAsync_BooleanTrue_RendersWithJsonBoolSpan()
    {
        GivenContent("{\"key\": true}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("<span class=\"json-bool\">true</span>");
    }

    [Fact]
    public async Task RenderAsync_BooleanFalse_RendersWithJsonBoolSpan()
    {
        GivenContent("{\"key\": false}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("<span class=\"json-bool\">false</span>");
    }

    [Fact]
    public async Task RenderAsync_NullValue_RendersWithJsonNullSpan()
    {
        GivenContent("{\"key\": null}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("<span class=\"json-null\">null</span>");
    }

    [Fact]
    public async Task RenderAsync_ObjectKey_RendersWithJsonKeySpan()
    {
        GivenContent("{\"myKey\": 1}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<span class=\"json-key\">");
        html.Should().Contain("myKey");
    }

    [Fact]
    public async Task RenderAsync_ColonSeparator_RendersWithJsonPunctuationSpan()
    {
        GivenContent("{\"k\": 1}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("<span class=\"json-punctuation\">: </span>");
    }

    [Fact]
    public async Task RenderAsync_MultipleProperties_RendersCommaWithJsonPunctuationSpan()
    {
        GivenContent("{\"a\": 1, \"b\": 2}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        HtmlOf(result).Should().Contain("<span class=\"json-punctuation\">,</span>");
    }

    [Fact]
    public async Task RenderAsync_StringValueContainingHtmlChars_HtmlEncodesContent()
    {
        GivenContent("{\"key\": \"<tag> & more\"}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("&lt;tag&gt;");
        html.Should().Contain("&amp;");
        html.Should().NotContain("<tag>");
    }

    [Fact]
    public async Task RenderAsync_NestedObject_RendersIndented()
    {
        GivenContent("{\"outer\": {\"inner\": 1}}");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("outer");
        html.Should().Contain("inner");
        html.Should().Contain("<span class=\"json-number\">1</span>");
    }

    [Fact]
    public async Task RenderAsync_ArrayOfValues_RendersAllElements()
    {
        GivenContent("[1, \"two\", true, null]");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<span class=\"json-number\">1</span>");
        html.Should().Contain("<span class=\"json-string\">");
        html.Should().Contain("<span class=\"json-bool\">true</span>");
        html.Should().Contain("<span class=\"json-null\">null</span>");
    }

    [Fact]
    public async Task RenderAsync_InvalidJson_RendersErrorPage()
    {
        GivenContent("{ not valid json ]");

        var result = await _sut.RenderAsync("test.json", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("class=\"error\"");
        html.Should().Contain("Invalid JSON");
    }

    [Fact]
    public void RenderError_WrapsMessage_InHtmlShellWithEmbeddedStylesheet()
    {
        var result = _sut.RenderError("File not found", "No such file.");

        var html = HtmlOf(result);
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<style>");
        html.Should().Contain("--bg: #0d1117");
        html.Should().Contain("class=\"error\"");
        html.Should().Contain("File not found");
        html.Should().Contain("No such file.");
    }

    [Fact]
    public void RenderError_HtmlEncodesUserSuppliedText()
    {
        var result = _sut.RenderError("<bad>", "1 < 2 & 3 > 0");

        var html = HtmlOf(result);
        html.Should().Contain("&lt;bad&gt;");
        html.Should().Contain("1 &lt; 2 &amp; 3 &gt; 0");
        html.Should().NotContain("<bad>");
    }

    [Fact]
    public void RenderError_WhenIsDarkThemeIsFalse_AppliesLightTheme()
    {
        _sut.IsDarkTheme = false;

        var result = _sut.RenderError("Not found", "Gone.");

        var html = HtmlOf(result);
        html.Should().Contain("--bg: #ffffff");
        html.Should().NotContain("--bg: #0d1117");
    }
}
