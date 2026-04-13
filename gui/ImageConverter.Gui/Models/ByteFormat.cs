using System;

namespace ImageConverter.Gui.Models;

/// <summary>
/// Utility class for formatting byte sizes into human-readable strings.
/// </summary>
public static class ByteFormat
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a byte count into a human-readable string with appropriate units.
    /// </summary>
    /// <param name="bytes">The number of bytes to format. Negative values are treated as 0.</param>
    /// <returns>A formatted string like "1.5 MB" or "256 B".</returns>
    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double value = bytes;
        int index = -1;

        while (value >= 1024 && index < Units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.##} {Units[index]}";
    }
}
