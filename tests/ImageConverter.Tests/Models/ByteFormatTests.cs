using ImageConverter.Gui.Models;
using Xunit;

namespace ImageConverter.Tests.Models;

/// <summary>
/// Tests for ByteFormat utility class.
/// </summary>
public class ByteFormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    public void Format_BytesUnder1024_ReturnsBytes(long bytes, string expected)
    {
        string result = ByteFormat.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(10240, "10 KB")]
    public void Format_Kilobytes_ReturnsKB(long bytes, string expected)
    {
        string result = ByteFormat.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1048576, "1 MB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(5242880, "5 MB")]
    public void Format_Megabytes_ReturnsMB(long bytes, string expected)
    {
        string result = ByteFormat.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1073741824, "1 GB")]
    [InlineData(2147483648, "2 GB")]
    public void Format_Gigabytes_ReturnsGB(long bytes, string expected)
    {
        string result = ByteFormat.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1099511627776, "1 TB")]
    [InlineData(2199023255552, "2 TB")]
    public void Format_Terabytes_ReturnsTB(long bytes, string expected)
    {
        string result = ByteFormat.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_RoundsToTwoDecimalPlaces()
    {
        long bytes = 1536 + 51; // 1.549 KB
        string result = ByteFormat.Format(bytes);
        Assert.Equal("1.55 KB", result);
    }

    [Fact]
    public void Format_DropsTrailingZeros()
    {
        long bytes = 1024; // Exactly 1 KB
        string result = ByteFormat.Format(bytes);
        Assert.Equal("1 KB", result);
    }

    [Fact]
    public void Format_HandlesLargeValues()
    {
        long bytes = long.MaxValue;
        string result = ByteFormat.Format(bytes);
        Assert.Contains("TB", result);
    }

    [Theory]
    [InlineData(-1, "0 B")]
    [InlineData(-1024, "0 B")]
    [InlineData(long.MinValue, "0 B")]
    public void Format_NegativeBytes_ReturnZeroBytes(long bytes, string expected)
    {
        string result = ByteFormat.Format(bytes);
        Assert.Equal(expected, result);
    }
}
