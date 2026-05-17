namespace MdPeek.Core.Rendering;

/// <summary>
/// Reads a file of a supported type and produces a <see cref="RenderResult"/>
/// for display in the browser host. Each renderer owns its own file I/O so
/// that encoding detection and format-specific reading stay co-located with
/// the rendering logic.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// File extensions (including the leading dot, e.g. <c>.md</c>) this
    /// renderer handles. Used by <see cref="IDocumentRendererFactory"/> to
    /// build its extension-keyed lookup and by <see cref="FolderNode"/> to
    /// derive file search patterns.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Controls which theme is applied to rendered output. Set by the
    /// application-level dark/light toggle before each render.
    /// </summary>
    bool IsDarkTheme { get; set; }

    /// <summary>
    /// Reads the file at <paramref name="filePath"/> and converts it to a
    /// <see cref="RenderResult"/> for the browser host.
    /// </summary>
    Task<RenderResult> RenderAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Renders an in-pane error as a <see cref="RenderResult.Html"/> in the
    /// same visual style as a normal document.
    /// </summary>
    RenderResult RenderError(string title, string detail);
}
