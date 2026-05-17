using MdPeek.App;

using FluentAssertions;

namespace MdPeek.App.Tests.ViewModels;

public class DocumentFileNodeViewModelTests
{
    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var vm = new DocumentFileNodeViewModel(new DocumentFileNode("C:\\docs\\notes.md"));

        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_DefaultsToFalse()
    {
        var vm = new DocumentFileNodeViewModel(new DocumentFileNode("C:\\docs\\notes.md"));

        vm.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void File_ExposesWrappedCoreNode()
    {
        var file = new DocumentFileNode("C:\\docs\\notes.md");
        var vm = new DocumentFileNodeViewModel(file);

        vm.File.Should().BeSameAs(file);
    }

    [Fact]
    public void DisplayName_DelegatesToCoreNode()
    {
        var vm = new DocumentFileNodeViewModel(new DocumentFileNode("C:\\docs\\notes.md"));

        vm.DisplayName.Should().Be("notes.md");
    }
}
