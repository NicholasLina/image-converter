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
    public ConversionJob(string inputPath, long sourceSizeBytes)
    {
        InputPath = inputPath;
        SourceSizeBytes = sourceSizeBytes;
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

/// <summary>
/// Utility class for formatting byte sizes into human-readable strings.
/// </summary>
public static class ByteFormat
{
    /// <summary>
    /// Formats a byte count into a human-readable string with appropriate units.
    /// </summary>
    /// <param name="bytes">The number of bytes to format.</param>
    /// <returns>A formatted string like "1.5 MB" or "256 B".</returns>
    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        string[] units = ["KB", "MB", "GB", "TB"];
        double value = bytes;
        int index = -1;

        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.##} {units[index]}";
    }
}
