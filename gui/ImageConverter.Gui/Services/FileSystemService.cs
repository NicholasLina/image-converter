using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Provides file system operations for image file discovery and validation.
/// </summary>
public class FileSystemService
{
    private static readonly string[] SupportedExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".avif",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".ico",
        ".hdr",
        ".pnm",
        ".ppm",
        ".pgm",
        ".pbm",
        ".dds",
        ".tga",
        ".qoi",
        ".exr"
    };

    private static readonly HashSet<string> SupportedExtensionSet =
        new(SupportedExtensions, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the array of supported file extensions.
    /// </summary>
    public static IReadOnlyList<string> GetSupportedExtensions() => SupportedExtensions;

    /// <summary>
    /// Checks if a file path has a supported image extension.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the file extension is supported, false otherwise.</returns>
    public static bool IsSupportedInput(string path) =>
        SupportedExtensionSet.Contains(Path.GetExtension(path));

    /// <summary>
    /// Recursively enumerates all supported image files in a directory.
    /// </summary>
    /// <param name="folderPath">The directory path to search.</param>
    /// <returns>A list of file paths with supported extensions.</returns>
    public static List<string> EnumerateSupportedFiles(string folderPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedInput)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Builds an output file path with collision-safe naming.
    /// If the file exists, appends _1, _2, etc. until a unique name is found.
    /// </summary>
    /// <param name="outputFolder">The output directory.</param>
    /// <param name="baseFileName">The base file name without extension.</param>
    /// <param name="extension">The file extension (without dot).</param>
    /// <returns>A unique output file path.</returns>
    public static string BuildOutputPath(string outputFolder, string baseFileName, string extension)
    {
        string baseCandidate = $"{baseFileName}.{extension}";
        string candidatePath = Path.Combine(outputFolder, baseCandidate);

        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        int suffix = 1;
        while (true)
        {
            string deduplicatedName = $"{baseFileName}_{suffix}.{extension}";
            string deduplicatedPath = Path.Combine(outputFolder, deduplicatedName);
            if (!File.Exists(deduplicatedPath))
            {
                return deduplicatedPath;
            }

            suffix++;
        }
    }
}
