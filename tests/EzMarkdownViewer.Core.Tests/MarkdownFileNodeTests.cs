using EzMarkdownViewer.Core;

using FluentAssertions;

namespace EzMarkdownViewer.Core.Tests;

public class MarkdownFileNodeTests
{
    [Theory]
    [InlineData("C:\\root\\notes.md", "notes")]
    [InlineData("C:\\root\\README.md", "README")]
    [InlineData("/root/.hidden.md", ".hidden")]
    [InlineData("/root/some.file.md", "some.file")]
    public void DisplayName_StripsMdExtension(string fullPath, string expected)
    {
        var node = new MarkdownFileNode(fullPath);

        node.DisplayName.Should().Be(expected);
    }

    [Fact]
    public void FullPath_PreservedVerbatim()
    {
        const string path = "C:\\docs\\notes.md";

        var node = new MarkdownFileNode(path);

        node.FullPath.Should().Be(path);
    }
}
