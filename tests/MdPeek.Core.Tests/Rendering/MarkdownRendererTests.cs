
using FluentAssertions;

using NSubstitute;

namespace MdPeek.Core.Tests.Rendering;

public class MarkdownRendererTests
{
    private readonly IFileSystem _fs = Substitute.For<IFileSystem>();
    private readonly MarkdownRenderer _sut;

    public MarkdownRendererTests()
    {
        _sut = new MarkdownRenderer(_fs);
    }

    private void GivenContent(string content) =>
        _fs.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(content);

    private static string HtmlOf(RenderResult result) =>
        result.Should().BeOfType<RenderResult.Html>().Which.Content;

    [Fact]
    public async Task RenderAsync_WrapsOutput_InHtmlShellWithEmbeddedStylesheet()
    {
        GivenContent("# hi");

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<head>");
        html.Should().Contain("<style>");
        html.Should().Contain("</style>");
        html.Should().Contain("<body>");
        html.Should().Contain("</html>");
    }

    [Fact]
    public async Task RenderAsync_EmbeddedStylesheet_AppliesDarkTheme()
    {
        GivenContent(string.Empty);

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("--bg: #0d1117");
        html.Should().Contain("background-color: var(--bg)");
    }

    [Fact]
    public async Task RenderAsync_WhenIsDarkThemeIsFalse_AppliesLightTheme()
    {
        _sut.IsDarkTheme = false;
        GivenContent(string.Empty);

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("--bg: #ffffff");
        html.Should().Contain("background-color: var(--bg)");
        html.Should().NotContain("--bg: #0d1117");
    }

    [Fact]
    public void RenderError_WhenIsDarkThemeIsFalse_AppliesLightTheme()
    {
        _sut.IsDarkTheme = false;

        var result = _sut.RenderError("Not found", "File gone.");

        HtmlOf(result).Should().Contain("--bg: #ffffff");
        HtmlOf(result).Should().NotContain("--bg: #0d1117");
    }

    [Theory]
    [InlineData("# Heading 1", "<h1")]
    [InlineData("## Heading 2", "<h2")]
    [InlineData("**bold**", "<strong>bold</strong>")]
    [InlineData("*italic*", "<em>italic</em>")]
    [InlineData("`code`", "<code>code</code>")]
    [InlineData("[link](https://example.com)", "href=\"https://example.com\"")]
    [InlineData("> a quote", "<blockquote>")]
    [InlineData("- item one\n- item two", "<ul>")]
    [InlineData("1. first\n2. second", "<ol>")]
    public async Task RenderAsync_CommonMarkdownConstructs_ProduceExpectedHtml(string markdown, string expectedFragment)
    {
        GivenContent(markdown);

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        HtmlOf(result).Should().Contain(expectedFragment);
    }

    [Fact]
    public async Task RenderAsync_FencedCodeBlockWithLanguage_PreservesLanguageClass()
    {
        GivenContent("```csharp\nvar x = 1;\n```");

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<pre>");
        html.Should().Contain("language-csharp");
    }

    [Fact]
    public async Task RenderAsync_AdvancedExtensions_RenderPipeTables()
    {
        GivenContent("| a | b |\n|---|---|\n| 1 | 2 |");

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        var html = HtmlOf(result);
        html.Should().Contain("<table>");
        html.Should().Contain("<th>a</th>");
        html.Should().Contain("<td>1</td>");
    }

    [Fact]
    public async Task RenderAsync_AdvancedExtensions_RenderTaskLists()
    {
        GivenContent("- [x] done\n- [ ] todo");

        var result = await _sut.RenderAsync("test.md", CancellationToken.None);

        HtmlOf(result).Should().Contain("type=\"checkbox\"");
    }

    [Fact]
    public async Task RenderAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        GivenContent("# hi");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.RenderAsync("test.md", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
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
    public void SupportedExtensions_ContainsDotMd()
    {
        _sut.SupportedExtensions.Should().ContainSingle().Which.Should().Be(".md");
    }
}
