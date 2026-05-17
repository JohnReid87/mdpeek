using System.Diagnostics;
using System.Text.Json;

using MdPeek.Core;

namespace MdPeek.App;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly IFileSystem _fs;

    public JsonSettingsStore(string filePath, IFileSystem fileSystem)
    {
        _filePath = filePath;
        _fs = fileSystem;
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        try
        {
            if (!_fs.FileExists(_filePath))
            {
                return new AppSettings();
            }

            var json = _fs.ReadAllTextAsync(_filePath, CancellationToken.None)
                .GetAwaiter().GetResult();
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

            if (settings is null || settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
            {
                return new AppSettings();
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"[JsonSettingsStore] Failed to load settings from '{_filePath}': {ex}");
            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            _fs.WriteAllTextAsync(_filePath, json, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[JsonSettingsStore] Failed to save settings to '{_filePath}': {ex}");
        }
    }
}
