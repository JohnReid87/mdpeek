using MdPeek.Core;

using FluentAssertions;

using NSubstitute;

namespace MdPeek.Core.Tests;

public class FolderNodeTests
{
    [Theory]
    [InlineData("C:\\root\\subfolder", "subfolder")]
    [InlineData("C:\\root\\subfolder\\", "subfolder")]
    [InlineData("/root/sub", "sub")]
    public void DisplayName_IsLastPathSegment(string fullPath, string expected)
    {
        var fs = Substitute.For<IFileSystem>();

        var node = new FolderNode(fullPath, fs);

        node.DisplayName.Should().Be(expected);
    }

    [Fact]
    public void Children_NotEnumerated_UntilAccessed()
    {
        var fs = Substitute.For<IFileSystem>();
        _ = new FolderNode("/root", fs);

        fs.DidNotReceive().EnumerateDirectories(Arg.Any<string>());
        fs.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public void Children_CachedAfterFirstAccess()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/root").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/root", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/root", fs);

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
        var node = new FolderNode("/r", fs);

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
        var node = new FolderNode("/r", fs);

        var names = node.Children.Select(c => c.DisplayName).ToArray();

        names.Should().Equal("apple.md", "Banana.md", "Zebra.md");
    }

    [Fact]
    public void Children_IncludesEmptyFolders_RegardlessOfMarkdownContent()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/empty", "/r/has-md" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs);

        var names = node.Children.Select(c => c.DisplayName).ToArray();

        names.Should().Equal("empty", "has-md");
    }

    [Fact]
    public void Children_FolderWithNoMarkdownAnywhere_ReturnsEmpty()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs);

        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void Children_NestedFolders_LoadLazilyOnExpansion()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var node = new FolderNode("/r", fs);

        _ = node.Children;

        fs.DidNotReceive().EnumerateDirectories("/r/sub");
        fs.DidNotReceive().EnumerateFiles("/r/sub", "*.md", SearchOption.TopDirectoryOnly);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetExpandedRecursive_SetsIsExpandedOnSelf(bool value)
    {
        var fs = Substitute.For<IFileSystem>();
        var node = new FolderNode("/r", fs) { IsExpanded = !value };

        node.SetExpandedRecursive(value);

        node.IsExpanded.Should().Be(value);
    }

    [Fact]
    public void SetExpandedRecursive_DoesNotForceLoadChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        var node = new FolderNode("/r", fs);

        node.SetExpandedRecursive(true);

        fs.DidNotReceive().EnumerateDirectories(Arg.Any<string>());
        fs.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public void SetExpandedRecursive_AppliesToAllLoadedDescendantFolders()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/a" });
        fs.EnumerateDirectories("/r/a").Returns(new[] { "/r/a/b" });
        fs.EnumerateDirectories("/r/a/b").Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var root = new FolderNode("/r", fs);
        var a = (FolderNode)root.Children.Single();
        var b = (FolderNode)a.Children.Single();

        root.SetExpandedRecursive(true);

        root.IsExpanded.Should().BeTrue();
        a.IsExpanded.Should().BeTrue();
        b.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void SetExpandedRecursive_DoesNotTouchUnloadedDescendants()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/a" });
        fs.EnumerateFiles(Arg.Any<string>(), "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var root = new FolderNode("/r", fs);
        _ = root.Children;

        root.SetExpandedRecursive(true);

        fs.DidNotReceive().EnumerateDirectories("/r/a");
    }
}
