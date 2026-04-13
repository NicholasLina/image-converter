using ImageConverter.Gui.Models;
using Xunit;

namespace ImageConverter.Tests.Models;

/// <summary>
/// Tests for OutputFormat enum and its extension methods.
/// </summary>
public class OutputFormatTests
{
    [Theory]
    [InlineData(OutputFormat.Jpeg, true)]
    [InlineData(OutputFormat.Avif, true)]
    [InlineData(OutputFormat.Png, false)]
    [InlineData(OutputFormat.WebP, false)]
    [InlineData(OutputFormat.Tiff, false)]
    [InlineData(OutputFormat.Bmp, false)]
    [InlineData(OutputFormat.Gif, false)]
    public void SupportsQuality_ReturnsCorrectValue(OutputFormat format, bool expected)
    {
        bool result = format.SupportsQuality();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(OutputFormat.Jpeg, "jpg")]
    [InlineData(OutputFormat.Png, "png")]
    [InlineData(OutputFormat.WebP, "webp")]
    [InlineData(OutputFormat.Avif, "avif")]
    [InlineData(OutputFormat.Tiff, "tiff")]
    [InlineData(OutputFormat.Bmp, "bmp")]
    [InlineData(OutputFormat.Gif, "gif")]
    public void FileExtension_ReturnsCorrectExtension(OutputFormat format, string expected)
    {
        string result = format.FileExtension();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FileExtension_DoesNotIncludeLeadingDot()
    {
        foreach (OutputFormat format in Enum.GetValues<OutputFormat>())
        {
            string extension = format.FileExtension();
            Assert.DoesNotContain(".", extension);
        }
    }

    [Fact]
    public void AllFormatsHaveValidExtensions()
    {
        foreach (OutputFormat format in Enum.GetValues<OutputFormat>())
        {
            string extension = format.FileExtension();
            Assert.NotNull(extension);
            Assert.NotEmpty(extension);
            Assert.DoesNotContain(" ", extension);
        }
    }

    [Fact]
    public void FileExtension_InvalidFormat_Throws()
    {
        var invalid = (OutputFormat)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => invalid.FileExtension());
    }
}
