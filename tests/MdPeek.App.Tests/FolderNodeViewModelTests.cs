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
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_DefaultsToFalse()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void Children_WrapsCoreChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(new[] { "/r/notes.md" });
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        var children = vm.Children;

        children.Should().HaveCount(2);
        children[0].Should().BeOfType<FolderNodeViewModel>();
        children[0].FullPath.Should().Be("/r/sub");
        children[1].Should().BeOfType<DocumentFileNodeViewModel>();
        children[1].FullPath.Should().Be("/r/notes.md");
    }

    [Fact]
    public void Children_CachedAfterFirstAccess()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        var first = vm.Children;
        var second = vm.Children;

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void LoadedChildren_BeforeChildrenAccessed_IsNull()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.LoadedChildren.Should().BeNull();
    }

    [Fact]
    public void LoadedChildren_BeforeChildrenAccessed_DoesNotTriggerDiskRead()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

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
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        var children = vm.Children;

        vm.LoadedChildren.Should().BeSameAs(children);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetExpandedRecursive_SetsIsExpandedOnSelf(bool value)
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"])) { IsExpanded = !value };

        vm.SetExpandedRecursive(value);

        vm.IsExpanded.Should().Be(value);
    }

    [Fact]
    public void SetExpandedRecursive_DoesNotForceLoadChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

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
        var root = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));
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
        var root = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));
        _ = root.Children;

        root.SetExpandedRecursive(true);

        fs.DidNotReceive().EnumerateDirectories("/r/a");
    }

    [Fact]
    public void IsLoading_DefaultsToFalse()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void DisplayChildren_BeforeLoading_ContainsPlaceholder()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.DisplayChildren.Should().ContainSingle()
            .Which.Should().BeOfType<LoadingPlaceholderViewModel>();
    }

    [Fact]
    public void DisplayChildren_PlaceholderDoesNotTriggerDiskRead()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        _ = vm.DisplayChildren;

        fs.DidNotReceive().EnumerateDirectories(Arg.Any<string>());
        fs.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public async Task LoadChildrenAsync_ReplacesPlaceholderWithWrappedChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(new[] { "/r/notes.md" });
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        await vm.LoadChildrenAsync();

        vm.DisplayChildren.Should().HaveCount(2);
        vm.DisplayChildren[0].Should().BeOfType<FolderNodeViewModel>();
        vm.DisplayChildren[0].FullPath.Should().Be("/r/sub");
        vm.DisplayChildren[1].Should().BeOfType<DocumentFileNodeViewModel>();
        vm.DisplayChildren[1].FullPath.Should().Be("/r/notes.md");
    }

    [Fact]
    public async Task LoadChildrenAsync_TogglesIsLoading()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));
        var observed = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FolderNodeViewModel.IsLoading))
            {
                observed.Add(vm.IsLoading);
            }
        };

        await vm.LoadChildrenAsync();

        observed.Should().Equal(true, false);
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadChildrenAsync_PopulatesLoadedChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        await vm.LoadChildrenAsync();

        vm.LoadedChildren.Should().NotBeNull();
        vm.LoadedChildren!.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadChildrenAsync_CalledTwice_OnlyEnumeratesOnce()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        await vm.LoadChildrenAsync();
        await vm.LoadChildrenAsync();

        fs.Received(1).EnumerateDirectories("/r");
    }

    [Fact]
    public async Task IsExpanded_FirstTimeTrue_TriggersBackgroundLoad()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.IsExpanded = true;
        // Drain the load that OnIsExpandedChanged kicked off.
        await vm.LoadChildrenAsync();

        vm.LoadedChildren.Should().NotBeNull();
        vm.DisplayChildren.Should().ContainSingle()
            .Which.FullPath.Should().Be("/r/sub");
    }

    [Fact]
    public void IsExpanded_FalseToFalse_DoesNotTriggerLoad()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        vm.IsExpanded = false;

        vm.IsLoading.Should().BeFalse();
        fs.DidNotReceive().EnumerateDirectories(Arg.Any<string>());
    }

    [Fact]
    public async Task IsExpanded_AfterLoadComplete_DoesNotReload()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(Array.Empty<string>());
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        await vm.LoadChildrenAsync();
        vm.IsExpanded = true;

        fs.Received(1).EnumerateDirectories("/r");
    }

    [Fact]
    public void Children_SyncAccess_PopulatesDisplayChildren()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.EnumerateDirectories("/r").Returns(new[] { "/r/sub" });
        fs.EnumerateFiles("/r", "*.md", SearchOption.TopDirectoryOnly).Returns(Array.Empty<string>());
        var vm = new FolderNodeViewModel(new FolderNode("/r", fs, ["*.md"]));

        _ = vm.Children;

        vm.DisplayChildren.Should().ContainSingle()
            .Which.Should().BeOfType<FolderNodeViewModel>();
    }
}
