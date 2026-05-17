
using FluentAssertions;

namespace MdPeek.App.Tests.Navigation;

public class NavigationHistoryTests
{
    [Fact]
    public void NewInstance_HasNoCurrentAndCannotGoBackOrForward()
    {
        var history = new NavigationHistory();

        history.Current.Should().BeNull();
        history.CanGoBack.Should().BeFalse();
        history.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void Visit_FirstEntry_SetsCurrentButDoesNotEnableBack()
    {
        var history = new NavigationHistory();

        history.Visit("C:\\notes\\a.md");

        history.Current.Should().Be("C:\\notes\\a.md");
        history.CanGoBack.Should().BeFalse();
        history.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void Visit_SecondEntry_PushesPreviousOntoBackStack()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");

        history.Visit("C:\\notes\\b.md");

        history.Current.Should().Be("C:\\notes\\b.md");
        history.CanGoBack.Should().BeTrue();
        history.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void Visit_SamePathAsCurrent_IsNoOp()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");

        history.Visit("C:\\notes\\a.md");

        history.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void Visit_SamePathAsCurrent_IsCaseInsensitive()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");

        history.Visit("C:\\NOTES\\A.MD");

        history.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void Back_ReturnsPreviousPathAndUpdatesCurrent()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");
        history.Visit("C:\\notes\\b.md");

        var result = history.Back();

        result.Should().Be("C:\\notes\\a.md");
        history.Current.Should().Be("C:\\notes\\a.md");
    }

    [Fact]
    public void Back_AfterMove_EnablesForward()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");
        history.Visit("C:\\notes\\b.md");

        history.Back();

        history.CanGoForward.Should().BeTrue();
    }

    [Fact]
    public void Back_WhenBackStackEmpty_ReturnsNullAndLeavesCurrentUnchanged()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");

        var result = history.Back();

        result.Should().BeNull();
        history.Current.Should().Be("C:\\notes\\a.md");
    }

    [Fact]
    public void Forward_ReturnsNextPathAndUpdatesCurrent()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");
        history.Visit("C:\\notes\\b.md");
        history.Back();

        var result = history.Forward();

        result.Should().Be("C:\\notes\\b.md");
        history.Current.Should().Be("C:\\notes\\b.md");
    }

    [Fact]
    public void Forward_WhenForwardStackEmpty_ReturnsNull()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");

        var result = history.Forward();

        result.Should().BeNull();
    }

    [Fact]
    public void Visit_AfterBack_ClearsForwardStack()
    {
        var history = new NavigationHistory();
        history.Visit("C:\\notes\\a.md");
        history.Visit("C:\\notes\\b.md");
        history.Visit("C:\\notes\\c.md");
        history.Back();
        history.Back();
        history.CanGoForward.Should().BeTrue();

        history.Visit("C:\\notes\\d.md");

        history.CanGoForward.Should().BeFalse();
        history.Current.Should().Be("C:\\notes\\d.md");
    }

    [Fact]
    public void BackForwardSequence_WalksThroughHistoryInOrder()
    {
        var history = new NavigationHistory();
        history.Visit("a");
        history.Visit("b");
        history.Visit("c");

        history.Back().Should().Be("b");
        history.Back().Should().Be("a");
        history.Forward().Should().Be("b");
        history.Forward().Should().Be("c");
    }

    [Fact]
    public void Clear_RemovesAllStateIncludingCurrent()
    {
        var history = new NavigationHistory();
        history.Visit("a");
        history.Visit("b");
        history.Back();

        history.Clear();

        history.Current.Should().BeNull();
        history.CanGoBack.Should().BeFalse();
        history.CanGoForward.Should().BeFalse();
    }
}
