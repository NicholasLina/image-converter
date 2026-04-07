using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Gui.Models;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Manages image conversion operations and size estimation.
/// </summary>
public class ConversionService
{
    /// <summary>
    /// Converts a single image file to the specified format.
    /// </summary>
    /// <param name="job">The conversion job containing input file information.</param>
    /// <param name="outputFolder">The output directory.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">The quality setting (1-100) for lossy formats.</param>
    /// <returns>A tuple indicating success and any error message.</returns>
    public static async Task<(bool success, string error)> ConvertImageAsync(
        ConversionJob job,
        string outputFolder,
        OutputFormat outputFormat,
        int quality)
    {
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
    public static async Task<long?> EstimateOutputSizeAsync(
        ConversionJob job,
        OutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () => RustInterop.EstimateOutputSize(job.InputPath, outputFormat, quality),
            cancellationToken);
    }

    /// <summary>
    /// Estimates output sizes for multiple conversion jobs.
    /// </summary>
    /// <param name="jobs">The jobs to estimate.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">The quality setting (1-100) for lossy formats.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public static async Task EstimateBatchAsync(
        IEnumerable<ConversionJob> jobs,
        OutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken = default)
    {
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
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <returns>A tuple with success count and failure count.</returns>
    public static async Task<(int successCount, int failureCount)> ConvertBatchAsync(
        IEnumerable<ConversionJob> jobs,
        string outputFolder,
        OutputFormat outputFormat,
        int quality,
        Action<ConversionJob, int, int>? progressCallback = null)
    {
        Directory.CreateDirectory(outputFolder);

        int successCount = 0;
        int failureCount = 0;

        foreach (ConversionJob job in jobs)
        {
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

    private static async Task<(bool success, string error)> ConvertImageToOutputPathAsync(
        ConversionJob job,
        string outputPath,
        OutputFormat outputFormat,
        int quality)
    {
        return await Task.Run(() =>
        {
            bool converted = RustInterop.ConvertImage(
                job.InputPath,
                outputPath,
                outputFormat,
                quality,
                out string errorMessage);
            return (converted, errorMessage);
        });
    }
}
