using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ImageConverter.Gui.Models;

/// <summary>
/// Represents a single image conversion job with its metadata and status.
/// </summary>
public sealed class ConversionJob : INotifyPropertyChanged
{
    private long? _estimatedSizeBytes;
    private string _status = "Queued";

    /// <summary>
    /// Initializes a new conversion job.
    /// </summary>
    /// <param name="inputPath">The full path to the input image file.</param>
    /// <param name="sourceSizeBytes">The size of the source file in bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputPath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="inputPath"/> is empty or whitespace.</exception>
    public ConversionJob(string inputPath, long sourceSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be empty or whitespace.", nameof(inputPath));
        }

        InputPath = inputPath;
        SourceSizeBytes = Math.Max(0, sourceSizeBytes);
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the full path to the input image file.
    /// </summary>
    public string InputPath { get; }

    /// <summary>
    /// Gets the file name (without directory path).
    /// </summary>
    public string FileName => Path.GetFileName(InputPath);

    /// <summary>
    /// Gets the size of the source file in bytes.
    /// </summary>
    public long SourceSizeBytes { get; }

    /// <summary>
    /// Gets the formatted source file size (e.g., "1.5 MB").
    /// </summary>
    public string SourceSize => ByteFormat.Format(SourceSizeBytes);

    /// <summary>
    /// Gets or sets the estimated output size in bytes.
    /// </summary>
    public long? EstimatedSizeBytes
    {
        get => _estimatedSizeBytes;
        set
        {
            if (_estimatedSizeBytes == value)
            {
                return;
            }

            _estimatedSizeBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EstimatedSize));
        }
    }

    /// <summary>
    /// Gets the formatted estimated output size.
    /// </summary>
    public string EstimatedSize =>
        EstimatedSizeBytes.HasValue ? ByteFormat.Format(EstimatedSizeBytes.Value) : "Estimating...";

    /// <summary>
    /// Gets or sets the current status of the conversion job.
    /// </summary>
    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
