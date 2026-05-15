namespace EzMarkdownViewer.App;

public interface IFileAssociationRegistrar
{
    /// <summary>
    /// Registers the running application as a handler for <c>.md</c> files
    /// under the current user's classes hive so it appears in the Windows
    /// "Open With" list and can be selected as the default <c>.md</c> handler.
    /// Implementations must be idempotent: re-running the call must not fail
    /// if the entries already exist.
    /// </summary>
    void Register();

    /// <summary>
    /// Removes the registry entries written by <see cref="Register"/>. Must be
    /// idempotent: running this without a prior <see cref="Register"/> call
    /// must succeed silently.
    /// </summary>
    void Unregister();
}
