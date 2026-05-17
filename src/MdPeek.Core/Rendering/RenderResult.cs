namespace MdPeek.Core.Rendering;

/// <summary>
/// Discriminated result returned by <see cref="IDocumentRenderer.RenderAsync"/>.
/// A renderer either produces an HTML string for inline display, or a URI for
/// the browser host to navigate to directly (e.g. a local PDF file).
/// </summary>
public abstract record RenderResult
{
    /// <summary>A complete HTML document to display in the browser host.</summary>
    public sealed record Html(string Content) : RenderResult;

    /// <summary>A URI for the browser host to navigate to directly.</summary>
    public sealed record Navigate(Uri Target) : RenderResult;
}
