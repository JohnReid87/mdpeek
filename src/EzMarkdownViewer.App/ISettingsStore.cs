namespace EzMarkdownViewer.App;

public interface ISettingsStore
{
    /// <summary>
    /// Loads persisted settings. Returns a fresh <see cref="AppSettings"/>
    /// with defaults if the file is missing, malformed, or carries a
    /// non-matching <see cref="AppSettings.SchemaVersion"/>.
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// Persists the given settings. Failures are swallowed: a save error on
    /// shutdown must not surface to the user or block app close.
    /// </summary>
    void Save(AppSettings settings);
}
