namespace MdPeek.Core.Rendering;

/// <summary>
/// Extension-keyed registry of <see cref="IDocumentRenderer"/> instances.
/// The factory is the single source of truth for which file extensions the
/// application can display; all extension-sensitive code queries it rather
/// than hard-coding extension strings.
/// </summary>
public interface IDocumentRendererFactory
{
    /// <summary>
    /// Returns the renderer registered for <paramref name="extension"/>
    /// (e.g. <c>.md</c>), or <c>null</c> if no renderer handles it.
    /// </summary>
    IDocumentRenderer? TryGet(string extension);

    /// <summary>All file extensions the registered renderers support.</summary>
    IEnumerable<string> SupportedExtensions { get; }

    /// <summary>
    /// All registered renderer instances (one per renderer, not one per
    /// extension). Used to propagate application-level settings such as theme.
    /// </summary>
    IEnumerable<IDocumentRenderer> All { get; }
}
