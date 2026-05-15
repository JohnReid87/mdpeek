namespace EzMarkdownViewer.Core;

public interface IFileSystem
{
    /// <summary>
    /// Returns true if the given path exists and is a directory.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Returns the immediate child directories of <paramref name="path"/>.
    /// </summary>
    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>
    /// Returns files under <paramref name="path"/> matching
    /// <paramref name="searchPattern"/>, either at the top level only or
    /// recursively, depending on <paramref name="searchOption"/>.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
}
