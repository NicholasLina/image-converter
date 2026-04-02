namespace ImageConverter.Gui.Models;

/// <summary>
/// Supported output image formats for conversion.
/// </summary>
public enum OutputFormat
{
    /// <summary>JPEG format (lossy compression).</summary>
    Jpeg = 0,
    
    /// <summary>PNG format (lossless compression).</summary>
    Png = 1,
    
    /// <summary>WebP format (lossless compression in this implementation).</summary>
    WebP = 2,
    
    /// <summary>AVIF format (lossy compression).</summary>
    Avif = 3,
    
    /// <summary>TIFF format (lossless).</summary>
    Tiff = 4,
    
    /// <summary>BMP format (uncompressed).</summary>
    Bmp = 5,
    
    /// <summary>GIF format.</summary>
    Gif = 6
}

/// <summary>
/// Extension methods for <see cref="OutputFormat"/>.
/// </summary>
public static class OutputFormatExtensions
{
    /// <summary>
    /// Determines if the format supports quality settings.
    /// </summary>
    /// <param name="format">The output format to check.</param>
    /// <returns>True if the format supports quality adjustment (JPEG, AVIF), false otherwise.</returns>
    public static bool SupportsQuality(this OutputFormat format) =>
        format is OutputFormat.Jpeg or OutputFormat.Avif;

    /// <summary>
    /// Gets the file extension for the format (without the leading dot).
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <returns>The file extension string.</returns>
    public static string FileExtension(this OutputFormat format) =>
        format switch
        {
            OutputFormat.Jpeg => "jpg",
            OutputFormat.Png => "png",
            OutputFormat.WebP => "webp",
            OutputFormat.Avif => "avif",
            OutputFormat.Tiff => "tiff",
            OutputFormat.Bmp => "bmp",
            OutputFormat.Gif => "gif",
            _ => "img"
        };
}
