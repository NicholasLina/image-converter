using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ImageConverter.Gui.Models;
using ImageConverter.Gui.Services;

namespace ImageConverter.Gui;

/// <summary>
/// Main window for the Image Converter application.
/// Provides UI for batch image conversion with drag-and-drop support.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private static readonly FilePickerFileType ImageFilePickerType = new("Image Files")
    {
        Patterns = FileSystemService.GetSupportedExtensions().Select(ext => $"*{ext}").ToArray(),
        MimeTypes = new[] { "image/*" },
        AppleUniformTypeIdentifiers = new[] { "public.image" }
    };

    private readonly ObservableCollection<ConversionJob> _jobs = new();
    private readonly ConversionService _conversionService = new(new RustInterop());
    private readonly string _defaultOutputFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "converted-images");
    private CancellationTokenSource? _estimateCts;
    private bool _isConverting;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, MainWindow_OnKeyDown, RoutingStrategies.Tunnel);
        JobsGrid.ItemsSource = _jobs;
        LoadSettings();
        UpdateQualityUi();
        SummaryText.Text = "No images yet. Add files or drop a folder to begin.";
        EstimateSummaryText.Text = "Estimated output: --";
        UpdateConvertButtonLabel();
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

        List<string> filePaths = await Task.Run(() => FileSystemService.EnumerateSupportedFiles(folderPath));
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
                resolvedPaths.AddRange(await Task.Run(() => FileSystemService.EnumerateSupportedFiles(localPath)));
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

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && shift && e.Key == Key.O)
        {
            AddFolderButton_OnClick(AddFolderButton, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (ctrl && !shift && e.Key == Key.O)
        {
            AddFilesButton_OnClick(AddFilesButton, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            RemoveSelectedButton_OnClick(RemoveSelectedButton, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.Enter)
        {
            ConvertButton_OnClick(ConvertButton, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
        }
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
        UpdateConvertButtonLabel();
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        _jobs.Clear();
        _estimateCts?.Cancel();
        _estimateCts?.Dispose();
        _estimateCts = null;
        ConversionProgressBar.Value = 0;
        SummaryText.Text = "List cleared.";
        EstimateSummaryText.Text = "Estimated output: --";
        UpdateConvertButtonLabel();
    }

    private async void EstimateButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RecalculateEstimatesAsync();
    }

    private void FormatComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateQualityUi();
        _ = RecalculateEstimatesAsync();
        SaveSettings();
    }

    private void QualitySlider_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (QualityLabel is null) return;
        QualityLabel.Text = $"{(int)Math.Round(e.NewValue)}";
        _ = RecalculateEstimatesAsync();
        SaveSettings();
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
            SaveSettings();
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
            SummaryText.Text = "Add at least one image to convert.";
            return;
        }

        string? outputFolder = OutputFolderTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            SummaryText.Text = "Choose an output folder before converting.";
            return;
        }

        _isConverting = true;
        UpdateConvertButtonLabel();
        SetControlsEnabled(false);
        _estimateCts?.Cancel();
        ConversionProgressBar.Maximum = _jobs.Count;
        ConversionProgressBar.Value = 0;

        OutputFormat outputFormat = GetSelectedFormat();
        int quality = (int)Math.Round(QualitySlider.Value);

        (int successCount, int failureCount) = await _conversionService.ConvertBatchAsync(
            _jobs,
            outputFolder,
            outputFormat,
            quality,
            progressCallback: (job, success, failure) => ConversionProgressBar.Value = success + failure);

        _isConverting = false;
        SetControlsEnabled(true);
        UpdateConvertButtonLabel();
        UpdateEstimateSummary();
        SummaryText.Text = failureCount == 0
            ? $"Converted {successCount} file(s) successfully."
            : $"Converted {successCount} file(s); {failureCount} failed. Review statuses for details.";
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
            if (!File.Exists(fullPath) || !FileSystemService.IsSupportedInput(fullPath) || existing.Contains(fullPath))
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
            SummaryText.Text = $"No new supported images added. {_jobs.Count} queued.";
            UpdateEstimateSummary();
            UpdateConvertButtonLabel();
            return;
        }

        SummaryText.Text = $"Added {added.Count} image(s). {_jobs.Count} queued.";
        UpdateConvertButtonLabel();
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
        _estimateCts?.Dispose();
        _estimateCts = new CancellationTokenSource();
        CancellationToken token = _estimateCts.Token;

        List<ConversionJob> targets = (subset ?? _jobs).ToList();
        OutputFormat outputFormat = GetSelectedFormat();
        int quality = (int)Math.Round(QualitySlider.Value);

        try
        {
            await _conversionService.EstimateBatchAsync(targets, outputFormat, quality, token);
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
        UpdateConvertButtonLabel();
    }

    private void UpdateConvertButtonLabel()
    {
        if (_isConverting)
        {
            ConvertButton.Content = "Converting...";
            return;
        }

        ConvertButton.Content = _jobs.Count switch
        {
            0 => "Convert files",
            1 => "Convert 1 file",
            _ => $"Convert {_jobs.Count} files"
        };
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

    private void LoadSettings()
    {
        AppSettings settings = AppSettingsService.Load();

        int selectedIndex = settings.OutputFormat switch
        {
            OutputFormat.Jpeg => 0,
            OutputFormat.Png => 1,
            OutputFormat.WebP => 2,
            OutputFormat.Avif => 3,
            OutputFormat.Tiff => 4,
            OutputFormat.Bmp => 5,
            OutputFormat.Gif => 6,
            _ => 0
        };

        FormatComboBox.SelectedIndex = selectedIndex;
        QualitySlider.Value = settings.Quality;
        OutputFolderTextBox.Text = settings.OutputFolder ?? _defaultOutputFolder;
    }

    private void SaveSettings()
    {
        if (!IsInitialized)
        {
            return;
        }

        AppSettings settings = new()
        {
            OutputFormat = GetSelectedFormat(),
            Quality = (int)Math.Round(QualitySlider.Value),
            OutputFolder = OutputFolderTextBox.Text?.Trim()
        };

        AppSettingsService.Save(settings);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// Releases managed resources held by this window.
    /// </summary>
    public void Dispose()
    {
        _estimateCts?.Cancel();
        _estimateCts?.Dispose();
        _estimateCts = null;
    }
}