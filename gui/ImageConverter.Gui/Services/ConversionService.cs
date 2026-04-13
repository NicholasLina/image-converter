using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Gui.Models;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Manages image conversion operations and size estimation.
/// Accepts an <see cref="IImageConverter"/> so the native layer can be replaced in tests.
/// </summary>
public sealed class ConversionService
{
    private readonly IImageConverter _converter;

    /// <summary>
    /// Creates a new <see cref="ConversionService"/> backed by the given converter.
    /// </summary>
    /// <param name="converter">The image converter implementation to delegate to.</param>
    public ConversionService(IImageConverter converter)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    /// <summary>
    /// Converts a single image file to the specified format.
    /// </summary>
    /// <param name="job">The conversion job containing input file information.</param>
    /// <param name="outputFolder">The output directory.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">The quality setting (1-100) for lossy formats.</param>
    /// <returns>A tuple indicating success and any error message.</returns>
    public async Task<(bool success, string error)> ConvertImageAsync(
        ConversionJob job,
        string outputFolder,
        OutputFormat outputFormat,
        int quality)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(outputFolder);

        string baseFileName = Path.GetFileNameWithoutExtension(job.FileName);
        string extension = outputFormat.FileExtension();
        string outputPath = FileSystemService.BuildOutputPath(outputFolder, baseFileName, extension);

        return await ConvertImageToOutputPathAsync(job, outputPath, outputFormat, quality);
    }

    /// <summary>
    /// Estimates the output size for a single conversion job.
    /// </summary>
    /// <param name="job">The conversion job to estimate.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">The quality setting (1-100) for lossy formats.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The estimated size in bytes, or null if estimation failed.</returns>
    public async Task<long?> EstimateOutputSizeAsync(
        ConversionJob job,
        OutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        return await Task.Run(
            () => _converter.EstimateOutputSize(job.InputPath, outputFormat, quality),
            cancellationToken);
    }

    /// <summary>
    /// Estimates output sizes for multiple conversion jobs.
    /// </summary>
    /// <param name="jobs">The jobs to estimate.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">The quality setting (1-100) for lossy formats.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task EstimateBatchAsync(
        IEnumerable<ConversionJob> jobs,
        OutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobs);

        foreach (ConversionJob job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            job.EstimatedSizeBytes = null;
            long? estimate = await EstimateOutputSizeAsync(job, outputFormat, quality, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            job.EstimatedSizeBytes = estimate;
        }
    }

    /// <summary>
    /// Converts multiple images in batch.
    /// </summary>
    /// <param name="jobs">The jobs to convert.</param>
    /// <param name="outputFolder">The output directory.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">The quality setting (1-100) for lossy formats.</param>
    /// <param name="cancellationToken">Token to cancel the remaining jobs.</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <returns>A tuple with success count and failure count.</returns>
    public async Task<(int successCount, int failureCount)> ConvertBatchAsync(
        IEnumerable<ConversionJob> jobs,
        string outputFolder,
        OutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken = default,
        Action<ConversionJob, int, int>? progressCallback = null)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(outputFolder);

        Directory.CreateDirectory(outputFolder);

        int successCount = 0;
        int failureCount = 0;

        foreach (ConversionJob job in jobs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            job.Status = "Converting...";
            string baseFileName = Path.GetFileNameWithoutExtension(job.FileName);
            string extension = outputFormat.FileExtension();
            string outputPath = FileSystemService.BuildOutputPath(outputFolder, baseFileName, extension);

            (bool success, string error) = await ConvertImageToOutputPathAsync(
                job,
                outputPath,
                outputFormat,
                quality);

            if (success)
            {
                successCount++;
                job.Status = "Done";

                if (File.Exists(outputPath))
                {
                    job.EstimatedSizeBytes = new FileInfo(outputPath).Length;
                }
            }
            else
            {
                failureCount++;
                job.Status = $"Failed: {Truncate(error, 70)}";
            }

            progressCallback?.Invoke(job, successCount, failureCount);
        }

        return (successCount, failureCount);
    }

    private static string Truncate(string text, int maxLength) =>
        string.IsNullOrWhiteSpace(text) || text.Length <= maxLength
            ? text
            : $"{text[..(maxLength - 3)]}...";

    private async Task<(bool success, string error)> ConvertImageToOutputPathAsync(
        ConversionJob job,
        string outputPath,
        OutputFormat outputFormat,
        int quality)
    {
        return await Task.Run(() =>
        {
            bool converted = _converter.ConvertImage(
                job.InputPath,
                outputPath,
                outputFormat,
                quality,
                out string errorMessage);
            return (converted, errorMessage);
        });
    }
}
