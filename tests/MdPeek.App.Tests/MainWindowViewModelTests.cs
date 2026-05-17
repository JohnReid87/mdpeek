using MdPeek.App;
using MdPeek.Core;

using FluentAssertions;

using NSubstitute;

namespace MdPeek.App.Tests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        IFolderPicker? picker = null,
        IFileSystem? fs = null,
        IMarkdownRenderer? renderer = null,
        IUserConfirmation? confirmation = null,
        IUserNotification? notification = null,
        IFileAssociationRegistrar? registrar = null) =>
        new(
            picker ?? Substitute.For<IFolderPicker>(),
            fs ?? Substitute.For<IFileSystem>(),
            renderer ?? Substitute.For<IMarkdownRenderer>(),
            confirmation ?? Substitute.For<IUserConfirmation>(),
            notification ?? Substitute.For<IUserNotification>(),
            registrar ?? Substitute.For<IFileAssociationRegistrar>());

    [Fact]
    public void OpenFolder_WhenUserPicksFolder_SetsRootNodeForChosenPath()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\notes\\design");
        var vm = CreateViewModel(picker: picker);

        vm.OpenFolderCommand.Execute(null);

        vm.RootNode.Should().NotBeNull();
        vm.RootNode!.FullPath.Should().Be("C:\\notes\\design");
    }

    [Fact]
    public void OpenFolder_WhenUserPicksFolder_PutsFolderNameInWindowTitle()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\notes\\design");
        var vm = CreateViewModel(picker: picker);

        vm.OpenFolderCommand.Execute(null);

        vm.WindowTitle.Should().Be("design — mdpeek");
    }

    [Fact]
    public void OpenFolder_WhenUserPicksFolder_PopulatesRootsWithSingleEntry()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\notes");
        var vm = CreateViewModel(picker: picker);

        vm.OpenFolderCommand.Execute(null);

        vm.Roots.Should().ContainSingle().Which.FullPath.Should().Be("C:\\notes");
    }

    [Fact]
    public void OpenFolder_WhenUserCancels_LeavesStateUnchanged()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns((string?)null);
        var vm = CreateViewModel(picker: picker);

        vm.OpenFolderCommand.Execute(null);

        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("mdpeek");
        vm.Roots.Should().BeEmpty();
    }

    [Fact]
    public void HasFolderOpen_IsFalse_BeforeOpeningAFolder()
    {
        var vm = CreateViewModel();

        vm.HasFolderOpen.Should().BeFalse();
        vm.HasNoFolderOpen.Should().BeTrue();
    }

    [Fact]
    public void HasFolderOpen_IsTrue_AfterOpeningAFolder()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\notes");
        var vm = CreateViewModel(picker: picker);

        vm.OpenFolderCommand.Execute(null);

        vm.HasFolderOpen.Should().BeTrue();
        vm.HasNoFolderOpen.Should().BeFalse();
    }

    [Fact]
    public void OpenFolder_RaisesPropertyChanged_ForDerivedTreeProperties()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\notes");
        var vm = CreateViewModel(picker: picker);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.OpenFolderCommand.Execute(null);

        changed.Should().Contain(new[] { nameof(vm.RootNode), nameof(vm.Roots), nameof(vm.HasFolderOpen), nameof(vm.HasNoFolderOpen) });
    }

    [Fact]
    public void SelectedNode_WhenMarkdownFileSelected_ReadsFileAndSetsRenderedHtml()
    {
        const string path = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(128L);
        fs.ReadAllTextAsync(path, Arg.Any<CancellationToken>()).Returns("# hello");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# hello", Arg.Any<CancellationToken>()).Returns("<html>ok</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().Be("<html>ok</html>");
    }

    [Fact]
    public void SelectedNode_WhenFolderSelected_LeavesHtmlContentUnchanged()
    {
        var fs = Substitute.For<IFileSystem>();
        var renderer = Substitute.For<IMarkdownRenderer>();
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new FolderNodeViewModel(new FolderNode("C:\\notes", fs));

        vm.HtmlContent.Should().BeNull();
        renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, Arg.Any<CancellationToken>());
        renderer.DidNotReceiveWithAnyArgs().RenderError(default!, default!);
    }

    [Fact]
    public void SelectedNode_WhenFileMissing_RendersFileNotFoundError()
    {
        const string path = "C:\\notes\\gone.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(false);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderError("File not found", Arg.Any<string>()).Returns("<html>missing</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().Be("<html>missing</html>");
        fs.DidNotReceive().ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedNode_WhenReadThrowsIoException_RendersReadError()
    {
        const string path = "C:\\notes\\locked.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(64L);
        fs.ReadAllTextAsync(path, Arg.Any<CancellationToken>()).Returns<string>(_ => throw new IOException("locked"));
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderError("Could not read file", Arg.Any<string>()).Returns("<html>ioerr</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().Be("<html>ioerr</html>");
        renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedNode_WhenRendererThrows_RendersParseError()
    {
        const string path = "C:\\notes\\bad.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(64L);
        fs.ReadAllTextAsync(path, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# hi", Arg.Any<CancellationToken>()).Returns<string>(_ => throw new InvalidOperationException("boom"));
        renderer.RenderError("Could not render markdown", Arg.Any<string>()).Returns("<html>parseerr</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().Be("<html>parseerr</html>");
    }

    [Fact]
    public void SelectedNode_WhenLargeFileAndUserConfirms_RendersFile()
    {
        const string path = "C:\\notes\\big.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(6L * 1024 * 1024);
        fs.ReadAllTextAsync(path, Arg.Any<CancellationToken>()).Returns("# big");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# big", Arg.Any<CancellationToken>()).Returns("<html>big</html>");
        var confirmation = Substitute.For<IUserConfirmation>();
        confirmation.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var vm = CreateViewModel(fs: fs, renderer: renderer, confirmation: confirmation);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().Be("<html>big</html>");
        confirmation.Received(1).Confirm(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void SelectedNode_WhenLargeFileAndUserDeclines_LeavesHtmlContentUnchanged()
    {
        const string path = "C:\\notes\\big.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(6L * 1024 * 1024);
        var renderer = Substitute.For<IMarkdownRenderer>();
        var confirmation = Substitute.For<IUserConfirmation>();
        confirmation.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var vm = CreateViewModel(fs: fs, renderer: renderer, confirmation: confirmation);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().BeNull();
        fs.DidNotReceive().ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedNode_WhenSet_SetsIsSelectedOnNewNode()
    {
        const string path = "C:\\notes\\a.md";
        var fs = CreateRenderingFileSystem(path);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        var file = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.SelectedNode = file;

        file.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void SelectedNode_WhenReplaced_ClearsIsSelectedOnPreviousNode()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        var first = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        var second = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.SelectedNode = first;

        vm.SelectedNode = second;

        first.IsSelected.Should().BeFalse();
        second.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void SelectedNode_WhenClearedToNull_ClearsIsSelectedOnPreviousNode()
    {
        const string path = "C:\\notes\\a.md";
        var fs = CreateRenderingFileSystem(path);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        var file = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));
        vm.SelectedNode = file;

        vm.SelectedNode = null;

        file.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void GoBack_SetsIsSelectedOnResolvedFileNode()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        var bNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.SelectedNode = bNode;

        vm.GoBackCommand.Execute(null);

        vm.SelectedNode.Should().NotBeNull();
        vm.SelectedNode!.IsSelected.Should().BeTrue();
        bNode.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void TryOpenFromPath_WhenPathIsMarkdownFile_SetsIsSelectedOnResolvedFileNode()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.TryOpenFromPath(file);

        vm.SelectedNode.Should().NotBeNull();
        vm.SelectedNode!.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void ApplyStartupSettings_WhenLastFolderExists_OpensRootNodeAndSetsTitle()
    {
        const string path = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(path).Returns(true);
        var vm = CreateViewModel(fs: fs);

        vm.ApplyStartupSettings(new AppSettings { LastFolder = path });

        vm.RootNode.Should().NotBeNull();
        vm.RootNode!.FullPath.Should().Be(path);
        vm.WindowTitle.Should().Be("notes — mdpeek");
    }

    [Fact]
    public void ApplyStartupSettings_WhenLastFolderMissing_LeavesRootNodeNull()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("C:\\gone").Returns(false);
        var vm = CreateViewModel(fs: fs);

        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\gone" });

        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("mdpeek");
    }

    [Fact]
    public void ApplyStartupSettings_WhenLastFolderNull_DoesNothing()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = CreateViewModel(fs: fs);

        vm.ApplyStartupSettings(new AppSettings());

        vm.RootNode.Should().BeNull();
    }

    [Fact]
    public void ApplyStartupSettings_WhenLastSelectedFileExists_RendersIt()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# hi", Arg.Any<CancellationToken>()).Returns("<html>restored</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            LastSelectedFile = file,
        });

        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(file);
        vm.HtmlContent.Should().Be("<html>restored</html>");
    }

    [Fact]
    public void ApplyStartupSettings_WhenLastSelectedFileMissing_DoesNotSelect()
    {
        const string folder = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(Arg.Any<string>()).Returns(false);
        var vm = CreateViewModel(fs: fs);

        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            LastSelectedFile = "C:\\notes\\gone.md",
        });

        vm.SelectedNode.Should().BeNull();
        vm.HtmlContent.Should().BeNull();
    }

    [Fact]
    public void PopulateSettingsForSave_WhenNoFolderOpen_LeavesPathsNull()
    {
        var vm = CreateViewModel();
        var settings = new AppSettings();

        vm.PopulateSettingsForSave(settings);

        settings.LastFolder.Should().BeNull();
        settings.LastSelectedFile.Should().BeNull();
        settings.ExpandedFolders.Should().BeEmpty();
    }

    [Fact]
    public void PopulateSettingsForSave_CapturesOpenFolderAndSelectedFile()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html>ok</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder, LastSelectedFile = file });
        var settings = new AppSettings();

        vm.PopulateSettingsForSave(settings);

        settings.LastFolder.Should().Be(folder);
        settings.LastSelectedFile.Should().Be(file);
    }

    [Fact]
    public void PopulateSettingsForSave_WhenFolderSelected_DoesNotSetLastSelectedFile()
    {
        const string folder = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder });
        vm.SelectedNode = vm.RootNode;
        var settings = new AppSettings();

        vm.PopulateSettingsForSave(settings);

        settings.LastSelectedFile.Should().BeNull();
    }

    [Fact]
    public void ApplyStartupSettings_ExpandsListedFolders()
    {
        const string folder = "C:\\notes";
        var sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        fs.EnumerateFiles(sub, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);

        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            ExpandedFolders = new List<string> { folder, sub },
        });

        vm.RootNode!.IsExpanded.Should().BeTrue();
        var child = vm.RootNode.Children.OfType<FolderNodeViewModel>().Single();
        child.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void PopulateSettingsForSave_CapturesExpandedFolders()
    {
        const string folder = "C:\\notes";
        var sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        fs.EnumerateFiles(sub, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            ExpandedFolders = new List<string> { folder, sub },
        });
        var settings = new AppSettings();

        vm.PopulateSettingsForSave(settings);

        settings.ExpandedFolders.Should().BeEquivalentTo(new[] { folder, sub });
    }

    [Fact]
    public void Refresh_WhenNoFolderOpen_DoesNothing()
    {
        var vm = CreateViewModel();

        vm.RefreshCommand.Execute(null);

        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("mdpeek");
    }

    [Fact]
    public void Refresh_RebuildsTreeFromDisk()
    {
        const string folder = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { "C:\\notes\\a.md" }, new[] { "C:\\notes\\a.md", "C:\\notes\\b.md" });
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            ExpandedFolders = new List<string> { folder },
        });
        vm.RootNode!.Children.Should().HaveCount(1);

        vm.RefreshCommand.Execute(null);

        vm.RootNode!.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Refresh_PreservesExpandedFoldersThatStillExist()
    {
        const string folder = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        fs.EnumerateFiles(sub, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            ExpandedFolders = new List<string> { folder, sub },
        });

        vm.RefreshCommand.Execute(null);

        vm.RootNode!.IsExpanded.Should().BeTrue();
        vm.RootNode.Children.OfType<FolderNodeViewModel>().Single().IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void Refresh_DoesNotExpandFoldersThatNoLongerExistOnDisk()
    {
        const string folder = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub }, Array.Empty<string>());
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            ExpandedFolders = new List<string> { folder, sub },
        });

        vm.RefreshCommand.Execute(null);

        vm.RootNode!.IsExpanded.Should().BeTrue();
        vm.RootNode.Children.Should().BeEmpty();
    }

    [Fact]
    public void Refresh_WhenSelectedFileStillExists_ReRendersIt()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# v1", "# v2");
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { file });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# v1", Arg.Any<CancellationToken>()).Returns("<html>v1</html>");
        renderer.RenderAsync("# v2", Arg.Any<CancellationToken>()).Returns("<html>v2</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder, LastSelectedFile = file });
        vm.HtmlContent.Should().Be("<html>v1</html>");

        vm.RefreshCommand.Execute(null);

        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(file);
        vm.HtmlContent.Should().Be("<html>v2</html>");
    }

    [Fact]
    public void Refresh_WhenSelectedFileNoLongerExists_ClearsSelectionAndContent()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true, false);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { file }, Array.Empty<string>());
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html>hi</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder, LastSelectedFile = file });

        vm.RefreshCommand.Execute(null);

        vm.SelectedNode.Should().BeNull();
        vm.HtmlContent.Should().BeNull();
    }

    [Fact]
    public void Refresh_WhenRootFolderNoLongerExists_ClearsState()
    {
        const string folder = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true, false);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder });

        vm.RefreshCommand.Execute(null);

        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("mdpeek");
        vm.HtmlContent.Should().BeNull();
    }

    [Fact]
    public void TryOpenFromPath_WhenPathIsFolder_SetsRootNodeAndTitle()
    {
        const string path = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(path).Returns(true);
        var vm = CreateViewModel(fs: fs);

        var opened = vm.TryOpenFromPath(path);

        opened.Should().BeTrue();
        vm.RootNode.Should().NotBeNull();
        vm.RootNode!.FullPath.Should().Be(path);
        vm.WindowTitle.Should().Be("design — mdpeek");
    }

    [Fact]
    public void TryOpenFromPath_WhenPathIsMarkdownFile_OpensParentAndPreSelectsFile()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.DirectoryExists(file).Returns(false);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# hi", Arg.Any<CancellationToken>()).Returns("<html>arg</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        var opened = vm.TryOpenFromPath(file);

        opened.Should().BeTrue();
        vm.RootNode!.FullPath.Should().Be(folder);
        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(file);
        vm.HtmlContent.Should().Be("<html>arg</html>");
    }

    [Fact]
    public void TryOpenFromPath_WhenPathIsMarkdownFile_UsesParentFolderInTitle()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.TryOpenFromPath(file);

        vm.WindowTitle.Should().Be("notes — mdpeek");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryOpenFromPath_WhenPathBlank_ReturnsFalseAndLeavesStateUnchanged(string? path)
    {
        var vm = CreateViewModel();

        var opened = vm.TryOpenFromPath(path);

        opened.Should().BeFalse();
        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("mdpeek");
    }

    [Fact]
    public void TryOpenFromPath_WhenPathDoesNotExist_ReturnsFalse()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(Arg.Any<string>()).Returns(false);
        fs.FileExists(Arg.Any<string>()).Returns(false);
        var vm = CreateViewModel(fs: fs);

        var opened = vm.TryOpenFromPath("C:\\nope\\missing.md");

        opened.Should().BeFalse();
        vm.RootNode.Should().BeNull();
    }

    [Fact]
    public void TryOpenFromPath_WhenFileIsNotMarkdown_ReturnsFalse()
    {
        const string file = "C:\\notes\\readme.txt";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(Arg.Any<string>()).Returns(false);
        fs.FileExists(file).Returns(true);
        var vm = CreateViewModel(fs: fs);

        var opened = vm.TryOpenFromPath(file);

        opened.Should().BeFalse();
        vm.RootNode.Should().BeNull();
    }

    [Fact]
    public void TryOpenFromPath_AcceptsUppercaseMdExtension()
    {
        const string folder = "C:\\notes";
        const string file = "C:\\notes\\README.MD";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        var opened = vm.TryOpenFromPath(file);

        opened.Should().BeTrue();
        vm.RootNode!.FullPath.Should().Be(folder);
    }

    [Fact]
    public void SelectedNode_WhenFileAtThreshold_DoesNotPromptForConfirmation()
    {
        const string path = "C:\\notes\\edge.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(5L * 1024 * 1024);
        fs.ReadAllTextAsync(path, Arg.Any<CancellationToken>()).Returns("# edge");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# edge", Arg.Any<CancellationToken>()).Returns("<html>edge</html>");
        var confirmation = Substitute.For<IUserConfirmation>();
        var vm = CreateViewModel(fs: fs, renderer: renderer, confirmation: confirmation);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.HtmlContent.Should().Be("<html>edge</html>");
        confirmation.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
    }

    [Fact]
    public void RegisterFileAssociation_InvokesRegistrarAndNotifiesOnSuccess()
    {
        var registrar = Substitute.For<IFileAssociationRegistrar>();
        var notification = Substitute.For<IUserNotification>();
        var vm = CreateViewModel(registrar: registrar, notification: notification);

        vm.RegisterFileAssociationCommand.Execute(null);

        registrar.Received(1).Register();
        notification.Received(1).Notify("Registered", Arg.Any<string>());
    }

    [Fact]
    public void RegisterFileAssociation_WhenRegistrarThrows_NotifiesFailureAndDoesNotShowSuccess()
    {
        var registrar = Substitute.For<IFileAssociationRegistrar>();
        registrar.When(r => r.Register()).Do(_ => throw new UnauthorizedAccessException("nope"));
        var notification = Substitute.For<IUserNotification>();
        var vm = CreateViewModel(registrar: registrar, notification: notification);

        vm.RegisterFileAssociationCommand.Execute(null);

        notification.Received(1).Notify("Registration failed", Arg.Is<string>(m => m.Contains("nope")));
        notification.DidNotReceive().Notify("Registered", Arg.Any<string>());
    }

    [Fact]
    public void UnregisterFileAssociation_InvokesRegistrarAndNotifiesOnSuccess()
    {
        var registrar = Substitute.For<IFileAssociationRegistrar>();
        var notification = Substitute.For<IUserNotification>();
        var vm = CreateViewModel(registrar: registrar, notification: notification);

        vm.UnregisterFileAssociationCommand.Execute(null);

        registrar.Received(1).Unregister();
        notification.Received(1).Notify("Unregistered", Arg.Any<string>());
    }

    [Fact]
    public void UnregisterFileAssociation_WhenRegistrarThrows_NotifiesFailureAndDoesNotShowSuccess()
    {
        var registrar = Substitute.For<IFileAssociationRegistrar>();
        registrar.When(r => r.Unregister()).Do(_ => throw new UnauthorizedAccessException("nope"));
        var notification = Substitute.For<IUserNotification>();
        var vm = CreateViewModel(registrar: registrar, notification: notification);

        vm.UnregisterFileAssociationCommand.Execute(null);

        notification.Received(1).Notify("Unregistration failed", Arg.Is<string>(m => m.Contains("nope")));
        notification.DidNotReceive().Notify("Unregistered", Arg.Any<string>());
    }

    private static IFileSystem CreateRenderingFileSystem(params string[] paths)
    {
        var fs = Substitute.For<IFileSystem>();
        foreach (var path in paths)
        {
            fs.FileExists(path).Returns(true);
            fs.GetFileSizeBytes(path).Returns(64L);
            fs.ReadAllTextAsync(path, Arg.Any<CancellationToken>()).Returns("# " + path);
        }
        return fs;
    }

    [Fact]
    public void CanGoBackAndForward_AreFalseInitially()
    {
        var vm = CreateViewModel();

        vm.CanGoBack.Should().BeFalse();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void SelectingMarkdownFile_RecordsItAsCurrentHistoryEntry()
    {
        const string path = "C:\\notes\\a.md";
        var fs = CreateRenderingFileSystem(path);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(path));

        vm.CanGoBack.Should().BeFalse();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void SelectingSecondMarkdownFile_EnablesGoBack()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));

        vm.CanGoBack.Should().BeTrue();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void GoBack_NavigatesToPreviousFileAndEnablesForward()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));

        vm.GoBackCommand.Execute(null);

        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(a);
        vm.CanGoBack.Should().BeFalse();
        vm.CanGoForward.Should().BeTrue();
    }

    [Fact]
    public void GoForward_NavigatesToNextFile()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.GoBackCommand.Execute(null);

        vm.GoForwardCommand.Execute(null);

        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(b);
        vm.CanGoBack.Should().BeTrue();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void VisitingNewFileAfterBack_DiscardsForwardStack()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        const string c = "C:\\notes\\c.md";
        var fs = CreateRenderingFileSystem(a, b, c);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.GoBackCommand.Execute(null);
        vm.CanGoForward.Should().BeTrue();

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(c));

        vm.CanGoForward.Should().BeFalse();
        vm.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public void GoBack_DoesNotRecordTheBackwardJumpAsANewVisit()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));

        vm.GoBackCommand.Execute(null);
        vm.GoForwardCommand.Execute(null);

        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(b);
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void DecliningLargeFileConfirmation_DoesNotRecordVisit()
    {
        const string a = "C:\\notes\\a.md";
        const string big = "C:\\notes\\big.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(a).Returns(true);
        fs.GetFileSizeBytes(a).Returns(64L);
        fs.ReadAllTextAsync(a, Arg.Any<CancellationToken>()).Returns("# a");
        fs.FileExists(big).Returns(true);
        fs.GetFileSizeBytes(big).Returns(6L * 1024 * 1024);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var confirmation = Substitute.For<IUserConfirmation>();
        confirmation.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var vm = CreateViewModel(fs: fs, renderer: renderer, confirmation: confirmation);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(big));

        vm.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void OpenFolder_ClearsNavigationHistory()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\other");
        var vm = CreateViewModel(picker: picker, fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.CanGoBack.Should().BeTrue();

        vm.OpenFolderCommand.Execute(null);

        vm.CanGoBack.Should().BeFalse();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void TryOpenFromPath_WhenChangingFolder_ClearsNavigationHistory()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        const string newFolder = "C:\\other";
        var fs = CreateRenderingFileSystem(a, b);
        fs.DirectoryExists(newFolder).Returns(true);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.CanGoBack.Should().BeTrue();

        vm.TryOpenFromPath(newFolder);

        vm.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void Refresh_WhenRootStillExists_PreservesNavigationHistory()
    {
        const string folder = "C:\\notes";
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { a, b });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder });
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.CanGoBack.Should().BeTrue();

        vm.RefreshCommand.Execute(null);

        vm.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public void Refresh_WhenRootGone_ClearsNavigationHistory()
    {
        const string folder = "C:\\notes";
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        fs.DirectoryExists(folder).Returns(true, false);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { a, b });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder });
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));

        vm.RefreshCommand.Execute(null);

        vm.CanGoBack.Should().BeFalse();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void GoBackCommand_CanExecute_TracksCanGoBack()
    {
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.GoBackCommand.CanExecute(null).Should().BeFalse();
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.GoBackCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void SelectingFolderNode_DoesNotAffectHistory()
    {
        const string folder = "C:\\notes";
        const string a = "C:\\notes\\a.md";
        var fs = CreateRenderingFileSystem(a);
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));

        vm.SelectedNode = new FolderNodeViewModel(new FolderNode(folder, fs));

        vm.CanGoBack.Should().BeFalse();
        vm.CanGoForward.Should().BeFalse();
    }

    private static IFileSystem CreateFilterTreeFileSystem()
    {
        // /notes
        //   /design
        //     architecture.md
        //     readme.md
        //   /random
        //     misc.md
        //   notes.md
        const string folder = "C:\\notes";
        const string design = "C:\\notes\\design";
        const string random = "C:\\notes\\random";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { design, random });
        fs.EnumerateDirectories(design).Returns(Array.Empty<string>());
        fs.EnumerateDirectories(random).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { "C:\\notes\\notes.md" });
        fs.EnumerateFiles(design, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { "C:\\notes\\design\\architecture.md", "C:\\notes\\design\\readme.md" });
        fs.EnumerateFiles(random, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { "C:\\notes\\random\\misc.md" });
        return fs;
    }

    [Fact]
    public void FilterText_DefaultsToEmpty()
    {
        var vm = CreateViewModel();

        vm.FilterText.Should().BeEmpty();
    }

    [Fact]
    public void Filter_WhenEmpty_LeavesAllNodesVisible()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });

        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        vm.RootNode.IsVisible.Should().BeTrue();
        design.IsVisible.Should().BeTrue();
        design.Children.All(c => c.IsVisible).Should().BeTrue();
    }

    [Fact]
    public void Filter_HidesNodesThatDoNotMatchSubstring()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });

        vm.FilterText = "architecture";

        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        var random = vm.RootNode.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "random");
        var notes = vm.RootNode.Children.OfType<MarkdownFileNodeViewModel>().Single(f => f.DisplayName == "notes.md");
        design.IsVisible.Should().BeTrue();
        random.IsVisible.Should().BeFalse();
        notes.IsVisible.Should().BeFalse();
        design.Children.OfType<MarkdownFileNodeViewModel>().Single(c => c.DisplayName == "architecture.md").IsVisible.Should().BeTrue();
        design.Children.OfType<MarkdownFileNodeViewModel>().Single(c => c.DisplayName == "readme.md").IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Filter_ExpandsAncestorsOfMatches()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });
        vm.RootNode!.IsExpanded.Should().BeFalse();

        vm.FilterText = "architecture";

        vm.RootNode.IsExpanded.Should().BeTrue();
        var design = vm.RootNode.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        design.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });

        vm.FilterText = "ARCH";

        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        design.Children.OfType<MarkdownFileNodeViewModel>().Single(c => c.DisplayName == "architecture.md").IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Filter_WhitespaceOnly_TreatedAsEmpty()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });
        vm.FilterText = "architecture";

        vm.FilterText = "   ";

        vm.RootNode!.Children.All(c => c.IsVisible).Should().BeTrue();
    }

    [Fact]
    public void Filter_FolderNameMatch_MakesFolderVisibleWithoutForcingExpansion()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });

        vm.FilterText = "design";

        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        design.IsVisible.Should().BeTrue();
        design.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void Filter_Clearing_RestoresVisibilityOnPreviouslyHiddenNodes()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });
        vm.FilterText = "architecture";
        var random = vm.RootNode!.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "random");
        random.IsVisible.Should().BeFalse();

        vm.FilterText = string.Empty;

        random.IsVisible.Should().BeTrue();
        random.Children.All(c => c.IsVisible).Should().BeTrue();
    }

    [Fact]
    public void Filter_Clearing_RestoresPreFilterExpansionState()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = "C:\\notes",
            ExpandedFolders = new List<string> { "C:\\notes" },
        });
        // Pre-filter: root is expanded by the user, child folders are not.
        vm.RootNode!.IsExpanded.Should().BeTrue();
        var design = vm.RootNode.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        var random = vm.RootNode.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "random");
        design.IsExpanded.Should().BeFalse();
        random.IsExpanded.Should().BeFalse();

        vm.FilterText = "architecture";
        // Filter force-expands ancestors of matches.
        design.IsExpanded.Should().BeTrue();

        vm.FilterText = string.Empty;

        vm.RootNode.IsExpanded.Should().BeTrue();
        design.IsExpanded.Should().BeFalse();
        random.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void OpenFolder_ClearsFilterText()
    {
        var picker = Substitute.For<IFolderPicker>();
        picker.PickFolder().Returns("C:\\other");
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(picker: picker, fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });
        vm.FilterText = "architecture";

        vm.OpenFolderCommand.Execute(null);

        vm.FilterText.Should().BeEmpty();
    }

    [Fact]
    public void TryOpenFromPath_WhenSwappingFolder_ClearsFilterText()
    {
        var fs = CreateFilterTreeFileSystem();
        fs.DirectoryExists("C:\\other").Returns(true);
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });
        vm.FilterText = "architecture";

        vm.TryOpenFromPath("C:\\other");

        vm.FilterText.Should().BeEmpty();
    }

    [Fact]
    public void CanGoUp_IsFalse_WhenNoFolderOpen()
    {
        var vm = CreateViewModel();

        vm.CanGoUp.Should().BeFalse();
        vm.GoUpCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanGoUp_IsTrue_WhenRootHasParent()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("C:\\notes\\design").Returns(true);
        var vm = CreateViewModel(fs: fs);

        vm.TryOpenFromPath("C:\\notes\\design");

        vm.CanGoUp.Should().BeTrue();
        vm.GoUpCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void CanGoUp_IsFalse_WhenRootIsDriveRoot()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("C:\\").Returns(true);
        var vm = CreateViewModel(fs: fs);

        vm.TryOpenFromPath("C:\\");

        vm.CanGoUp.Should().BeFalse();
    }

    [Fact]
    public void GoUp_ReRootsTreeAtParentFolder()
    {
        const string sub = "C:\\notes\\design";
        const string parent = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists(parent).Returns(true);
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(sub);

        vm.GoUpCommand.Execute(null);

        vm.RootNode!.FullPath.Should().Be(parent);
        vm.WindowTitle.Should().Be("notes — mdpeek");
    }

    [Fact]
    public void GoUp_WhenParentDoesNotExist_LeavesStateUnchanged()
    {
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists("C:\\notes").Returns(false);
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(sub);

        vm.GoUpCommand.Execute(null);

        vm.RootNode!.FullPath.Should().Be(sub);
    }

    [Fact]
    public void GoUp_PreservesSelectedFile()
    {
        const string sub = "C:\\notes\\design";
        const string parent = "C:\\notes";
        const string file = "C:\\notes\\design\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists(parent).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# readme");
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(parent, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        fs.EnumerateFiles(sub, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { file });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# readme", Arg.Any<CancellationToken>()).Returns("<html>readme</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(sub);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(file));

        vm.GoUpCommand.Execute(null);

        vm.SelectedNode.Should().BeOfType<MarkdownFileNodeViewModel>().Which.FullPath.Should().Be(file);
        vm.HtmlContent.Should().Be("<html>readme</html>");
    }

    [Fact]
    public void GoUp_ExpandsAncestorFoldersOfSelectedFile()
    {
        const string sub = "C:\\notes\\design";
        const string parent = "C:\\notes";
        const string file = "C:\\notes\\design\\readme.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists(parent).Returns(true);
        fs.FileExists(file).Returns(true);
        fs.GetFileSizeBytes(file).Returns(64L);
        fs.ReadAllTextAsync(file, Arg.Any<CancellationToken>()).Returns("# readme");
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(parent, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        fs.EnumerateFiles(sub, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(new[] { file });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(sub);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(file));

        vm.GoUpCommand.Execute(null);

        vm.RootNode!.IsExpanded.Should().BeTrue();
        var designFolder = vm.RootNode.Children.OfType<FolderNodeViewModel>().Single();
        designFolder.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void GoUp_PreservesExpansionStateOfFoldersUnderTheOldRoot()
    {
        const string sub = "C:\\notes\\design";
        const string parent = "C:\\notes";
        const string nested = "C:\\notes\\design\\nested";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists(parent).Returns(true);
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(new[] { nested });
        fs.EnumerateDirectories(nested).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(sub);
        // Pre-expand the old root and its nested folder.
        vm.RootNode!.IsExpanded = true;
        var nestedFolder = vm.RootNode.Children.OfType<FolderNodeViewModel>().Single();
        nestedFolder.IsExpanded = true;

        vm.GoUpCommand.Execute(null);

        vm.RootNode!.FullPath.Should().Be(parent);
        vm.RootNode.IsExpanded.Should().BeTrue();
        var oldRootInNewTree = vm.RootNode.Children.OfType<FolderNodeViewModel>().Single(f => f.FullPath == sub);
        oldRootInNewTree.IsExpanded.Should().BeTrue();
        oldRootInNewTree.Children.OfType<FolderNodeViewModel>().Single().IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void GoUp_DoesNotRecordANewHistoryEntry()
    {
        const string sub = "C:\\notes\\design";
        const string parent = "C:\\notes";
        const string a = "C:\\notes\\design\\a.md";
        const string b = "C:\\notes\\design\\b.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists(parent).Returns(true);
        fs.FileExists(a).Returns(true);
        fs.FileExists(b).Returns(true);
        fs.GetFileSizeBytes(Arg.Any<string>()).Returns(64L);
        fs.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("# x");
        fs.EnumerateDirectories(Arg.Any<string>()).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(sub);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.CanGoBack.Should().BeTrue();
        vm.CanGoForward.Should().BeFalse();

        vm.GoUpCommand.Execute(null);

        vm.CanGoBack.Should().BeTrue();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void GoUp_ClearsFilterText()
    {
        const string sub = "C:\\notes\\design";
        const string parent = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(sub).Returns(true);
        fs.DirectoryExists(parent).Returns(true);
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(sub);
        vm.FilterText = "anything";

        vm.GoUpCommand.Execute(null);

        vm.FilterText.Should().BeEmpty();
    }

    [Fact]
    public void SetAsRoot_ReRootsTreeAtGivenFolder()
    {
        const string parent = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(parent).Returns(true);
        fs.DirectoryExists(sub).Returns(true);
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(parent);
        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().Single();

        vm.SetAsRootCommand.Execute(design);

        vm.RootNode!.FullPath.Should().Be(sub);
        vm.WindowTitle.Should().Be("design — mdpeek");
    }

    [Fact]
    public void SetAsRoot_PreservesNavigationHistory()
    {
        const string parent = "C:\\notes";
        const string sub = "C:\\notes\\design";
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(parent).Returns(true);
        fs.DirectoryExists(sub).Returns(true);
        fs.FileExists(a).Returns(true);
        fs.FileExists(b).Returns(true);
        fs.GetFileSizeBytes(Arg.Any<string>()).Returns(64L);
        fs.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("# x");
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(parent);
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(a));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(b));
        vm.CanGoBack.Should().BeTrue();
        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().Single();

        vm.SetAsRootCommand.Execute(design);

        vm.CanGoBack.Should().BeTrue();
        vm.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void SetAsRoot_ClearsFilterText()
    {
        const string parent = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(parent).Returns(true);
        fs.DirectoryExists(sub).Returns(true);
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(parent);
        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().Single();
        vm.FilterText = "anything";

        vm.SetAsRootCommand.Execute(design);

        vm.FilterText.Should().BeEmpty();
    }

    [Fact]
    public void SetAsRoot_CanExecute_IsFalseForCurrentRoot()
    {
        const string parent = "C:\\notes";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(parent).Returns(true);
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(parent);

        vm.SetAsRootCommand.CanExecute(vm.RootNode).Should().BeFalse();
    }

    [Fact]
    public void SetAsRoot_CanExecute_IsTrueForDescendantFolder()
    {
        const string parent = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(parent).Returns(true);
        fs.DirectoryExists(sub).Returns(true);
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(parent);
        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().Single();

        vm.SetAsRootCommand.CanExecute(design).Should().BeTrue();
    }

    [Fact]
    public void SetAsRoot_CanExecute_IsFalse_WhenNoFolderOpen()
    {
        var fs = Substitute.For<IFileSystem>();
        var vm = CreateViewModel(fs: fs);
        var orphan = new FolderNodeViewModel(new FolderNode("C:\\notes\\design", fs));

        vm.SetAsRootCommand.CanExecute(orphan).Should().BeFalse();
    }

    [Fact]
    public void SetAsRoot_WhenFolderNoLongerExists_LeavesStateUnchanged()
    {
        const string parent = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(parent).Returns(true);
        fs.EnumerateDirectories(parent).Returns(new[] { sub });
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(parent);
        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().Single();
        fs.DirectoryExists(sub).Returns(false);

        vm.SetAsRootCommand.Execute(design);

        vm.RootNode!.FullPath.Should().Be(parent);
    }

    [Fact]
    public void SetAsRoot_RestoresExpansionStateOfDescendantFolders()
    {
        const string root = "C:\\docs";
        const string guides = "C:\\docs\\guides";
        const string beginner = "C:\\docs\\guides\\beginner";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(root).Returns(true);
        fs.DirectoryExists(guides).Returns(true);
        fs.DirectoryExists(beginner).Returns(true);
        fs.EnumerateDirectories(root).Returns(new[] { guides });
        fs.EnumerateDirectories(guides).Returns(new[] { beginner });
        fs.EnumerateDirectories(beginner).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.TryOpenFromPath(root);
        vm.RootNode!.IsExpanded = true;
        var guidesVm = vm.RootNode.Children.OfType<FolderNodeViewModel>().Single();
        guidesVm.IsExpanded = true;
        var beginnerVm = guidesVm.Children.OfType<FolderNodeViewModel>().Single();
        beginnerVm.IsExpanded = true;

        vm.SetAsRootCommand.Execute(guidesVm);

        vm.RootNode!.FullPath.Should().Be(guides);
        vm.RootNode.IsExpanded.Should().BeTrue();
        vm.RootNode.Children.OfType<FolderNodeViewModel>().Single().IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void Refresh_PreservesFilterTextAndReappliesToRebuiltTree()
    {
        var fs = CreateFilterTreeFileSystem();
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\notes" });
        vm.FilterText = "architecture";

        vm.RefreshCommand.Execute(null);

        vm.FilterText.Should().Be("architecture");
        var design = vm.RootNode!.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "design");
        var random = vm.RootNode.Children.OfType<FolderNodeViewModel>().First(f => f.DisplayName == "random");
        design.IsVisible.Should().BeTrue();
        design.IsExpanded.Should().BeTrue();
        random.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ExpandAll_WhenNoFolderOpen_DoesNothing()
    {
        var vm = CreateViewModel();

        vm.ExpandAllCommand.Execute(null);

        vm.RootNode.Should().BeNull();
    }

    [Fact]
    public void CollapseAll_WhenNoFolderOpen_DoesNothing()
    {
        var vm = CreateViewModel();

        vm.CollapseAllCommand.Execute(null);

        vm.RootNode.Should().BeNull();
    }

    [Fact]
    public void ExpandAll_ExpandsRootAndAllLoadedDescendantFolders()
    {
        const string folder = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder });
        _ = vm.RootNode!.Children.OfType<FolderNodeViewModel>().Single().Children;

        vm.ExpandAllCommand.Execute(null);

        vm.RootNode!.IsExpanded.Should().BeTrue();
        vm.RootNode.Children.OfType<FolderNodeViewModel>().Single().IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void CollapseAll_CollapsesRootAndAllLoadedDescendantFolders()
    {
        const string folder = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub });
        fs.EnumerateDirectories(sub).Returns(Array.Empty<string>());
        fs.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            ExpandedFolders = new List<string> { folder, sub },
        });
        vm.RootNode!.IsExpanded.Should().BeTrue();
        vm.RootNode.Children.OfType<FolderNodeViewModel>().Single().IsExpanded.Should().BeTrue();

        vm.CollapseAllCommand.Execute(null);

        vm.RootNode!.IsExpanded.Should().BeFalse();
        vm.RootNode.Children.OfType<FolderNodeViewModel>().Single().IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ExpandAll_DoesNotForceLoadUnopenedFolders()
    {
        const string folder = "C:\\notes";
        const string sub = "C:\\notes\\design";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(new[] { sub });
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        var vm = CreateViewModel(fs: fs);
        vm.ApplyStartupSettings(new AppSettings { LastFolder = folder });
        _ = vm.RootNode!.Children;

        vm.ExpandAllCommand.Execute(null);

        fs.DidNotReceive().EnumerateDirectories(sub);
        fs.DidNotReceive().EnumerateFiles(sub, Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public void GoBack_AfterSelectingTreeWrappers_ResolvesToSameTreeWrapperInstance()
    {
        const string folder = "C:\\notes";
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { a, b });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(folder);

        var children = vm.RootNode!.Children;
        var aWrapper = children.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == a);
        var bWrapper = children.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == b);
        vm.SelectedNode = aWrapper;
        vm.SelectedNode = bWrapper;

        vm.GoBackCommand.Execute(null);

        vm.SelectedNode.Should().BeSameAs(aWrapper);
    }

    [Fact]
    public void GoForward_AfterSelectingTreeWrappers_ResolvesToSameTreeWrapperInstance()
    {
        const string folder = "C:\\notes";
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { a, b });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(folder);
        var children = vm.RootNode!.Children;
        var aWrapper = children.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == a);
        var bWrapper = children.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == b);
        vm.SelectedNode = aWrapper;
        vm.SelectedNode = bWrapper;
        vm.GoBackCommand.Execute(null);

        vm.GoForwardCommand.Execute(null);

        vm.SelectedNode.Should().BeSameAs(bWrapper);
    }

    [Fact]
    public void Refresh_RebuildsFileIndexSoBackResolvesNewTreeWrapper()
    {
        const string folder = "C:\\notes";
        const string a = "C:\\notes\\a.md";
        const string b = "C:\\notes\\b.md";
        var fs = CreateRenderingFileSystem(a, b);
        fs.DirectoryExists(folder).Returns(true);
        fs.EnumerateDirectories(folder).Returns(Array.Empty<string>());
        fs.EnumerateFiles(folder, Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(new[] { a, b });
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("<html/>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);
        vm.TryOpenFromPath(folder);
        var firstChildren = vm.RootNode!.Children;
        var firstAWrapper = firstChildren.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == a);
        var firstBWrapper = firstChildren.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == b);
        vm.SelectedNode = firstAWrapper;
        vm.SelectedNode = firstBWrapper;

        vm.RefreshCommand.Execute(null);
        var newChildren = vm.RootNode!.Children;
        var newAWrapper = newChildren.OfType<MarkdownFileNodeViewModel>().Single(c => c.FullPath == a);
        vm.GoBackCommand.Execute(null);

        vm.SelectedNode.Should().BeSameAs(newAWrapper);
        vm.SelectedNode.Should().NotBeSameAs(firstAWrapper);
    }

    [Fact]
    public async Task SelectingDifferentFile_MidLoad_CancelsPreviousReadAndDoesNotOverwriteNewFile()
    {
        const string slow = "C:\\notes\\slow.md";
        const string fast = "C:\\notes\\fast.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(slow).Returns(true);
        fs.FileExists(fast).Returns(true);
        fs.GetFileSizeBytes(Arg.Any<string>()).Returns(64L);

        // The slow file's read is gated on a TaskCompletionSource so it cannot
        // complete until the test releases it — that is the window in which
        // the second selection arrives and should cancel the in-flight load.
        var slowRead = new TaskCompletionSource<string>();
        fs.ReadAllTextAsync(slow, Arg.Any<CancellationToken>()).Returns(slowRead.Task);
        fs.ReadAllTextAsync(fast, Arg.Any<CancellationToken>()).Returns(Task.FromResult("# fast"));

        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderAsync("# slow", Arg.Any<CancellationToken>()).Returns(Task.FromResult("<html>slow</html>"));
        renderer.RenderAsync("# fast", Arg.Any<CancellationToken>()).Returns(Task.FromResult("<html>fast</html>"));

        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(slow));
        vm.SelectedNode = new MarkdownFileNodeViewModel(new MarkdownFileNode(fast));

        vm.HtmlContent.Should().Be("<html>fast</html>");

        // Now release the slow file's read; its continuation should observe
        // the cancellation token set by the second selection and bail without
        // overwriting HtmlContent.
        slowRead.SetResult("# slow");
        if (vm.CurrentLoadTask is not null)
        {
            await vm.CurrentLoadTask;
        }

        vm.HtmlContent.Should().Be("<html>fast</html>");
    }
}
