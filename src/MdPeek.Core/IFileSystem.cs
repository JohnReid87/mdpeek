namespace MdPeek.Core;

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

    /// <summary>
    /// Returns true if the given path exists and is a regular file.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Returns the size in bytes of the file at <paramref name="path"/>.
    /// </summary>
    long GetFileSizeBytes(string path);

    /// <summary>
    /// Reads the full text contents of the file at <paramref name="path"/>.
    /// </summary>
    string ReadAllText(string path);
}
