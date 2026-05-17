namespace MdPeek.Core;

/// <summary>
/// Builds an extension-keyed lookup from a flat list of
/// <see cref="IDocumentRenderer"/> instances. Each renderer declares the
/// extensions it handles via <see cref="IDocumentRenderer.SupportedExtensions"/>.
/// If two renderers claim the same extension the last-registered one wins.
/// </summary>
public sealed class DocumentRendererFactory : IDocumentRendererFactory
{
    private readonly Dictionary<string, IDocumentRenderer> _byExtension =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<IDocumentRenderer> _all;

    public DocumentRendererFactory(IEnumerable<IDocumentRenderer> renderers)
    {
        var list = renderers.ToList();
        _all = list;

        foreach (var renderer in list)
        {
            foreach (var ext in renderer.SupportedExtensions)
            {
                _byExtension[ext] = renderer;
            }
        }
    }

    /// <inheritdoc />
    public IDocumentRenderer? TryGet(string extension) =>
        _byExtension.TryGetValue(extension, out var r) ? r : null;

    /// <inheritdoc />
    public IEnumerable<string> SupportedExtensions => _byExtension.Keys;

    /// <inheritdoc />
    public IEnumerable<IDocumentRenderer> All => _all;
}
