using MdPeek.App;
using MdPeek.Core;

using FluentAssertions;

namespace MdPeek.App.Tests;

public class MarkdownFileNodeViewModelTests
{
    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var vm = new MarkdownFileNodeViewModel(new MarkdownFileNode("C:\\docs\\notes.md"));

        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_DefaultsToFalse()
    {
        var vm = new MarkdownFileNodeViewModel(new MarkdownFileNode("C:\\docs\\notes.md"));

        vm.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void File_ExposesWrappedCoreNode()
    {
        var file = new MarkdownFileNode("C:\\docs\\notes.md");
        var vm = new MarkdownFileNodeViewModel(file);

        vm.File.Should().BeSameAs(file);
    }

    [Fact]
    public void DisplayName_DelegatesToCoreNode()
    {
        var vm = new MarkdownFileNodeViewModel(new MarkdownFileNode("C:\\docs\\notes.md"));

        vm.DisplayName.Should().Be("notes.md");
    }
}
