using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ImageConverter.Gui.Models;

public sealed class ConversionJob : INotifyPropertyChanged
{
    private long? _estimatedSizeBytes;
    private string _status = "Queued";

    public ConversionJob(string inputPath, long sourceSizeBytes)
    {
        InputPath = inputPath;
        SourceSizeBytes = sourceSizeBytes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string InputPath { get; }

    public string FileName => Path.GetFileName(InputPath);

    public long SourceSizeBytes { get; }

    public string SourceSize => ByteFormat.Format(SourceSizeBytes);

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

    public string EstimatedSize =>
        EstimatedSizeBytes.HasValue ? ByteFormat.Format(EstimatedSizeBytes.Value) : "Estimating...";

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

public static class ByteFormat
{
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
