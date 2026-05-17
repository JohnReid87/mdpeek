using System.Text;

using MdPeek.Core;

using FluentAssertions;

using NSubstitute;

namespace MdPeek.Core.Tests;

public class PlainTextRendererTests
{
    private readonly IFileSystem _fs = Substitute.For<IFileSystem>();
    private readonly PlainTextRenderer _sut;

    public PlainTextRendererTests()
    {
        _sut = new PlainTextRenderer(_fs);
    }

    private void GivenBytes(byte[] bytes) =>
        _fs.ReadAllBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bytes);

    private void GivenUtf8Content(string content) =>
        GivenBytes(Encoding.UTF8.GetBytes(content));

    private static string HtmlOf(RenderResult result) =>
        result.Should().BeOfType<RenderResult.Html>().Which.Content;

    [Fact]
    public void SupportedExtensions_ContainsDotTxt()
    {
        _sut.SupportedExtensions.Should().ContainSingle().Which.Should().Be(".txt");
    }

    [Fact]
    public async Task RenderAsync_WrapsOutput_InHtmlShellWithEmbeddedStylesheet()
    {
        GivenUtf8Content("hello");

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<style>");
        html.Should().Contain("</style>");
        html.Should().Contain("<body>");
        html.Should().Contain("</html>");
    }

    [Fact]
    public async Task RenderAsync_WrapsContent_InPreTag()
    {
        GivenUtf8Content("hello world");

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<pre class=\"plaintext\">");
        html.Should().Contain("hello world");
        html.Should().Contain("</pre>");
    }

    [Fact]
    public async Task RenderAsync_EmbeddedStylesheet_AppliesDarkTheme()
    {
        GivenUtf8Content(string.Empty);

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("--bg: #0d1117");
        html.Should().Contain("background-color: var(--bg)");
    }

    [Fact]
    public async Task RenderAsync_WhenIsDarkThemeIsFalse_AppliesLightTheme()
    {
        _sut.IsDarkTheme = false;
        GivenUtf8Content(string.Empty);

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("--bg: #ffffff");
        html.Should().NotContain("--bg: #0d1117");
    }

    [Fact]
    public async Task RenderAsync_Utf8BomContent_StripsBomAndRendersText()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var payload = Encoding.UTF8.GetBytes("BOM file content");
        GivenBytes([.. bom, .. payload]);

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("BOM file content");
        html.Should().NotContain("﻿");
    }

    [Fact]
    public async Task RenderAsync_WithNonUtf8Bytes_FallsBackToSystemDefaultEncoding()
    {
        // 0x96 is an en dash in Windows-1252 and is not a valid UTF-8 byte,
        // so the strict-UTF-8 attempt will throw and the fallback path runs.
        var bytes = new byte[] { (byte)'H', (byte)'i', 0x96 };
        GivenBytes(bytes);

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain(Encoding.Default.GetString(bytes));
    }

    [Theory]
    [InlineData("<tag>", "&lt;tag&gt;")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("1 < 2 > 0", "1 &lt; 2 &gt; 0")]
    public async Task RenderAsync_HtmlEncodesSpecialCharacters_InContent(string input, string expectedEncoded)
    {
        GivenUtf8Content(input);

        var result = await _sut.RenderAsync("test.txt", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain(expectedEncoded);
        html.Should().NotContain(input);
    }

    [Fact]
    public void RenderError_WrapsMessage_InHtmlShellWithEmbeddedStylesheet()
    {
        var result = _sut.RenderError("File not found", "It was not there.");

        var html = HtmlOf(result);
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<style>");
        html.Should().Contain("--bg: #0d1117");
        html.Should().Contain("class=\"error\"");
        html.Should().Contain("File not found");
        html.Should().Contain("It was not there.");
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

        var result = _sut.RenderError("Not found", "File gone.");

        HtmlOf(result).Should().Contain("--bg: #ffffff");
        HtmlOf(result).Should().NotContain("--bg: #0d1117");
    }
}
