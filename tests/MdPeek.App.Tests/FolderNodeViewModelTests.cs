using MdPeek.App;
using MdPeek.Core;

using FluentAssertions;

using NSubstitute;

namespace MdPeek.App.Tests;

public class FolderNodeViewModelTests
{
    [Fact]
    public void IsExpanded_DefaultsToFalse()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        vm.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Children_WrapsCoreChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(new[] { "/r/notes.md" });
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        var children = vm.Children;

        children.Should().HaveCount(2);
        children[0].Should().BeOfType<FolderNodeViewModel>();
        children[0].FullPath.Should().Be("/r/sub");
        children[1].Should().BeOfType<MarkdownFileNodeViewModel>();
        children[1].FullPath.Should().Be("/r/notes.md");
    }

    [Fact]
    public void Children_CachedAfterFirstAccess()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        var first = vm.Children;
        var second = vm.Children;

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void LoadedChildren_BeforeChildrenAccessed_IsNull()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        vm.LoadedChildren.Should().BeNull();
    }

    [Fact]
    public void LoadedChildren_BeforeChildrenAccessed_DoesNotTriggerDiskRead()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        _ = vm.LoadedChildren;

        fs.DidNotReceive().EnumerateDirectories(Arg.Any<string>());
        fs.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public void LoadedChildren_AfterChildrenAccessed_ReturnsSameInstance()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        var children = vm.Children;

        vm.LoadedChildren.Should().BeSameAs(children);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetExpandedRecursive_SetsIsExpandedOnSelf(bool value)
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs)) { IsExpanded = !value };

        vm.SetExpandedRecursive(value);

        vm.IsExpanded.Should().Be(value);
    }

    [Fact]
    public void SetExpandedRecursive_DoesNotForceLoadChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs));

        vm.SetExpandedRecursive(true);

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
        var root = new FolderNodeViewModel(new FolderNode("/r", fs));
        var a = (FolderNodeViewModel)root.Children.Single();
        var b = (FolderNodeViewModel)a.Children.Single();

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
        var root = new FolderNodeViewModel(new FolderNode("/r", fs));
        _ = root.Children;

        root.SetExpandedRecursive(true);

        fs.DidNotReceive().EnumerateDirectories("/r/a");
    }
}
