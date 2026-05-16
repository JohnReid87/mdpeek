using MdPeek.Core;

namespace MdPeek.App;

/// <summary>
/// View-model for a <see cref="MarkdownFileNode"/>. Adds no UI state beyond
/// the <see cref="DirectoryTreeNodeViewModel.IsVisible"/> from the base — the
/// type is its own marker so the selection-changed logic can pattern-match
/// for file selections.
/// </summary>
public sealed class MarkdownFileNodeViewModel : DirectoryTreeNodeViewModel
{
    public MarkdownFileNodeViewModel(MarkdownFileNode file)
        : base(file)
    {
    }

    public MarkdownFileNode File => (MarkdownFileNode)Node;
}
