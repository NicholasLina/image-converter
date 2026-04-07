using System.Text.Json;
using ImageConverter.Gui.Models;
using ImageConverter.Gui.Services;
using Xunit;

namespace ImageConverter.Tests.Services;

public class AppSettingsServiceTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string settingsPath = Path.Combine(tempDir, "missing.json");

        try
        {
            AppSettings settings = AppSettingsService.Load(settingsPath);

            Assert.Equal(OutputFormat.Jpeg, settings.OutputFormat);
            Assert.Equal(85, settings.Quality);
            Assert.Null(settings.OutputFolder);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsValues()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string settingsPath = Path.Combine(tempDir, "settings.json");

        try
        {
            AppSettings original = new()
            {
                OutputFormat = OutputFormat.Avif,
                Quality = 42,
                OutputFolder = "/tmp/output-folder"
            };

            AppSettingsService.Save(settingsPath, original);
            AppSettings loaded = AppSettingsService.Load(settingsPath);

            Assert.Equal(OutputFormat.Avif, loaded.OutputFormat);
            Assert.Equal(42, loaded.Quality);
            Assert.Equal("/tmp/output-folder", loaded.OutputFolder);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefaults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string settingsPath = Path.Combine(tempDir, "settings.json");

        try
        {
            File.WriteAllText(settingsPath, "{ not-valid-json ");

            AppSettings loaded = AppSettingsService.Load(settingsPath);

            Assert.Equal(OutputFormat.Jpeg, loaded.OutputFormat);
            Assert.Equal(85, loaded.Quality);
            Assert.Null(loaded.OutputFolder);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_WhenValuesOutOfRange_UsesSanitizedDefaults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string settingsPath = Path.Combine(tempDir, "settings.json");

        try
        {
            string payload = JsonSerializer.Serialize(new
            {
                outputFormat = 999,
                quality = 500,
                outputFolder = "   "
            });
            File.WriteAllText(settingsPath, payload);

            AppSettings loaded = AppSettingsService.Load(settingsPath);

            Assert.Equal(OutputFormat.Jpeg, loaded.OutputFormat);
            Assert.Equal(85, loaded.Quality);
            Assert.Null(loaded.OutputFolder);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
