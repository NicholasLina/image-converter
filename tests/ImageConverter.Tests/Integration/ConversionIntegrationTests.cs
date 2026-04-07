using ImageConverter.Gui.Models;
using ImageConverter.Gui.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace ImageConverter.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end image conversion workflows.
/// These tests verify the complete conversion pipeline from input to output.
/// </summary>
public class ConversionIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testImagePath;

    public ConversionIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ImageConverterTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        
        _testImagePath = Path.Combine(_tempDir, "test_source.png");
        CreateTestImage(_testImagePath, 100, 100);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static void CreateTestImage(string path, int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        
        // Create a simple gradient pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)((x * 255) / width);
                byte g = (byte)((y * 255) / height);
                byte b = (byte)(((x + y) * 255) / (width + height));
                image[x, y] = new Rgba32(r, g, b, 255);
            }
        }
        
        image.SaveAsPng(path);
    }

    [Theory]
    [InlineData(OutputFormat.Jpeg)]
    [InlineData(OutputFormat.Png)]
    [InlineData(OutputFormat.WebP)]
    [InlineData(OutputFormat.Tiff)]
    [InlineData(OutputFormat.Bmp)]
    [InlineData(OutputFormat.Gif)]
    public async Task ConvertImageAsync_AllFormats_ProducesValidOutput(OutputFormat format)
    {
        var job = new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length);
        string outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(outputDir);

        var (success, error) = await ConversionService.ConvertImageAsync(job, outputDir, format, 85);

        Assert.True(success, $"Conversion failed: {error}");
        
        string expectedExtension = format.FileExtension();
        var outputFiles = Directory.GetFiles(outputDir, $"*.{expectedExtension}");
        Assert.Single(outputFiles);
        Assert.True(new FileInfo(outputFiles[0]).Length > 0);
    }

    [Fact]
    public async Task ConvertImageAsync_WithQuality_ProducesOutput()
    {
        var job = new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length);
        string outputDir = Path.Combine(_tempDir, "quality_test");
        Directory.CreateDirectory(outputDir);

        var (success, error) = await ConversionService.ConvertImageAsync(
            job, outputDir, OutputFormat.Jpeg, 50);

        Assert.True(success, $"Conversion failed: {error}");
        
        var outputFiles = Directory.GetFiles(outputDir, "*.jpg");
        Assert.Single(outputFiles);
    }

    [Fact]
    public async Task EstimateOutputSizeAsync_ReturnsValidEstimate()
    {
        var job = new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length);

        long? estimate = await ConversionService.EstimateOutputSizeAsync(
            job, OutputFormat.Jpeg, 85);

        Assert.NotNull(estimate);
        Assert.True(estimate > 0);
    }

    [Fact]
    public async Task EstimateBatchAsync_UpdatesAllJobs()
    {
        var jobs = new[]
        {
            new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length),
            new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length)
        };

        await ConversionService.EstimateBatchAsync(jobs, OutputFormat.Jpeg, 85);

        Assert.All(jobs, job => Assert.NotNull(job.EstimatedSizeBytes));
        Assert.All(jobs, job => Assert.True(job.EstimatedSizeBytes > 0));
    }

    [Fact]
    public async Task ConvertBatchAsync_ConvertsMultipleImages()
    {
        string image1 = Path.Combine(_tempDir, "image1.png");
        string image2 = Path.Combine(_tempDir, "image2.png");
        CreateTestImage(image1, 50, 50);
        CreateTestImage(image2, 75, 75);

        var jobs = new[]
        {
            new ConversionJob(image1, new FileInfo(image1).Length),
            new ConversionJob(image2, new FileInfo(image2).Length)
        };

        string outputDir = Path.Combine(_tempDir, "batch_output");

        var (successCount, failureCount) = await ConversionService.ConvertBatchAsync(
            jobs, outputDir, OutputFormat.Jpeg, 85);

        Assert.Equal(2, successCount);
        Assert.Equal(0, failureCount);
        Assert.All(jobs, job => Assert.Equal("Done", job.Status));
        Assert.All(jobs, job => Assert.NotNull(job.EstimatedSizeBytes));
        Assert.All(jobs, job => Assert.True(job.EstimatedSizeBytes > 0));
        
        var outputFiles = Directory.GetFiles(outputDir, "*.jpg");
        Assert.Equal(2, outputFiles.Length);
    }

    [Fact]
    public async Task ConvertBatchAsync_WithProgress_InvokesCallback()
    {
        var job = new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length);
        string outputDir = Path.Combine(_tempDir, "progress_test");
        
        int callbackCount = 0;
        ConversionJob? lastJob = null;

        var (successCount, failureCount) = await ConversionService.ConvertBatchAsync(
            new[] { job },
            outputDir,
            OutputFormat.Jpeg,
            85,
            (j, s, f) =>
            {
                callbackCount++;
                lastJob = j;
            });

        Assert.Equal(1, callbackCount);
        Assert.NotNull(lastJob);
        Assert.Equal(job, lastJob);
    }

    [Fact]
    public async Task ConvertImageAsync_NonExistentInput_ReturnsFailure()
    {
        string nonExistentPath = Path.Combine(_tempDir, "nonexistent.png");
        var job = new ConversionJob(nonExistentPath, 0);
        string outputDir = Path.Combine(_tempDir, "error_test");

        var (success, error) = await ConversionService.ConvertImageAsync(
            job, outputDir, OutputFormat.Jpeg, 85);

        Assert.False(success);
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task EstimateOutputSizeAsync_NonExistentInput_ReturnsNull()
    {
        string nonExistentPath = Path.Combine(_tempDir, "nonexistent.png");
        var job = new ConversionJob(nonExistentPath, 0);

        long? estimate = await ConversionService.EstimateOutputSizeAsync(
            job, OutputFormat.Jpeg, 85);

        Assert.Null(estimate);
    }

    [Fact]
    public async Task ConvertBatchAsync_MixedResults_ReportsCorrectCounts()
    {
        string validImage = Path.Combine(_tempDir, "valid.png");
        string invalidImage = Path.Combine(_tempDir, "invalid.png");
        CreateTestImage(validImage, 50, 50);

        var jobs = new[]
        {
            new ConversionJob(validImage, new FileInfo(validImage).Length),
            new ConversionJob(invalidImage, 0)
        };

        string outputDir = Path.Combine(_tempDir, "mixed_test");

        var (successCount, failureCount) = await ConversionService.ConvertBatchAsync(
            jobs, outputDir, OutputFormat.Jpeg, 85);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Equal("Done", jobs[0].Status);
        Assert.StartsWith("Failed:", jobs[1].Status);
    }

    [Fact]
    public void FileSystemService_BuildOutputPath_HandlesCollisions()
    {
        string outputDir = Path.Combine(_tempDir, "collision_test");
        Directory.CreateDirectory(outputDir);

        string firstPath = FileSystemService.BuildOutputPath(outputDir, "test", "jpg");
        File.WriteAllText(firstPath, "test");

        string secondPath = FileSystemService.BuildOutputPath(outputDir, "test", "jpg");

        Assert.NotEqual(firstPath, secondPath);
        Assert.EndsWith("test_1.jpg", secondPath);
    }

    [Fact]
    public void FileSystemService_EnumerateSupportedFiles_FindsImages()
    {
        string searchDir = Path.Combine(_tempDir, "search_test");
        Directory.CreateDirectory(searchDir);
        
        CreateTestImage(Path.Combine(searchDir, "image1.png"), 10, 10);
        CreateTestImage(Path.Combine(searchDir, "image2.png"), 10, 10);
        File.WriteAllText(Path.Combine(searchDir, "readme.txt"), "test");

        var files = FileSystemService.EnumerateSupportedFiles(searchDir);

        Assert.Equal(2, files.Count);
        Assert.All(files, file => Assert.True(FileSystemService.IsSupportedInput(file)));
    }

    [Theory]
    [InlineData(20, 90)]
    [InlineData(30, 80)]
    [InlineData(50, 70)]
    public async Task EstimateOutputSize_DifferentQualities_ProducesDifferentSizes(
        int lowQuality, int highQuality)
    {
        var job = new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length);

        long? lowEstimate = await ConversionService.EstimateOutputSizeAsync(
            job, OutputFormat.Jpeg, lowQuality);
        long? highEstimate = await ConversionService.EstimateOutputSizeAsync(
            job, OutputFormat.Jpeg, highQuality);

        Assert.NotNull(lowEstimate);
        Assert.NotNull(highEstimate);
        Assert.True(highEstimate > lowEstimate,
            $"High quality ({highQuality}) should produce larger file than low quality ({lowQuality})");
    }

    [Fact]
    public async Task ConvertImageAsync_CreatesOutputDirectory()
    {
        var job = new ConversionJob(_testImagePath, new FileInfo(_testImagePath).Length);
        string outputDir = Path.Combine(_tempDir, "auto_create", "nested", "dir");

        var (success, error) = await ConversionService.ConvertImageAsync(
            job, outputDir, OutputFormat.Jpeg, 85);

        Assert.True(success, $"Conversion failed: {error}");
        Assert.True(Directory.Exists(outputDir));
    }
}
