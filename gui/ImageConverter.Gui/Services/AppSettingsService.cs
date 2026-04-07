using System;
using System.IO;
using System.Text.Json;
using ImageConverter.Gui.Models;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Persists user-selected app settings to a local JSON file.
/// </summary>
public static class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string DefaultSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ImageConverter",
        "settings.json");

    public static AppSettings Load() => Load(DefaultSettingsFilePath);

    public static AppSettings Load(string settingsFilePath)
    {
        try
        {
            if (!File.Exists(settingsFilePath))
            {
                return AppSettings.Default();
            }

            string json = File.ReadAllText(settingsFilePath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings?.Sanitized() ?? AppSettings.Default();
        }
        catch
        {
            return AppSettings.Default();
        }
    }

    public static void Save(AppSettings settings) => Save(DefaultSettingsFilePath, settings);

    public static void Save(string settingsFilePath, AppSettings settings)
    {
        AppSettings safeSettings = settings.Sanitized();
        string? folder = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string json = JsonSerializer.Serialize(safeSettings, JsonOptions);
        File.WriteAllText(settingsFilePath, json);
    }
}
