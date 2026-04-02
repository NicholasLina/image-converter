using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ImageConverter.Gui.Models;
using ImageConverter.Gui.Services;

namespace ImageConverter.Gui;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".avif",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".ico",
        ".hdr",
        ".pnm",
        ".ppm",
        ".pgm",
        ".pbm",
        ".dds",
        ".tga",
        ".qoi",
        ".exr"
    };

    private static readonly HashSet<string> SupportedExtensionSet =
        new(SupportedExtensions, StringComparer.OrdinalIgnoreCase);

    private static readonly FilePickerFileType ImageFilePickerType = new("Image Files")
    {
        Patterns = SupportedExtensions.Select(ext => $"*{ext}").ToArray(),
        MimeTypes = new[] { "image/*" },
        AppleUniformTypeIdentifiers = new[] { "public.image" }
    };

    private readonly ObservableCollection<ConversionJob> _jobs = new();
    private CancellationTokenSource? _estimateCts;
    private bool _isConverting;

    public MainWindow()
    {
        InitializeComponent();
        JobsGrid.ItemsSource = _jobs;
        FormatComboBox.SelectedIndex = 0;
        QualitySlider.Value = 85;
        OutputFolderTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "converted-images");
        UpdateQualityUi();
        SummaryText.Text = "Queue is empty.";
        EstimateSummaryText.Text = "Estimated output: --";
    }

    private async void AddFilesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        TopLevel? topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select image files",
                AllowMultiple = true,
                FileTypeFilter = new[] { ImageFilePickerType }
            });

        IEnumerable<string> paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

        await AddInputPathsAsync(paths);
    }

    private async void AddFolderButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        TopLevel? topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select folder to import",
                AllowMultiple = false
            });

        string? folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        List<string> filePaths = await Task.Run(() => EnumerateSupportedFiles(folderPath));
        await AddInputPathsAsync(filePaths);
    }

    private async void DropZone_OnDrop(object? sender, DragEventArgs e)
    {
        if (_isConverting || !e.DataTransfer.Contains(DataFormat.File))
        {
            return;
        }

        IEnumerable<IStorageItem>? storageItems = e.DataTransfer.TryGetFiles();
        if (storageItems is null)
        {
            return;
        }

        List<string> resolvedPaths = new();
        foreach (IStorageItem item in storageItems)
        {
            string? localPath = item.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                continue;
            }

            if (item is IStorageFolder)
            {
                resolvedPaths.AddRange(await Task.Run(() => EnumerateSupportedFiles(localPath)));
            }
            else
            {
                resolvedPaths.Add(localPath);
            }
        }

        await AddInputPathsAsync(resolvedPaths);
    }

    private void DropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void RemoveSelectedButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        List<ConversionJob> selectedJobs = JobsGrid.SelectedItems?
            .OfType<ConversionJob>()
            .ToList() ?? new List<ConversionJob>();

        if (selectedJobs.Count == 0)
        {
            SummaryText.Text = $"No rows selected. {_jobs.Count} queued.";
            return;
        }

        foreach (ConversionJob selectedJob in selectedJobs)
        {
            _jobs.Remove(selectedJob);
        }

        SummaryText.Text = $"Removed {selectedJobs.Count} file(s). {_jobs.Count} queued.";
        UpdateEstimateSummary();
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        _jobs.Clear();
        _estimateCts?.Cancel();
        ConversionProgressBar.Value = 0;
        SummaryText.Text = "Queue cleared.";
        EstimateSummaryText.Text = "Estimated output: --";
    }

    private async void EstimateButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RecalculateEstimatesAsync();
    }

    private void FormatComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateQualityUi();
        _ = RecalculateEstimatesAsync();
    }

    private void QualitySlider_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (QualityLabel is null) return;
        QualityLabel.Text = $"{(int)Math.Round(e.NewValue)}";
        _ = RecalculateEstimatesAsync();
    }

    private async void BrowseOutputButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopLevel? topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select output folder",
                AllowMultiple = false
            });

        string? folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            OutputFolderTextBox.Text = folderPath;
        }
    }

    private async void ConvertButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        if (_jobs.Count == 0)
        {
            SummaryText.Text = "Add files first.";
            return;
        }

        string? outputFolder = OutputFolderTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            SummaryText.Text = "Choose an output folder before converting.";
            return;
        }

        Directory.CreateDirectory(outputFolder);

        _isConverting = true;
        SetControlsEnabled(false);
        _estimateCts?.Cancel();
        ConversionProgressBar.Maximum = _jobs.Count;
        ConversionProgressBar.Value = 0;

        OutputFormat outputFormat = GetSelectedFormat();
        int quality = (int)Math.Round(QualitySlider.Value);
        int successCount = 0;
        int failureCount = 0;

        foreach (ConversionJob job in _jobs)
        {
            job.Status = "Converting...";
            string outputPath = BuildOutputPath(outputFolder, job, outputFormat);
            (bool success, string error) = await Task.Run(() =>
            {
                bool converted = RustInterop.ConvertImage(
                    job.InputPath,
                    outputPath,
                    outputFormat,
                    quality,
                    out string errorMessage);
                return (converted, errorMessage);
            });

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

            ConversionProgressBar.Value = successCount + failureCount;
        }

        _isConverting = false;
        SetControlsEnabled(true);
        UpdateEstimateSummary();
        SummaryText.Text = failureCount == 0
            ? $"Converted {successCount} file(s) successfully."
            : $"Completed with {failureCount} failure(s), {successCount} success(es).";
    }

    private async Task AddInputPathsAsync(IEnumerable<string> paths)
    {
        HashSet<string> existing = new(
            _jobs.Select(job => Path.GetFullPath(job.InputPath)),
            StringComparer.OrdinalIgnoreCase);

        List<ConversionJob> added = new();
        foreach (string rawPath in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(rawPath);
            if (!File.Exists(fullPath) || !IsSupportedInput(fullPath) || existing.Contains(fullPath))
            {
                continue;
            }

            long size = new FileInfo(fullPath).Length;
            ConversionJob job = new(fullPath, size);
            _jobs.Add(job);
            added.Add(job);
            existing.Add(fullPath);
        }

        if (added.Count == 0)
        {
            SummaryText.Text = $"No new supported files added. {_jobs.Count} queued.";
            UpdateEstimateSummary();
            return;
        }

        SummaryText.Text = $"Added {added.Count} file(s). {_jobs.Count} queued.";
        await RecalculateEstimatesAsync(added);
    }

    private async Task RecalculateEstimatesAsync(IEnumerable<ConversionJob>? subset = null)
    {
        if (_jobs.Count == 0)
        {
            EstimateSummaryText.Text = "Estimated output: --";
            return;
        }

        _estimateCts?.Cancel();
        _estimateCts = new CancellationTokenSource();
        CancellationToken token = _estimateCts.Token;

        List<ConversionJob> targets = (subset ?? _jobs).ToList();
        OutputFormat outputFormat = GetSelectedFormat();
        int quality = (int)Math.Round(QualitySlider.Value);

        foreach (ConversionJob job in targets)
        {
            job.EstimatedSizeBytes = null;
        }

        try
        {
            foreach (ConversionJob job in targets)
            {
                token.ThrowIfCancellationRequested();
                long? estimate = await Task.Run(
                    () => RustInterop.EstimateOutputSize(job.InputPath, outputFormat, quality),
                    token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                job.EstimatedSizeBytes = estimate;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        UpdateEstimateSummary();
    }

    private void UpdateEstimateSummary()
    {
        if (_jobs.Count == 0)
        {
            EstimateSummaryText.Text = "Estimated output: --";
            return;
        }

        long sum = 0;
        int pending = 0;

        foreach (ConversionJob job in _jobs)
        {
            if (job.EstimatedSizeBytes.HasValue)
            {
                sum += job.EstimatedSizeBytes.Value;
            }
            else
            {
                pending++;
            }
        }

        string estimateText = $"Estimated output: {ByteFormat.Format(sum)}";
        if (pending > 0)
        {
            estimateText += $" (+ {pending} pending)";
        }

        EstimateSummaryText.Text = estimateText;
    }

    private void UpdateQualityUi()
    {
        OutputFormat outputFormat = GetSelectedFormat();
        bool supportsQuality = outputFormat.SupportsQuality();

        QualitySlider.IsEnabled = supportsQuality && !_isConverting;
        QualityHelpText.Text = supportsQuality
            ? "(used for JPEG/AVIF)"
            : "(quality not applicable)";
    }

    private void SetControlsEnabled(bool enabled)
    {
        AddFilesButton.IsEnabled = enabled;
        AddFolderButton.IsEnabled = enabled;
        RemoveSelectedButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
        EstimateButton.IsEnabled = enabled;
        FormatComboBox.IsEnabled = enabled;
        BrowseOutputButton.IsEnabled = enabled;
        OutputFolderTextBox.IsEnabled = enabled;
        ConvertButton.IsEnabled = enabled;
        UpdateQualityUi();
    }

    private OutputFormat GetSelectedFormat()
    {
        if (FormatComboBox.SelectedItem is ComboBoxItem comboBoxItem &&
            comboBoxItem.Tag is string tag &&
            Enum.TryParse(tag, true, out OutputFormat outputFormat))
        {
            return outputFormat;
        }

        return OutputFormat.Jpeg;
    }

    private static string BuildOutputPath(string outputFolder, ConversionJob job, OutputFormat outputFormat)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(job.FileName);
        string extension = outputFormat.FileExtension();
        string baseCandidate = $"{fileNameWithoutExtension}.{extension}";
        string candidatePath = Path.Combine(outputFolder, baseCandidate);

        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        int suffix = 1;
        while (true)
        {
            string deduplicatedName = $"{fileNameWithoutExtension}_{suffix}.{extension}";
            string deduplicatedPath = Path.Combine(outputFolder, deduplicatedName);
            if (!File.Exists(deduplicatedPath))
            {
                return deduplicatedPath;
            }

            suffix++;
        }
    }

    private static bool IsSupportedInput(string path) =>
        SupportedExtensionSet.Contains(Path.GetExtension(path));

    private static List<string> EnumerateSupportedFiles(string folderPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedInput)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string Truncate(string text, int maxLength) =>
        string.IsNullOrWhiteSpace(text) || text.Length <= maxLength
            ? text
            : $"{text[..(maxLength - 3)]}...";
}