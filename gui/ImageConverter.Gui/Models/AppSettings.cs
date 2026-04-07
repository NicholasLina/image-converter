using System;

namespace ImageConverter.Gui.Models;

/// <summary>
/// Persistent user preferences for the main window.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Gets or sets the preferred output format.
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Jpeg;

    /// <summary>
    /// Gets or sets the preferred quality value.
    /// </summary>
    public int Quality { get; set; } = 85;

    /// <summary>
    /// Gets or sets the preferred output folder path.
    /// </summary>
    public string? OutputFolder { get; set; }

    /// <summary>
    /// Creates default settings values for first run or fallback scenarios.
    /// </summary>
    public static AppSettings Default() => new();

    /// <summary>
    /// Returns a sanitized copy that clamps invalid values and trims folder text.
    /// </summary>
    public AppSettings Sanitized()
    {
        OutputFormat format = Enum.IsDefined(typeof(OutputFormat), OutputFormat)
            ? OutputFormat
            : Models.OutputFormat.Jpeg;

        int quality = Math.Clamp(Quality, 1, 100);
        string? outputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder.Trim();

        return new AppSettings
        {
            OutputFormat = format,
            Quality = quality,
            OutputFolder = outputFolder
        };
    }
}
