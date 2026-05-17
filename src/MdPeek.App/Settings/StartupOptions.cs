namespace MdPeek.App.Settings;

/// <summary>
/// Options derived from the process command line. <see cref="Path"/> is the
/// first positional argument, if any — either a folder to open as the root or
/// a <c>.md</c> file to pre-select inside its parent folder.
/// </summary>
public sealed class StartupOptions
{
    public string? Path { get; init; }
}
