using ImageConverter.Gui.Models;
using ImageConverter.Gui.Services;
using Xunit;

namespace ImageConverter.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ConversionService"/> using a fake <see cref="IImageConverter"/>.
/// These tests verify service logic without needing the Rust native library.
/// </summary>
public class ConversionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConversionService _sut;
    private readonly FakeImageConverter _fakeConverter;

    public ConversionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ConvSvcTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _fakeConverter = new FakeImageConverter();
        _sut = new ConversionService(_fakeConverter);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); }
            catch { }
        }
    }

    [Fact]
    public void Constructor_NullConverter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConversionService(null!));
    }

    [Fact]
    public async Task ConvertImageAsync_NullJob_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ConvertImageAsync(null!, _tempDir, OutputFormat.Jpeg, 85));
    }

    [Fact]
    public async Task ConvertImageAsync_NullOutputFolder_Throws()
    {
        var job = new ConversionJob("/fake/path.png", 100);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ConvertImageAsync(job, null!, OutputFormat.Jpeg, 85));
    }

    [Fact]
    public async Task ConvertImageAsync_DelegatesToConverter()
    {
        string inputPath = Path.Combine(_tempDir, "input.png");
        File.WriteAllText(inputPath, "fake image");
        var job = new ConversionJob(inputPath, 100);
        string outputDir = Path.Combine(_tempDir, "out");

        var (success, error) = await _sut.ConvertImageAsync(job, outputDir, OutputFormat.Jpeg, 85);

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(1, _fakeConverter.ConvertCallCount);
    }

    [Fact]
    public async Task ConvertImageAsync_WhenConverterFails_ReturnsError()
    {
        _fakeConverter.ShouldFail = true;
        _fakeConverter.ErrorToReturn = "test error";

        string inputPath = Path.Combine(_tempDir, "input.png");
        File.WriteAllText(inputPath, "fake image");
        var job = new ConversionJob(inputPath, 100);
        string outputDir = Path.Combine(_tempDir, "out");

        var (success, error) = await _sut.ConvertImageAsync(job, outputDir, OutputFormat.Png, 85);

        Assert.False(success);
        Assert.Equal("test error", error);
    }

    [Fact]
    public async Task EstimateOutputSizeAsync_DelegatesToConverter()
    {
        _fakeConverter.EstimateToReturn = 42_000;
        var job = new ConversionJob("/fake/path.png", 100);

        long? estimate = await _sut.EstimateOutputSizeAsync(job, OutputFormat.Jpeg, 85);

        Assert.Equal(42_000, estimate);
    }

    [Fact]
    public async Task EstimateBatchAsync_NullJobs_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.EstimateBatchAsync(null!, OutputFormat.Jpeg, 85));
    }

    [Fact]
    public async Task EstimateBatchAsync_UpdatesAllJobs()
    {
        _fakeConverter.EstimateToReturn = 1000;
        var jobs = new[]
        {
            new ConversionJob("/fake/a.png", 100),
            new ConversionJob("/fake/b.png", 200)
        };

        await _sut.EstimateBatchAsync(jobs, OutputFormat.Jpeg, 85);

        Assert.All(jobs, j => Assert.Equal(1000L, j.EstimatedSizeBytes));
    }

    [Fact]
    public async Task EstimateBatchAsync_RespectsCancellation()
    {
        _fakeConverter.EstimateToReturn = 1000;
        var jobs = new[]
        {
            new ConversionJob("/fake/a.png", 100),
            new ConversionJob("/fake/b.png", 200),
            new ConversionJob("/fake/c.png", 300)
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.EstimateBatchAsync(jobs, OutputFormat.Jpeg, 85, cts.Token));
    }

    [Fact]
    public async Task ConvertBatchAsync_NullJobs_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ConvertBatchAsync(null!, _tempDir, OutputFormat.Jpeg, 85));
    }

    [Fact]
    public async Task ConvertBatchAsync_NullOutputFolder_Throws()
    {
        var jobs = new[] { new ConversionJob("/fake/a.png", 100) };
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ConvertBatchAsync(jobs, null!, OutputFormat.Jpeg, 85));
    }

    [Fact]
    public async Task ConvertBatchAsync_SuccessfulConversion_SetsStatusToDone()
    {
        var job = new ConversionJob("/fake/a.png", 100);
        string outputDir = Path.Combine(_tempDir, "batch_out");

        var (successCount, failureCount) = await _sut.ConvertBatchAsync(
            new[] { job }, outputDir, OutputFormat.Jpeg, 85);

        Assert.Equal(1, successCount);
        Assert.Equal(0, failureCount);
        Assert.Equal("Done", job.Status);
    }

    [Fact]
    public async Task ConvertBatchAsync_FailedConversion_SetsStatusToFailed()
    {
        _fakeConverter.ShouldFail = true;
        _fakeConverter.ErrorToReturn = "boom";

        var job = new ConversionJob("/fake/a.png", 100);
        string outputDir = Path.Combine(_tempDir, "batch_fail");

        var (successCount, failureCount) = await _sut.ConvertBatchAsync(
            new[] { job }, outputDir, OutputFormat.Jpeg, 85);

        Assert.Equal(0, successCount);
        Assert.Equal(1, failureCount);
        Assert.StartsWith("Failed:", job.Status);
    }

    [Fact]
    public async Task ConvertBatchAsync_InvokesProgressCallback()
    {
        var job = new ConversionJob("/fake/a.png", 100);
        string outputDir = Path.Combine(_tempDir, "progress");

        int callbackInvocations = 0;
        await _sut.ConvertBatchAsync(
            new[] { job },
            outputDir,
            OutputFormat.Jpeg,
            85,
            progressCallback: (_, _, _) => callbackInvocations++);

        Assert.Equal(1, callbackInvocations);
    }

    private sealed class FakeImageConverter : IImageConverter
    {
        public bool ShouldFail { get; set; }
        public string ErrorToReturn { get; set; } = "Fake error";
        public long? EstimateToReturn { get; set; } = 5000;
        public int ConvertCallCount { get; private set; }

        public bool ConvertImage(
            string inputPath,
            string outputPath,
            OutputFormat outputFormat,
            int quality,
            out string errorMessage)
        {
            ConvertCallCount++;

            if (ShouldFail)
            {
                errorMessage = ErrorToReturn;
                return false;
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(outputPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
            errorMessage = string.Empty;
            return true;
        }

        public long? EstimateOutputSize(
            string inputPath,
            OutputFormat outputFormat,
            int quality)
        {
            return EstimateToReturn;
        }
    }
}
