namespace EzMarkdownViewer.App;

/// <summary>
/// User-facing state persisted across app launches: last opened folder, last
/// selected file, expanded folder paths, window geometry, and splitter
/// position.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string? LastFolder { get; set; }

    public string? LastSelectedFile { get; set; }

    public List<string> ExpandedFolders { get; set; } = new();

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public bool WindowMaximized { get; set; }

    public double? SplitterPosition { get; set; }
}
