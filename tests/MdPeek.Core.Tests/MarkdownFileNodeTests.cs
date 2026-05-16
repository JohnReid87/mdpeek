using MdPeek.Core;

using FluentAssertions;

namespace MdPeek.Core.Tests;

public class MarkdownFileNodeTests
{
    [Theory]
    [InlineData("C:\\root\\notes.md", "notes.md")]
    [InlineData("C:\\root\\README.md", "README.md")]
    [InlineData("/root/.hidden.md", ".hidden.md")]
    [InlineData("/root/some.file.md", "some.file.md")]
    public void DisplayName_IsFileNameIncludingExtension(string fullPath, string expected)
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
