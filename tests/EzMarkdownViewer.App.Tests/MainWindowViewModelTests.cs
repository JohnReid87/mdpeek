using EzMarkdownViewer.App;
using EzMarkdownViewer.Core;

using FluentAssertions;

using NSubstitute;

namespace EzMarkdownViewer.App.Tests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        IFolderPicker? picker = null,
        IFileSystem? fs = null,
        IMarkdownRenderer? renderer = null,
        IUserConfirmation? confirmation = null) =>
        new(
            picker ?? Substitute.For<IFolderPicker>(),
            fs ?? Substitute.For<IFileSystem>(),
            renderer ?? Substitute.For<IMarkdownRenderer>(),
            confirmation ?? Substitute.For<IUserConfirmation>());

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

        vm.WindowTitle.Should().Be("design — ez-markdown-viewer");
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
        vm.WindowTitle.Should().Be("ez-markdown-viewer");
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
        fs.ReadAllText(path).Returns("# hello");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.Render("# hello").Returns("<html>ok</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNode(path);

        vm.HtmlContent.Should().Be("<html>ok</html>");
    }

    [Fact]
    public void SelectedNode_WhenFolderSelected_LeavesHtmlContentUnchanged()
    {
        var fs = Substitute.For<IFileSystem>();
        var renderer = Substitute.For<IMarkdownRenderer>();
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new FolderNode("C:\\notes", fs);

        vm.HtmlContent.Should().BeNull();
        renderer.DidNotReceiveWithAnyArgs().Render(default!);
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

        vm.SelectedNode = new MarkdownFileNode(path);

        vm.HtmlContent.Should().Be("<html>missing</html>");
        fs.DidNotReceive().ReadAllText(Arg.Any<string>());
    }

    [Fact]
    public void SelectedNode_WhenReadThrowsIoException_RendersReadError()
    {
        const string path = "C:\\notes\\locked.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(64L);
        fs.ReadAllText(path).Returns(_ => throw new IOException("locked"));
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.RenderError("Could not read file", Arg.Any<string>()).Returns("<html>ioerr</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNode(path);

        vm.HtmlContent.Should().Be("<html>ioerr</html>");
        renderer.DidNotReceiveWithAnyArgs().Render(default!);
    }

    [Fact]
    public void SelectedNode_WhenRendererThrows_RendersParseError()
    {
        const string path = "C:\\notes\\bad.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(64L);
        fs.ReadAllText(path).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.Render("# hi").Returns(_ => throw new InvalidOperationException("boom"));
        renderer.RenderError("Could not render markdown", Arg.Any<string>()).Returns("<html>parseerr</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.SelectedNode = new MarkdownFileNode(path);

        vm.HtmlContent.Should().Be("<html>parseerr</html>");
    }

    [Fact]
    public void SelectedNode_WhenLargeFileAndUserConfirms_RendersFile()
    {
        const string path = "C:\\notes\\big.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(6L * 1024 * 1024);
        fs.ReadAllText(path).Returns("# big");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.Render("# big").Returns("<html>big</html>");
        var confirmation = Substitute.For<IUserConfirmation>();
        confirmation.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var vm = CreateViewModel(fs: fs, renderer: renderer, confirmation: confirmation);

        vm.SelectedNode = new MarkdownFileNode(path);

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

        vm.SelectedNode = new MarkdownFileNode(path);

        vm.HtmlContent.Should().BeNull();
        fs.DidNotReceive().ReadAllText(Arg.Any<string>());
        renderer.DidNotReceiveWithAnyArgs().Render(default!);
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
        vm.WindowTitle.Should().Be("notes — ez-markdown-viewer");
    }

    [Fact]
    public void ApplyStartupSettings_WhenLastFolderMissing_LeavesRootNodeNull()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("C:\\gone").Returns(false);
        var vm = CreateViewModel(fs: fs);

        vm.ApplyStartupSettings(new AppSettings { LastFolder = "C:\\gone" });

        vm.RootNode.Should().BeNull();
        vm.WindowTitle.Should().Be("ez-markdown-viewer");
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
        fs.ReadAllText(file).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.Render("# hi").Returns("<html>restored</html>");
        var vm = CreateViewModel(fs: fs, renderer: renderer);

        vm.ApplyStartupSettings(new AppSettings
        {
            LastFolder = folder,
            LastSelectedFile = file,
        });

        vm.SelectedNode.Should().BeOfType<MarkdownFileNode>().Which.FullPath.Should().Be(file);
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
        fs.ReadAllText(file).Returns("# hi");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.Render(Arg.Any<string>()).Returns("<html>ok</html>");
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
        var child = vm.RootNode.Children.OfType<FolderNode>().Single();
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
    public void SelectedNode_WhenFileAtThreshold_DoesNotPromptForConfirmation()
    {
        const string path = "C:\\notes\\edge.md";
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.GetFileSizeBytes(path).Returns(5L * 1024 * 1024);
        fs.ReadAllText(path).Returns("# edge");
        var renderer = Substitute.For<IMarkdownRenderer>();
        renderer.Render("# edge").Returns("<html>edge</html>");
        var confirmation = Substitute.For<IUserConfirmation>();
        var vm = CreateViewModel(fs: fs, renderer: renderer, confirmation: confirmation);

        vm.SelectedNode = new MarkdownFileNode(path);

        vm.HtmlContent.Should().Be("<html>edge</html>");
        confirmation.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
    }
}
