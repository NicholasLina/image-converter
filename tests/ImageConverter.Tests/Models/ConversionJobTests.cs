using System.ComponentModel;
using ImageConverter.Gui.Models;
using Xunit;

namespace ImageConverter.Tests.Models;

/// <summary>
/// Tests for ConversionJob model.
/// </summary>
public class ConversionJobTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        const string inputPath = "/path/to/image.png";
        const long size = 1024;

        var job = new ConversionJob(inputPath, size);

        Assert.Equal(inputPath, job.InputPath);
        Assert.Equal(size, job.SourceSizeBytes);
        Assert.Equal("Queued", job.Status);
        Assert.Null(job.EstimatedSizeBytes);
    }

    [Theory]
    [InlineData("/path/to/image.png", "image.png")]
    [InlineData("/another/path/photo.jpg", "photo.jpg")]
    [InlineData("simple.gif", "simple.gif")]
    public void FileName_ExtractsCorrectFileName(string inputPath, string expectedFileName)
    {
        var job = new ConversionJob(inputPath, 1024);
        Assert.Equal(expectedFileName, job.FileName);
    }

    [Fact]
    public void SourceSize_FormatsCorrectly()
    {
        var job = new ConversionJob("/path/to/image.png", 2048);
        Assert.Equal("2 KB", job.SourceSize);
    }

    [Fact]
    public void EstimatedSize_WhenNull_ReturnsEstimating()
    {
        var job = new ConversionJob("/path/to/image.png", 1024);
        Assert.Equal("Estimating...", job.EstimatedSize);
    }

    [Fact]
    public void EstimatedSize_WhenSet_ReturnsFormattedSize()
    {
        var job = new ConversionJob("/path/to/image.png", 1024)
        {
            EstimatedSizeBytes = 3072
        };
        Assert.Equal("3 KB", job.EstimatedSize);
    }

    [Fact]
    public void Status_CanBeChanged()
    {
        var job = new ConversionJob("/path/to/image.png", 1024);
        Assert.Equal("Queued", job.Status);

        job.Status = "Converting...";
        Assert.Equal("Converting...", job.Status);

        job.Status = "Done";
        Assert.Equal("Done", job.Status);
    }

    [Fact]
    public void PropertyChanged_RaisedWhenStatusChanges()
    {
        var job = new ConversionJob("/path/to/image.png", 1024);
        string? changedPropertyName = null;

        job.PropertyChanged += (sender, args) => changedPropertyName = args.PropertyName;

        job.Status = "Converting...";

        Assert.Equal("Status", changedPropertyName);
    }

    [Fact]
    public void PropertyChanged_RaisedWhenEstimatedSizeBytesChanges()
    {
        var job = new ConversionJob("/path/to/image.png", 1024);
        var changedProperties = new List<string>();

        job.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        job.EstimatedSizeBytes = 2048;

        Assert.Contains("EstimatedSizeBytes", changedProperties);
        Assert.Contains("EstimatedSize", changedProperties);
    }

    [Fact]
    public void PropertyChanged_NotRaisedWhenSettingSameStatus()
    {
        var job = new ConversionJob("/path/to/image.png", 1024);
        int eventCount = 0;

        job.PropertyChanged += (sender, args) => eventCount++;

        job.Status = "Queued"; // Same as initial value
        Assert.Equal(0, eventCount);

        job.Status = "Converting...";
        Assert.Equal(1, eventCount);

        job.Status = "Converting..."; // Same as current value
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void PropertyChanged_NotRaisedWhenSettingSameEstimatedSize()
    {
        var job = new ConversionJob("/path/to/image.png", 1024);
        int eventCount = 0;

        job.PropertyChanged += (sender, args) => eventCount++;

        job.EstimatedSizeBytes = null; // Same as initial value
        Assert.Equal(0, eventCount);

        job.EstimatedSizeBytes = 2048;
        Assert.Equal(2, eventCount); // Both EstimatedSizeBytes and EstimatedSize

        job.EstimatedSizeBytes = 2048; // Same as current value
        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void Constructor_NullInputPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConversionJob(null!, 100));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceInputPath_Throws(string inputPath)
    {
        Assert.Throws<ArgumentException>(() => new ConversionJob(inputPath, 100));
    }

    [Fact]
    public void Constructor_NegativeSize_ClampsToZero()
    {
        var job = new ConversionJob("/path/to/image.png", -500);
        Assert.Equal(0, job.SourceSizeBytes);
    }
}
