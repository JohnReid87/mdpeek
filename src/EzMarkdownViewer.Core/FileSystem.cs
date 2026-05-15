namespace EzMarkdownViewer.Core;

public sealed class FileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public long GetFileSizeBytes(string path) => new FileInfo(path).Length;

    /// <inheritdoc />
    public string ReadAllText(string path) => File.ReadAllText(path);
}
