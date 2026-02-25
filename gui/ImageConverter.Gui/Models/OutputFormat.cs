namespace ImageConverter.Gui.Models;

public enum OutputFormat
{
    Jpeg = 0,
    Png = 1,
    WebP = 2,
    Avif = 3,
    Tiff = 4,
    Bmp = 5,
    Gif = 6
}

public static class OutputFormatExtensions
{
    public static bool SupportsQuality(this OutputFormat format) =>
        format is OutputFormat.Jpeg or OutputFormat.Avif;

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
