using EzMarkdownViewer.Core;

using FluentAssertions;

namespace EzMarkdownViewer.Core.Tests;

public class MarkdownRendererTests
{
    private readonly MarkdownRenderer _sut = new();

    [Fact]
    public void Render_WrapsOutput_InHtmlShellWithEmbeddedStylesheet()
    {
        var result = _sut.Render("# hi");

        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("<html lang=\"en\">");
        result.Should().Contain("<head>");
        result.Should().Contain("<style>");
        result.Should().Contain("</style>");
        result.Should().Contain("<body>");
        result.Should().Contain("</html>");
    }

    [Fact]
    public void Render_EmbeddedStylesheet_AppliesDarkTheme()
    {
        var result = _sut.Render(string.Empty);

        result.Should().Contain("--bg: #0d1117");
        result.Should().Contain("background-color: var(--bg)");
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
    public void Render_CommonMarkdownConstructs_ProduceExpectedHtml(string markdown, string expectedFragment)
    {
        var result = _sut.Render(markdown);

        result.Should().Contain(expectedFragment);
    }

    [Fact]
    public void Render_FencedCodeBlockWithLanguage_PreservesLanguageClass()
    {
        var markdown = "```csharp\nvar x = 1;\n```";

        var result = _sut.Render(markdown);

        result.Should().Contain("<pre>");
        result.Should().Contain("language-csharp");
    }

    [Fact]
    public void Render_AdvancedExtensions_RenderPipeTables()
    {
        var markdown = "| a | b |\n|---|---|\n| 1 | 2 |";

        var result = _sut.Render(markdown);

        result.Should().Contain("<table>");
        result.Should().Contain("<th>a</th>");
        result.Should().Contain("<td>1</td>");
    }

    [Fact]
    public void Render_AdvancedExtensions_RenderTaskLists()
    {
        var markdown = "- [x] done\n- [ ] todo";

        var result = _sut.Render(markdown);

        result.Should().Contain("type=\"checkbox\"");
    }

    [Fact]
    public void RenderError_WrapsMessage_InHtmlShellWithEmbeddedStylesheet()
    {
        var result = _sut.RenderError("File not found", "It was not there.");

        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("<style>");
        result.Should().Contain("--bg: #0d1117");
        result.Should().Contain("class=\"error\"");
        result.Should().Contain("File not found");
        result.Should().Contain("It was not there.");
    }

    [Fact]
    public void RenderError_HtmlEncodesUserSuppliedText()
    {
        var result = _sut.RenderError("<bad>", "1 < 2 & 3 > 0");

        result.Should().Contain("&lt;bad&gt;");
        result.Should().Contain("1 &lt; 2 &amp; 3 &gt; 0");
        result.Should().NotContain("<bad>");
    }
}
