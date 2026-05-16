using System.Text.Json;

using MdPeek.App;

using FluentAssertions;

namespace MdPeek.App.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public JsonSettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ezmv-tests-" + Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var sut = new JsonSettingsStore(_filePath);

        var result = sut.Load();

        result.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        result.LastFolder.Should().BeNull();
        result.LastSelectedFile.Should().BeNull();
        result.ExpandedFolders.Should().BeEmpty();
    }

    [Fact]
    public void Load_WhenFileMalformed_ReturnsDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, "{ this is not valid json");
        var sut = new JsonSettingsStore(_filePath);

        var result = sut.Load();

        result.LastFolder.Should().BeNull();
        result.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
    }

    [Fact]
    public void Load_WhenSchemaVersionMismatch_ReturnsDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(new AppSettings
        {
            SchemaVersion = 999,
            LastFolder = "C:\\notes",
        }));
        var sut = new JsonSettingsStore(_filePath);

        var result = sut.Load();

        result.LastFolder.Should().BeNull();
        result.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var sut = new JsonSettingsStore(_filePath);
        var original = new AppSettings
        {
            LastFolder = "C:\\notes",
            LastSelectedFile = "C:\\notes\\readme.md",
            ExpandedFolders = new List<string> { "C:\\notes", "C:\\notes\\design" },
            WindowWidth = 1024,
            WindowHeight = 768,
            WindowLeft = 100,
            WindowTop = 50,
            WindowMaximized = true,
            SplitterPosition = 320,
        };

        sut.Save(original);
        var loaded = sut.Load();

        loaded.LastFolder.Should().Be("C:\\notes");
        loaded.LastSelectedFile.Should().Be("C:\\notes\\readme.md");
        loaded.ExpandedFolders.Should().Equal("C:\\notes", "C:\\notes\\design");
        loaded.WindowWidth.Should().Be(1024);
        loaded.WindowHeight.Should().Be(768);
        loaded.WindowLeft.Should().Be(100);
        loaded.WindowTop.Should().Be(50);
        loaded.WindowMaximized.Should().BeTrue();
        loaded.SplitterPosition.Should().Be(320);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var sut = new JsonSettingsStore(_filePath);

        sut.Save(new AppSettings { LastFolder = "C:\\notes" });

        File.Exists(_filePath).Should().BeTrue();
    }
}
