using MdPeek.Core;

namespace MdPeek.App.ViewModels;

/// <summary>
/// View-model for a <see cref="DocumentFileNode"/>. Adds no UI state beyond
/// the <see cref="DirectoryTreeNodeViewModel.IsVisible"/> from the base — the
/// type is its own marker so the selection-changed logic can pattern-match
/// for file selections.
/// </summary>
public sealed class DocumentFileNodeViewModel : DirectoryTreeNodeViewModel
{
    public DocumentFileNodeViewModel(DocumentFileNode file)
        : base(file)
    {
    }

    public DocumentFileNode File => (DocumentFileNode)Node;
}
