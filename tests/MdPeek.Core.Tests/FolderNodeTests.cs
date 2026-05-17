using MdPeek.Core;

using FluentAssertions;

using NSubstitute;

namespace MdPeek.Core.Tests;

public class FolderNodeTests
{
    private static readonly IReadOnlyList<string> MdPatterns = ["*.md"];

    [Theory]
    [InlineData("C:\\root\\subfolder", "subfolder")]
    [InlineData("C:\\root\\subfolder\\", "subfolder")]
    [InlineData("/root/sub", "sub")]
    public void DisplayName_IsLastPathSegment(string fullPath, string expected)
    {
        var fs = Substitute.For<IFileSystem>();

        var node = new FolderNode(fullPath, fs, MdPatterns);

        node.DisplayName.Should().Be(expected);
    }

    [Fact]
    public void Children_NotEnumerated_UntilAccessed()
    {
        var fs = Substitute.For<IFileSystem>();
        _ = new FolderNode("/root", fs, MdPatterns);

        fs.DidNotReceive().EnumerateDirectories(Arg.Any<string>());
        fs.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public void Children_CachedAfterFirstAccess()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/root").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/root", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/root", fs, MdPatterns);

        _ = node.Children;
        _ = node.Children;

        fs.Received(1).EnumerateDirectories("/root");
        fs.Received(1).EnumerateFiles("/root", "*.md", SearchOption.TopDirectoryOnly);
    }

    [Fact]
    public void Children_PlacesFoldersFirst_ThenFiles_EachSortedAlphabetically()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/zeta", "/r/alpha" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(new[] { "/r/zoom.md", "/r/aardvark.md" });
        var node = new FolderNode("/r", fs, MdPatterns);

        var names = node.Children.Select(c => c.DisplayName).ToArray();

        names.Should().Equal("alpha", "zeta", "aardvark.md", "zoom.md");
    }

    [Fact]
    public void Children_AlphabeticalOrdering_IsCaseInsensitive()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly)
            .Returns(new[] { "/r/Zebra.md", "/r/apple.md", "/r/Banana.md" });
        var node = new FolderNode("/r", fs, MdPatterns);

        var names = node.Children.Select(c => c.DisplayName).ToArray();

        names.Should().Equal("apple.md", "Banana.md", "Zebra.md");
    }

    [Fact]
    public void Children_IncludesEmptyFolders_RegardlessOfMarkdownContent()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/empty", "/r/has-md" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs, MdPatterns);

        var names = node.Children.Select(c => c.DisplayName).ToArray();

        names.Should().Equal("empty", "has-md");
    }

    [Fact]
    public void Children_FolderWithNoMarkdownAnywhere_ReturnsEmpty()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs, MdPatterns);

        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void Children_NestedFolders_LoadLazilyOnExpansion()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs, MdPatterns);

        _ = node.Children;

        fs.DidNotReceive().EnumerateDirectories("/r/sub");
        fs.DidNotReceive().EnumerateFiles("/r/sub", "*.md", SearchOption.TopDirectoryOnly);
    }

    [Fact]
    public void LoadedChildren_BeforeChildrenAccessed_IsNull()
    {
        var fs = Substitute.For<IFileSystem>();
        var node = new FolderNode("/r", fs, MdPatterns);

        node.LoadedChildren.Should().BeNull();
    }

    [Fact]
    public void LoadedChildren_AfterChildrenAccessed_ReturnsSameInstance()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs, MdPatterns);

        var children = node.Children;

        node.LoadedChildren.Should().BeSameAs(children);
    }

    [Fact]
    public void Children_WithMultiplePatterns_UnionsResultsAndDeduplicated()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(new[] { "/r/a.md" });
        fs.EnumerateFiles("/r", "*.markdown", SearchOption.TopDirectoryOnly).Returns(new[] { "/r/b.markdown" });
        var node = new FolderNode("/r", fs, ["*.md", "*.markdown"]);

        var names = node.Children.Select(c => c.DisplayName).ToArray();

        names.Should().BeEquivalentTo("a.md", "b.markdown");
    }
}
