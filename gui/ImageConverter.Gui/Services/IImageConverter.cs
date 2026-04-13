using ImageConverter.Gui.Models;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Abstracts image conversion operations so they can be mocked in tests.
/// </summary>
public interface IImageConverter
{
    /// <summary>
    /// Converts an image file to the specified format.
    /// </summary>
    /// <param name="inputPath">Path to the input image file.</param>
    /// <param name="outputPath">Path where the converted image will be saved.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">Quality setting (1-100) for lossy formats.</param>
    /// <param name="errorMessage">Output parameter containing error details if conversion fails.</param>
    /// <returns>True if conversion succeeded, false otherwise.</returns>
    bool ConvertImage(
        string inputPath,
        string outputPath,
        OutputFormat outputFormat,
        int quality,
        out string errorMessage);

    /// <summary>
    /// Estimates the output file size for a conversion without writing to disk.
    /// </summary>
    /// <param name="inputPath">Path to the input image file.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">Quality setting (1-100) for lossy formats.</param>
    /// <returns>The estimated size in bytes, or null if estimation failed.</returns>
    long? EstimateOutputSize(
        string inputPath,
        OutputFormat outputFormat,
        int quality);
}
