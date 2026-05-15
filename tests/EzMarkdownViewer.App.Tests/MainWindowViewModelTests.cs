using EzMarkdownViewer.App;
using EzMarkdownViewer.Core;

using FluentAssertions;

using NSubstitute;

namespace EzMarkdownViewer.App.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void OpenFolder_WhenUserPicksFolder_SetsRootNodeForChosenPath()
    {
        var picker = Substitute.For<IFolderPicker>();
        var fs = Substitute.For<IFileSystem>();
        picker.PickFolder().Returns("C:\\notes\\design");
        var vm = new MainWindowViewModel(picker, fs);

        vm.OpenFolderCommand.Execute(null);

        vm.RootNode.Should().NotBeNull();
        vm.RootNode!.FullPath.Should().Be("C:\\notes\\design");
    }

    [Fact]
    public void OpenFolder_WhenUserPicksFolder_PutsFolderNameInWindowTitle()
    {
        var picker = Substitute.For<IFolderPicker>();
        var fs = Substitute.For<IFileSystem>();
        picker.PickFolder().Returns("C:\\notes\\design");
        var vm = new MainWindowViewModel(picker, fs);

        vm.OpenFolderCommand.Execute(null);

        vm.WindowTitle.Should().Be("design — ez-markdown-viewer");
    }

    [Fact]
    public void OpenFolder_WhenUserPicksFolder_PopulatesRootsWithSingleEntry()
    {
        var picker = Substitute.For<IFolderPicker>();
        var fs = Substitute.For<IFileSystem>();
        picker.PickFolder().Returns("C:\\notes");
        var vm = new MainWindowViewModel(picker, fs);

        vm.OpenFolderCommand.Execute(null);

        vm.Roots.Should().ContainSingle().Which.FullPath.Should().Be("C:\\notes");
    }

    [Fact]
    public void OpenFolder_WhenUserCancels_LeavesStateUnchanged()
    {
        var picker = Substitute.For<IFolderPicker>();
        var fs = Substitute.For<IFileSystem>();
        picker.PickFolder().Returns((string?)null);
        var vm = new MainWindowViewModel(picker, fs);

        vm.OpenFolderCommand.Execute(null);

        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("ez-markdown-viewer");
        vm.Roots.Should().BeEmpty();
    }

    [Fact]
    public void HasFolderOpen_IsFalse_BeforeOpeningAFolder()
    {
        var vm = new MainWindowViewModel(Substitute.For<IFolderPicker>(), Substitute.For<IFileSystem>());

        vm.HasFolderOpen.Should().BeFalse();
        vm.HasNoFolderOpen.Should().BeTrue();
    }

    [Fact]
    public void HasFolderOpen_IsTrue_AfterOpeningAFolder()
    {
        var picker = Substitute.For<IFolderPicker>();
        var fs = Substitute.For<IFileSystem>();
        picker.PickFolder().Returns("C:\\notes");
        var vm = new MainWindowViewModel(picker, fs);

        vm.OpenFolderCommand.Execute(null);

        vm.HasFolderOpen.Should().BeTrue();
        vm.HasNoFolderOpen.Should().BeFalse();
    }

    [Fact]
    public void OpenFolder_RaisesPropertyChanged_ForDerivedTreeProperties()
    {
        var picker = Substitute.For<IFolderPicker>();
        var fs = Substitute.For<IFileSystem>();
        picker.PickFolder().Returns("C:\\notes");
        var vm = new MainWindowViewModel(picker, fs);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.OpenFolderCommand.Execute(null);

        changed.Should().Contain(new[] { nameof(vm.RootNode), nameof(vm.Roots), nameof(vm.HasFolderOpen), nameof(vm.HasNoFolderOpen) });
    }
}
