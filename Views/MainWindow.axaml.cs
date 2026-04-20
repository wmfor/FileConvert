using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FileConvert.Models;
using FileConvert.ViewModels;
using ImageMagick;

namespace FileConvert.Views;

public partial class MainWindow : Window
{
    // ── Static converters used in the batch-queue DataTemplate ────────────
    public static readonly FuncValueConverter<BatchStatus, string> StatusToSymbol =
        new(s => s switch
        {
            BatchStatus.Pending    => "○",
            BatchStatus.Converting => "◌",
            BatchStatus.Done       => "●",
            BatchStatus.Error      => "✕",
            _                      => "?"
        });

    public static readonly FuncValueConverter<BatchStatus, IBrush> StatusToColor =
        new(s => s switch
        {
            BatchStatus.Done       => new SolidColorBrush(Color.Parse("#51cf66")),
            BatchStatus.Error      => new SolidColorBrush(Color.Parse("#ff6b6b")),
            BatchStatus.Converting => new SolidColorBrush(Color.Parse("#339af0")),
            _                      => new SolidColorBrush(Color.Parse("#555555"))
        });

    // ── State ─────────────────────────────────────────────────────────────
    private bool   _isConversionValid;
    private bool   _isResolutionValid;
    private bool   _isFilePresent;
    private int    _activeTab            = 0;   // 0=Image  1=Audio  2=Video
    private bool   _isBatchMode          = false;
    private bool   _isHistoryVisible     = false;
    private bool   _aspectRatioLocked    = false;
    private double _aspectRatio          = 1.0;
    private bool   _suppressResolutionUpdate = false;
    private CancellationTokenSource? _conversionCts;
    private int    _inputFileCategory    = -1;  // 0=Image 1=Video 2=Audio -1=none
    private bool   _atvUseImage          = false;
    private string _atvImagePath         = "";
    private string _atvColor             = "1a1a1a";

    // ── File type filter constants ─────────────────────────────────────────
    private static readonly FilePickerFileType AllSupportedTypes = new("All Supported Files")
    {
        Patterns = new[]
        {
            "*.png","*.jpg","*.jpeg","*.webp","*.gif","*.bmp","*.tiff","*.tif",
            "*.ico","*.avif","*.heic",
            "*.mp4","*.mov","*.avi","*.mkv","*.webm","*.flv","*.wmv","*.ts","*.m4v",
            "*.mp3","*.wav","*.flac","*.ogg","*.aac","*.m4a","*.opus","*.aiff","*.wma"
        }
    };
    private static readonly FilePickerFileType ImageTypes = new("Image Files")
    {
        Patterns = new[] { "*.png","*.jpg","*.jpeg","*.webp","*.gif","*.bmp","*.tiff","*.tif","*.ico","*.avif","*.heic" }
    };
    private static readonly FilePickerFileType VideoTypes = new("Video Files")
    {
        Patterns = new[] { "*.mp4","*.mov","*.avi","*.mkv","*.webm","*.flv","*.wmv","*.ts","*.m4v" }
    };
    private static readonly FilePickerFileType AudioTypes = new("Audio Files")
    {
        Patterns = new[] { "*.mp3","*.wav","*.flac","*.ogg","*.aac","*.m4a","*.opus","*.aiff","*.wma" }
    };

    // ── Window state persistence ──────────────────────────────────────────
    private static string WindowStatePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "FileConvert", "window.json");

    // ── Constructor ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        SetUIResolution(false, 0, 0);
        SetupDragDrop();
        LoadWindowState();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        _conversionCts?.Cancel();
        SaveWindowState();
    }

    private void LoadWindowState()
    {
        try
        {
            if (!File.Exists(WindowStatePath)) return;
            using var doc  = JsonDocument.Parse(File.ReadAllText(WindowStatePath));
            var        root = doc.RootElement;
            if (root.TryGetProperty("w", out var w) && root.TryGetProperty("h", out var h))
            {
                Width  = Math.Max(900,  w.GetDouble());
                Height = Math.Max(620, h.GetDouble());
            }
            if (root.TryGetProperty("x", out var x) && root.TryGetProperty("y", out var y))
                Position = new Avalonia.PixelPoint(x.GetInt32(), y.GetInt32());
        }
        catch { }
    }

    private void SaveWindowState()
    {
        try
        {
            string dir = Path.GetDirectoryName(WindowStatePath)!;
            Directory.CreateDirectory(dir);
            var obj = new { w = Width, h = Height, x = Position.X, y = Position.Y };
            File.WriteAllText(WindowStatePath,
                JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void SetupDragDrop()
    {
        InputFileBorder.AddHandler(DragDrop.DropEvent,      OnFileDrop);
        InputFileBorder.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        InputFileBorder.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        var previewBorder = SingleFilePanel.Children.OfType<Border>().FirstOrDefault();
        if (previewBorder != null)
        {
            previewBorder.AddHandler(DragDrop.DropEvent,      OnFileDrop);
            previewBorder.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            previewBorder.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  DRAG AND DROP
    // ──────────────────────────────────────────────────────────────────────

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            DragHintOverlay.IsVisible = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DragHintOverlay.IsVisible = false;
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        DragHintOverlay.IsVisible = false;

        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0) return;

        var paths = files.Select(f => f.Path.ToString().Remove(0, 8)).ToList();

        if (paths.Count > 1 && !_isBatchMode)
        {
            _isBatchMode = true;
            BatchModeToggle.Classes.Set("batchActive", true);
            SingleFilePanel.IsVisible    = false;
            BatchPanel.IsVisible         = true;
            GenerateButton.IsVisible     = false;
            BatchConvertButton.IsVisible = true;
        }

        if (_isBatchMode)
        {
            (DataContext as MainWindowViewModel)!.AddFilesToBatch(paths);
            UpdateBatchQueueLabel();
            UpdateBatchConvertButton();
        }
        else
        {
            LoadFile(paths[0]);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  TAB MANAGEMENT
    // ──────────────────────────────────────────────────────────────────────

    private void OnTabButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        SwitchToTab(int.Parse(btn.Tag!.ToString()!));
        UpdateTypeConversionAccessibility();
    }

    private void SwitchToTab(int tabIndex)
    {
        _activeTab = tabIndex;

        TabImageButton.Classes.Set("tabActive", tabIndex == 0);
        TabAudioButton.Classes.Set("tabActive", tabIndex == 1);
        TabVideoButton.Classes.Set("tabActive", tabIndex == 2);

        ImageFormatPanel.IsVisible = tabIndex == 0;
        AudioFormatPanel.IsVisible = tabIndex == 1;
        VideoFormatPanel.IsVisible = tabIndex == 2;

        ResolutionPanel.IsVisible = tabIndex != 1; // hide for audio

        WrapPanel panel = tabIndex switch { 0 => ImageFormatPanel, 1 => AudioFormatPanel, _ => VideoFormatPanel };
        bool first = true;
        foreach (var child in panel.Children)
        {
            if (child is not Button chip) continue;
            chip.Classes.Set("fmtActive", first);
            if (first)
            {
                (DataContext as MainWindowViewModel)!.SelectedConversionType = chip.Tag!.ToString()!;
                first = false;
            }
        }
    }

    private void AutoSwitchTabForFileType()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        var match = MainWindowViewModel.FileTypes.FirstOrDefault(x =>
            x.Item2.Equals(vm.SelectedFileType, StringComparison.OrdinalIgnoreCase));
        if (match == default) return;

        int uiTab = match.Item1 switch { 0 => 0, 1 => 2, 2 => 1, _ => 0 };
        SwitchToTab(uiTab);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  FORMAT CHIP SELECTION
    // ──────────────────────────────────────────────────────────────────────

    private void OnFormatChipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button chip) return;
        WrapPanel panel = _activeTab switch { 0 => ImageFormatPanel, 1 => AudioFormatPanel, _ => VideoFormatPanel };
        foreach (var child in panel.Children)
            if (child is Button b) b.Classes.Set("fmtActive", b == chip);

        (DataContext as MainWindowViewModel)!.SelectedConversionType = chip.Tag!.ToString()!;
        UpdateTypeConversionAccessibility();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  HISTORY PANEL
    // ──────────────────────────────────────────────────────────────────────

    private void OnHistoryToggleClicked(object? sender, RoutedEventArgs e)
    {
        _isHistoryVisible = !_isHistoryVisible;
        HistoryToggle.Classes.Set("batchActive", _isHistoryVisible);

        HistoryPanel.IsVisible = _isHistoryVisible;
        if (_isHistoryVisible)
        {
            SingleFilePanel.IsVisible = false;
            BatchPanel.IsVisible      = false;
        }
        else
        {
            SingleFilePanel.IsVisible = !_isBatchMode;
            BatchPanel.IsVisible      = _isBatchMode;
        }
    }

    private void OnHistoryClearClicked(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)!.ConversionHistory.Clear();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  BATCH MODE
    // ──────────────────────────────────────────────────────────────────────

    private void OnBatchModeToggleClicked(object? sender, RoutedEventArgs e)
    {
        _isBatchMode = !_isBatchMode;
        BatchModeToggle.Classes.Set("batchActive", _isBatchMode);

        if (!_isHistoryVisible)
        {
            SingleFilePanel.IsVisible    = !_isBatchMode;
            BatchPanel.IsVisible         = _isBatchMode;
        }

        GenerateButton.IsVisible     = !_isBatchMode;
        BatchConvertButton.IsVisible = _isBatchMode;

        if (_isBatchMode) UpdateBatchConvertButton();
        else              IsGenerationViable();
    }

    private async void OnBatchAddFilesClicked(object? sender, RoutedEventArgs e)
    {
        var files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title         = "Add Files to Batch",
                AllowMultiple = true,
                FileTypeFilter = new[] { AllSupportedTypes, ImageTypes, VideoTypes, AudioTypes }
            });

        if (files.Count == 0) return;

        var vm = (DataContext as MainWindowViewModel)!;
        vm.AddFilesToBatch(files.Select(f => f.Path.ToString().Remove(0, 8)));
        UpdateBatchQueueLabel();
        UpdateBatchConvertButton();
    }

    private void OnBatchClearClicked(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)!.ClearBatch();
        UpdateBatchQueueLabel();
        UpdateBatchConvertButton();
    }

    private void UpdateBatchQueueLabel()
    {
        int n = (DataContext as MainWindowViewModel)!.BatchQueue.Count;
        BatchHeaderLabel.Text  = $"BATCH QUEUE  ({n})";
        BatchProgressText.Text = n == 0 ? "No files queued" : $"{n} file{(n == 1 ? "" : "s")} queued";
    }

    private void UpdateBatchConvertButton()
    {
        int n = (DataContext as MainWindowViewModel)!.BatchQueue.Count;
        BatchConvertButton.Content   = $"Convert All  ({n})";
        BatchConvertButton.IsEnabled = n > 0;
    }

    private async void OnBatchConvertClicked(object? sender, RoutedEventArgs e)
    {
        var vm      = (DataContext as MainWindowViewModel)!;
        var pending = vm.BatchQueue.Where(x => x.Status == BatchStatus.Pending).ToList();
        if (pending.Count == 0) return;

        string? outputFolder = await ResolveOutputFolder();
        if (outputFolder == null) return;

        vm.Settings!.LastUsedFolder = outputFolder;

        // Capture all shared conversion options from UI state BEFORE any parallel work
        string outExt        = vm.SelectedConversionType.ToLower();
        var    settings      = vm.Settings;
        string preset        = settings.VideoPreset;
        string bitrate       = settings.AudioBitrate;
        bool   overwrite     = settings.OverwriteExistingFiles;
        bool   hwAccel       = settings.UseHardwareAcceleration;
        int    threads       = settings.ThreadCount;
        bool   atvUseImage   = _atvUseImage;
        string atvImagePath  = _atvImagePath;
        string atvColor      = _atvColor;
        string trimStart     = TrimStartInput.Text?.Trim() ?? "";
        string trimEnd       = TrimEndInput.Text?.Trim()   ?? "";
        bool   stripMeta     = settings.StripImageMetadata;
        int    quality       = (int)QualitySlider.Value;
        bool   doResize      = AreResolutionInputsValid() && ResolutionPanel.IsVisible;
        int    resW          = doResize && int.TryParse(ResolutionWidthInput.Text,  out int w) ? w : 0;
        int    resH          = doResize && int.TryParse(ResolutionHeightInput.Text, out int h) ? h : 0;
        int    maxParallel   = settings.MaxParallelConversions;

        // Pre-generate all output filenames sequentially to avoid duplicates under parallel execution
        var outputNames = new List<string>(pending.Count);
        foreach (var item in pending)
        {
            vm.SelectedFileName = Path.GetFileNameWithoutExtension(item.FilePath);
            outputNames.Add(vm.GetFileOutputName(outputFolder));
        }

        _conversionCts               = new CancellationTokenSource();
        BatchConvertButton.IsVisible = false;
        CancelButton.IsVisible       = true;
        ConversionProgress.IsVisible = true;
        BatchProgressBar.IsVisible   = true;
        BatchProgressBar.Maximum     = pending.Count;
        BatchProgressBar.Value       = 0;

        int completedCount = 0;
        var semaphore = new SemaphoreSlim(maxParallel);

        var tasks = pending.Select(async (item, idx) =>
        {
            await semaphore.WaitAsync(_conversionCts.Token).ConfigureAwait(false);
            try
            {
                if (_conversionCts.IsCancellationRequested)
                {
                    item.Status = BatchStatus.Pending;
                    return;
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() => item.Status = BatchStatus.Converting);

                // Determine category from item's file type (thread-safe, no shared state)
                var match = MainWindowViewModel.FileTypes.FirstOrDefault(x =>
                    x.Item2.Equals(item.FileType, StringComparison.OrdinalIgnoreCase));
                int itemCategory = match.Item2 != null ? match.Item1 : -1;

                bool useFfmpeg = GetUseFfmpegForTypes(item.FileType, vm.SelectedConversionType);
                var sw = Stopwatch.StartNew();

                bool success = await ConvertFileAsync(
                    item.FilePath, item.FileType, itemCategory,
                    outputFolder, outputNames[idx],
                    outExt, useFfmpeg,
                    preset, bitrate, overwrite, hwAccel, threads,
                    atvUseImage, atvImagePath, atvColor,
                    trimStart, trimEnd,
                    stripMeta, quality, doResize, resW, resH,
                    _conversionCts.Token, null);

                sw.Stop();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => item.Status = success ? BatchStatus.Done : BatchStatus.Error);

                long inputBytes  = File.Exists(item.FilePath) ? new FileInfo(item.FilePath).Length : 0;
                string outPath   = Path.Combine(outputFolder, outputNames[idx] + "." + outExt);
                long outputBytes = success && File.Exists(outPath) ? new FileInfo(outPath).Length : 0;

                vm.AddToHistory(new ConversionHistoryEntry
                {
                    InputName   = item.FileName,
                    FromExt     = item.FileType.ToUpper(),
                    ToExt       = outExt.ToUpper(),
                    Success     = success,
                    InputBytes  = inputBytes,
                    OutputBytes = outputBytes,
                    ElapsedMs   = (int)sw.ElapsedMilliseconds,
                    OutputPath  = success ? outPath : null,
                });

                int done = Interlocked.Increment(ref completedCount);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    BatchProgressBar.Value = done;
                    BatchProgressText.Text = maxParallel > 1
                        ? $"{done} / {pending.Count} done  (×{maxParallel} parallel)"
                        : $"Converting {done} / {pending.Count}…";
                });
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _conversionCts.Dispose();
        _conversionCts = null;

        Title = "FileConvert";
        ConversionProgress.IsVisible = false;
        CancelButton.IsVisible       = false;
        BatchConvertButton.IsVisible = true;
        BatchProgressText.Text       = $"Done — {completedCount} file{(completedCount == 1 ? "" : "s")} converted.";
        OutputButton.IsEnabled       = true;
        BatchConvertButton.IsEnabled = true;

        HandleAfterConversion(outputFolder, null);
    }

    // Helper to determine ffmpeg usage without relying on vm state (thread-safe for parallel)
    private static bool GetUseFfmpegForTypes(string inputType, string outputType)
    {
        bool outputIsAV = MainWindowViewModel.FileTypes.Any(x =>
            (x.Item1 == 1 || x.Item1 == 2) &&
            x.Item2.Equals(outputType, StringComparison.OrdinalIgnoreCase));

        if (!outputIsAV) return false;

        if (outputType.Equals("GIF", StringComparison.OrdinalIgnoreCase))
        {
            bool inputIsImage = MainWindowViewModel.FileTypes.Any(x =>
                x.Item1 == 0 &&
                x.Item2.Equals(inputType, StringComparison.OrdinalIgnoreCase));
            if (inputIsImage) return false;
        }

        return true;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  SINGLE FILE SELECT
    // ──────────────────────────────────────────────────────────────────────

    private void OnSelectFileButtonClicked(object? sender, RoutedEventArgs e) =>
        OpenFileSelectWindow();

    private async void OpenFileSelectWindow()
    {
        var files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title          = "Select File to Convert",
                AllowMultiple  = false,
                FileTypeFilter = new[] { AllSupportedTypes, ImageTypes, VideoTypes, AudioTypes }
            });

        if (files.Count != 1) return;
        LoadFile(files[0].Path.ToString().Remove(0, 8));
    }

    private async void LoadFile(string path)
    {
        var vm = (DataContext as MainWindowViewModel)!;
        vm.SelectedFilePath  = path;
        vm.SelectedFileName  = Path.GetFileNameWithoutExtension(path);
        vm.SelectedFileType  = Path.GetExtension(path).TrimStart('.');

        var _ft = MainWindowViewModel.FileTypes.FirstOrDefault(x =>
            x.Item2.Equals(vm.SelectedFileType, StringComparison.OrdinalIgnoreCase));
        _inputFileCategory = _ft.Item2 != null ? _ft.Item1 : -1;

        SelectedFileNameDisplay.Text       = vm.SelectedFileNameWithType;
        SelectedFileNameDisplay.Foreground = new SolidColorBrush(Color.Parse("#cccccc"));
        MetaFileName.Text  = vm.SelectedFileNameWithType;
        MetaFileType.Text  = vm.SelectedFileType.ToUpper();
        MetaFileSize.Text  = "—";
        ClearFileButton.IsVisible    = true;
        CopyPathButton.IsVisible     = false;
        SelectedImage.Source         = null;
        VideoPreviewBadge.IsVisible  = false;
        ClearMediaInfoRows();
        PreviewLoadingBar.IsVisible  = true;

        // Show trim panel for audio/video
        TrimPanel.IsVisible = _inputFileCategory == 1 || _inputFileCategory == 2;

        AutoSwitchTabForFileType();
        UpdateTypeConversionAccessibility();
        UpdateSelectedFileSize();

        await Task.WhenAll(UpdateImageAsync(), UpdateFileMetadataAsync());

        PreviewLoadingBar.IsVisible = false;
    }

    private void OnClearFileClicked(object? sender, RoutedEventArgs e)
    {
        var vm = (DataContext as MainWindowViewModel)!;
        vm.SelectedFilePath = null;
        vm.SelectedFileName = "";
        vm.SelectedFileType = "";

        SelectedFileNameDisplay.Text       = "No file selected — click Browse or drop a file here";
        SelectedFileNameDisplay.Foreground = new SolidColorBrush(Color.Parse("#4a4a4a"));
        MetaFileName.Text = "—";
        MetaFileType.Text = "—";
        MetaFileSize.Text = "—";

        SelectedImage.Source         = null;
        VideoPreviewBadge.IsVisible  = false;
        PreviewPlaceholder.IsVisible = false;
        ClearFileButton.IsVisible    = false;
        TrimPanel.IsVisible          = false;
        TrimStartInput.Text          = "";
        TrimEndInput.Text            = "";
        ClearMediaInfoRows();
        SetUIResolution(false, 0, 0);

        _isFilePresent     = false;
        _isConversionValid = false;
        _inputFileCategory = -1;
        ConversionArrowLabel.Text = "";
        UpdateTabVisibility();
        UpdateOptionsPanel();
        IsGenerationViable();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  TRIM INPUTS
    // ──────────────────────────────────────────────────────────────────────

    private void OnTrimInputChanged(object? sender, TextChangedEventArgs e)
    {
        // Visual feedback for invalid time format
        if (sender is not TextBox box) return;
        string val = box.Text?.Trim() ?? "";
        bool valid = IsValidTrimTime(val);
        box.BorderBrush = valid
            ? new SolidColorBrush(Color.Parse("#383838"))
            : new SolidColorBrush(Color.Parse("#ff6b6b"));
    }

    private static bool IsValidTrimTime(string t) =>
        string.IsNullOrEmpty(t) ||
        Regex.IsMatch(t, @"^\d{1,2}:\d{2}:\d{2}(\.\d+)?$") ||
        Regex.IsMatch(t, @"^\d+(\.\d+)?$");

    // ──────────────────────────────────────────────────────────────────────
    //  CONVERSION VALIDATION
    // ──────────────────────────────────────────────────────────────────────

    private void UpdateTypeConversionAccessibility()
    {
        var vm = (DataContext as MainWindowViewModel)!;

        var from = MainWindowViewModel.FileTypes.FirstOrDefault(x =>
            x.Item2.Equals(vm.SelectedFileType, StringComparison.OrdinalIgnoreCase));
        int originalType = from == default ? -1 : from.Item1;

        int desiredType;
        if (vm.SelectedConversionType.Equals("GIF", StringComparison.OrdinalIgnoreCase))
            desiredType = _activeTab == 2 ? 1 : 0;
        else
        {
            var to = MainWindowViewModel.FileTypes.FirstOrDefault(x =>
                x.Item2.Equals(vm.SelectedConversionType, StringComparison.OrdinalIgnoreCase));
            desiredType = to == default ? -1 : to.Item1;
        }

        _isFilePresent = originalType != -1;

        _isConversionValid =
            originalType == desiredType ||
            (originalType == 1 && desiredType == 2) ||
            (originalType == 2 && desiredType == 1) ||
            (originalType == 1 && desiredType == 1 &&
             vm.SelectedConversionType.Equals("GIF", StringComparison.OrdinalIgnoreCase));

        UpdateTabVisibility();
        UpdateOptionsPanel();
        IsGenerationViable();
        UpdateConversionLabel();
    }

    private void UpdateConversionLabel()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        if (!_isFilePresent || string.IsNullOrEmpty(vm.SelectedFileType))
        {
            ConversionArrowLabel.Text = "";
            return;
        }
        ConversionArrowLabel.Text =
            $"{vm.SelectedFileType.ToUpper()}  →  {vm.SelectedConversionType.ToUpper()}";
    }

    private void UpdateTabVisibility()
    {
        bool showImage = !_isFilePresent || _inputFileCategory == 0;
        bool showAudio = !_isFilePresent || _inputFileCategory == 1 || _inputFileCategory == 2;
        bool showVideo = !_isFilePresent || _inputFileCategory == 1 || _inputFileCategory == 2;

        TabImageButton.IsVisible = showImage;
        TabAudioButton.IsVisible = showAudio;
        TabVideoButton.IsVisible = showVideo;
    }

    private void UpdateOptionsPanel()
    {
        bool atv = _isFilePresent && _inputFileCategory == 2 && _activeTab == 2;
        AudioToVideoPanel.IsVisible = atv;
        if (atv) ResolutionPanel.IsVisible = false;
    }

    // ── AUDIO → VIDEO background options ──────────────────────────────────

    private void OnATVModeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _atvUseImage = btn.Tag?.ToString() == "image";
        ATVColorChip.Classes.Set("fmtActive", !_atvUseImage);
        ATVImageChip.Classes.Set("fmtActive",  _atvUseImage);
        ATVColorRow.IsVisible  = !_atvUseImage;
        ATVImageRow.IsVisible  =  _atvUseImage;
        IsGenerationViable();
    }

    private void OnATVColorChanged(object? sender, TextChangedEventArgs e)
    {
        string hex = (ATVColorBox.Text ?? "").Trim().TrimStart('#');
        _atvColor = hex;
        try { ATVColorPreview.Background = new SolidColorBrush(Color.Parse("#" + hex)); }
        catch { }
    }

    private async void OnATVImageBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title          = "Select Background Image",
                AllowMultiple  = false,
                FileTypeFilter = new[] { ImageTypes }
            });
        if (files.Count != 1) return;
        _atvImagePath = files[0].Path.ToString().Remove(0, 8);
        ATVImagePathBox.Text = _atvImagePath;
        IsGenerationViable();
    }

    private void IsGenerationViable()
    {
        bool atvImageMissing = _isFilePresent && _inputFileCategory == 2 && _activeTab == 2
                               && _atvUseImage && string.IsNullOrEmpty(_atvImagePath);

        if (!_isFilePresent)
            ErrorText.Text = "No file selected.";
        else if (!_isConversionValid)
            ErrorText.Text = "Conversion not available for this file type.";
        else if (!_isResolutionValid)
            ErrorText.Text = "Resolution values are not valid.";
        else if (atvImageMissing)
            ErrorText.Text = "Select a background image.";
        else
            ErrorText.Text = "";

        bool viable = _isResolutionValid && _isConversionValid && _isFilePresent && !atvImageMissing;
        GenerateButton.IsEnabled = viable;
        UpdateOutputPreview(viable);
    }

    private void UpdateOutputPreview(bool viable)
    {
        if (!viable) { OutputPreviewText.Text = ""; return; }
        var vm = (DataContext as MainWindowViewModel)!;
        var s  = vm.Settings;
        if (s == null) { OutputPreviewText.Text = ""; return; }
        string ext  = vm.SelectedConversionType.ToLower();
        OutputPreviewText.Text = $"→  {s.GetPreviewOutputName(vm.SelectedFileName, ext)}";
    }

    private void OnResolutionInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressResolutionUpdate) return;

        if (_aspectRatioLocked && _aspectRatio > 0 && sender is TextBox changed)
        {
            _suppressResolutionUpdate = true;
            try
            {
                if (changed == ResolutionWidthInput &&
                    int.TryParse(ResolutionWidthInput.Text, out int w) && w > 0)
                {
                    ResolutionHeightInput.Text = Math.Max(1, (int)Math.Round(w / _aspectRatio)).ToString();
                }
                else if (changed == ResolutionHeightInput &&
                         int.TryParse(ResolutionHeightInput.Text, out int h) && h > 0)
                {
                    ResolutionWidthInput.Text = Math.Max(1, (int)Math.Round(h * _aspectRatio)).ToString();
                }
            }
            finally { _suppressResolutionUpdate = false; }
        }

        _isResolutionValid = AreResolutionInputsValid();
        IsGenerationViable();
    }

    private void OnAspectRatioLockClicked(object? sender, RoutedEventArgs e)
    {
        _aspectRatioLocked = !_aspectRatioLocked;

        if (_aspectRatioLocked &&
            int.TryParse(ResolutionWidthInput.Text, out int w) &&
            int.TryParse(ResolutionHeightInput.Text, out int h) &&
            w > 0 && h > 0)
        {
            _aspectRatio = (double)w / h;
        }

        AspectRatioLockButton.Background  = _aspectRatioLocked
            ? new SolidColorBrush(Color.Parse("#0d2a4a"))
            : new SolidColorBrush(Color.Parse("#1e1e1e"));
        AspectRatioLockButton.Foreground  = _aspectRatioLocked
            ? new SolidColorBrush(Color.Parse("#4db8ff"))
            : new SolidColorBrush(Color.Parse("#555555"));
        AspectRatioLockButton.BorderBrush = _aspectRatioLocked
            ? new SolidColorBrush(Color.Parse("#0063b1"))
            : new SolidColorBrush(Color.Parse("#333333"));
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isBatchMode &&
            GenerateButton.IsEnabled && GenerateButton.IsVisible)
        {
            _ = GenerateSingleAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFilePresent && !_isBatchMode)
        {
            OnClearFileClicked(null, null!);
            e.Handled = true;
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        _conversionCts?.Cancel();
    }

    private void OnBatchItemRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not BatchFileItem item) return;
        (DataContext as MainWindowViewModel)!.BatchQueue.Remove(item);
        UpdateBatchQueueLabel();
        UpdateBatchConvertButton();
    }

    private void OnBatchListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && BatchQueueList.SelectedItem is BatchFileItem item)
        {
            (DataContext as MainWindowViewModel)!.BatchQueue.Remove(item);
            UpdateBatchQueueLabel();
            UpdateBatchConvertButton();
            e.Handled = true;
        }
    }

    private void OnBatchClearDoneClicked(object? sender, RoutedEventArgs e)
    {
        var vm   = (DataContext as MainWindowViewModel)!;
        var done = vm.BatchQueue
            .Where(x => x.Status == BatchStatus.Done || x.Status == BatchStatus.Error)
            .ToList();
        foreach (var item in done) vm.BatchQueue.Remove(item);
        UpdateBatchQueueLabel();
        UpdateBatchConvertButton();
    }

    private string? _lastOutputPath;

    private void OnCopyPathClicked(object? sender, RoutedEventArgs e)
    {
        if (_lastOutputPath == null) return;
        _ = GetTopLevel(this)?.Clipboard?.SetTextAsync(_lastOutputPath);
        CopyPathButton.Content = "Copied!";
        Task.Delay(1500).ContinueWith(_ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CopyPathButton.Content = "Copy Path"));
    }

    private bool AreResolutionInputsValid()
    {
        if (!ResolutionPanel.IsVisible) return true;
        string w = ResolutionWidthInput.Text  ?? "";
        string h = ResolutionHeightInput.Text ?? "";
        if (w == "" || h == "") return false;
        if (!Regex.IsMatch(w, @"^\d+$") || !Regex.IsMatch(h, @"^\d+$")) return false;
        return int.Parse(w) > 0 && int.Parse(h) > 0;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  GENERATE (single mode)
    // ──────────────────────────────────────────────────────────────────────

    private void OnGenerateButtonClicked(object? sender, RoutedEventArgs e) =>
        _ = GenerateSingleAsync();

    private async Task GenerateSingleAsync()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        if (string.IsNullOrEmpty(vm.SelectedFilePath) || vm.SelectedFilePath.Length <= 3) return;

        string? outputFolder = await ResolveOutputFolder();
        if (outputFolder == null) return;

        vm.Settings!.LastUsedFolder = outputFolder;
        string outputFileName = vm.GetFileOutputName(outputFolder);

        // Capture all UI state before async work
        var    settings    = vm.Settings;
        string outExt      = vm.SelectedConversionType.ToLower();
        bool   useFfmpeg   = vm.GetIsUsingFfmpeg();
        string preset      = settings.VideoPreset;
        string bitrate     = settings.AudioBitrate;
        bool   overwrite   = settings.OverwriteExistingFiles;
        bool   hwAccel     = settings.UseHardwareAcceleration;
        int    threads     = settings.ThreadCount;
        bool   atvUseImage = _atvUseImage;
        string atvImgPath  = _atvImagePath;
        string atvColor    = _atvColor;
        string trimStart   = TrimStartInput.Text?.Trim() ?? "";
        string trimEnd     = TrimEndInput.Text?.Trim()   ?? "";
        bool   stripMeta   = settings.StripImageMetadata;
        int    quality     = (int)QualitySlider.Value;
        bool   doResize    = AreResolutionInputsValid() && ResolutionPanel.IsVisible;
        int    resW        = doResize && int.TryParse(ResolutionWidthInput.Text,  out int rw) ? rw : 0;
        int    resH        = doResize && int.TryParse(ResolutionHeightInput.Text, out int rh) ? rh : 0;
        string inputPath   = vm.SelectedFilePath;
        string inputType   = vm.SelectedFileType;
        int    inputCat    = _inputFileCategory;

        _conversionCts                     = new CancellationTokenSource();
        GenerateButton.IsVisible           = false;
        CancelButton.IsVisible             = true;
        ConversionProgress.IsIndeterminate = true;
        ConversionProgress.IsVisible       = true;
        ConversionPercentText.IsVisible    = false;
        OutputPreviewText.Text             = "";
        ErrorText.Text                     = "";

        var convProgress = new Progress<double>(pct =>
        {
            ConversionProgress.IsIndeterminate = false;
            ConversionProgress.Value           = pct;
            ConversionPercentText.Text         = $"{pct:F0}%";
            ConversionPercentText.IsVisible    = true;
        });

        var sw = Stopwatch.StartNew();

        bool convSuccess = await ConvertFileAsync(
            inputPath, inputType, inputCat,
            outputFolder, outputFileName,
            outExt, useFfmpeg,
            preset, bitrate, overwrite, hwAccel, threads,
            atvUseImage, atvImgPath, atvColor,
            trimStart, trimEnd,
            stripMeta, quality, doResize, resW, resH,
            _conversionCts.Token, convProgress);

        sw.Stop();
        bool cancelled = _conversionCts.IsCancellationRequested;
        _conversionCts.Dispose();
        _conversionCts = null;

        string outputPath = convSuccess
            ? Path.Combine(outputFolder, outputFileName + "." + outExt)
            : "";

        ConversionProgress.IsVisible       = false;
        ConversionProgress.IsIndeterminate = true;
        ConversionPercentText.IsVisible    = false;
        CancelButton.IsVisible             = false;
        GenerateButton.IsVisible           = true;
        GenerateButton.IsEnabled     = _isResolutionValid && _isConversionValid && _isFilePresent;
        OutputButton.IsEnabled       = true;

        if (cancelled)
        {
            ErrorText.Text       = "Conversion cancelled.";
            ErrorText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
            CopyPathButton.IsVisible = false;
        }
        else if (convSuccess && File.Exists(outputPath))
        {
            long inputBytes  = File.Exists(inputPath) ? new FileInfo(inputPath).Length : 0;
            long outputBytes = new FileInfo(outputPath).Length;
            string sizeInfo  = $"{FmtBytes(inputBytes)} → {FmtBytes(outputBytes)}";

            ErrorText.Text       = $"Converted successfully.  {sizeInfo}";
            ErrorText.Foreground = new SolidColorBrush(Color.Parse("#51cf66"));
            _lastOutputPath      = outputPath;
            CopyPathButton.IsVisible = true;
            CopyPathButton.IsEnabled = true;
            CopyPathButton.Content   = "Copy Path";

            vm.AddToHistory(new ConversionHistoryEntry
            {
                InputName   = Path.GetFileName(inputPath),
                FromExt     = inputType.ToUpper(),
                ToExt       = outExt.ToUpper(),
                Success     = true,
                InputBytes  = inputBytes,
                OutputBytes = outputBytes,
                ElapsedMs   = (int)sw.ElapsedMilliseconds,
                OutputPath  = outputPath,
            });

            HandleAfterConversion(outputFolder, outputPath);
        }
        else
        {
            ErrorText.Text       = "Conversion failed.";
            ErrorText.Foreground = new SolidColorBrush(Color.Parse("#ff6b6b"));
            CopyPathButton.IsVisible = false;

            vm.AddToHistory(new ConversionHistoryEntry
            {
                InputName  = Path.GetFileName(inputPath),
                FromExt    = inputType.ToUpper(),
                ToExt      = outExt.ToUpper(),
                Success    = false,
                ElapsedMs  = (int)sw.ElapsedMilliseconds,
            });
        }
    }

    private static string FmtBytes(long b)
    {
        if (b <= 0) return "—";
        if (b < 1_048_576) return $"{b / 1024.0:F1} KB";
        return $"{b / 1_048_576.0:F2} MB";
    }

    /// <summary>
    /// Core conversion. All parameters are explicit so this is safe to call in parallel.
    /// </summary>
    private async Task<bool> ConvertFileAsync(
        string inputPath, string inputType, int inputCategory,
        string outputFolder, string outputFileName,
        string outputExt, bool useFfmpeg,
        string preset, string bitrate, bool overwrite, bool hwAccel, int threads,
        bool atvUseImage, string atvImagePath, string atvColor,
        string trimStart, string trimEnd,
        bool stripMeta, int quality, bool doResize, int resW, int resH,
        CancellationToken ct = default, IProgress<double>? progress = null)
    {
        var vm = (DataContext as MainWindowViewModel)!;

        try
        {
            if (useFfmpeg)
            {
                string overwriteFlag = overwrite  ? "-y "              : "-n ";
                string hwFlag        = hwAccel    ? "-hwaccel auto "   : "";
                string threadFlag    = threads > 0 ? $"-threads {threads} " : "";
                string trimStartFlag = IsValidTrimTime(trimStart) && !string.IsNullOrEmpty(trimStart)
                    ? $"-ss {trimStart} " : "";
                string trimEndFlag   = IsValidTrimTime(trimEnd) && !string.IsNullOrEmpty(trimEnd)
                    ? $"-to {trimEnd} "   : "";

                var outEntry = MainWindowViewModel.FileTypes.FirstOrDefault(x =>
                    x.Item2.Equals(outputExt, StringComparison.OrdinalIgnoreCase));

                // Progress setup
                string progressFile = Path.Combine(Path.GetTempPath(), $"fc_prog_{Guid.NewGuid():N}.txt");
                string progressFlag = progress != null ? $"-progress \"{progressFile}\" " : "";

                double totalSec = 0;
                if (progress != null)
                {
                    string durStr = await vm.RunFFprobeAsync(
                        $"-v quiet -show_entries format=duration " +
                        $"-of default=noprint_wrappers=1:nokey=1 \"{inputPath}\"", ct);
                    double.TryParse(durStr.Trim(), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out totalSec);
                }

                string output = $"\"{Path.Combine(outputFolder, outputFileName)}.{outputExt}\"";
                string cmd;

                if (inputCategory == 2 && outEntry.Item1 == 1)
                {
                    // Audio → Video
                    string bgPart = atvUseImage && File.Exists(atvImagePath)
                        ? $"-loop 1 -i \"{atvImagePath}\" "
                        : $"-f lavfi -i color=c=0x{atvColor}:size=1280x720:rate=25 ";

                    bool   webm  = outputExt == "webm";
                    string vCdc  = webm ? "libvpx"    : "libx264";
                    string aCdc  = webm ? "libvorbis"  : "aac";
                    string tune  = webm ? ""           : "-tune stillimage ";
                    string pix   = webm ? ""           : "-pix_fmt yuv420p ";
                    cmd = $"{overwriteFlag}{hwFlag}{bgPart}{progressFlag}{threadFlag}-i \"{inputPath}\" " +
                          $"{trimStartFlag}{trimEndFlag}-c:v {vCdc} {tune}{pix}-c:a {aCdc} -b:a {bitrate} -shortest {output}";
                }
                else
                {
                    string extraFlags = "";
                    if (outEntry.Item1 == 1)      extraFlags = $"{threadFlag}-preset {preset} ";
                    else if (outEntry.Item1 == 2) extraFlags = $"{threadFlag}-b:a {bitrate} ";
                    cmd = $"{overwriteFlag}{hwFlag}{progressFlag}-i \"{inputPath}\" {trimStartFlag}{trimEndFlag}{extraFlags}{output}";
                }

                using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                Task pollTask = progress != null && totalSec > 0
                    ? PollFfmpegProgressAsync(progressFile, totalSec, progress, pollCts.Token)
                    : Task.CompletedTask;

                await vm.RunFFmpegAsync(cmd, ct);

                pollCts.Cancel();
                try { await pollTask; } catch (OperationCanceledException) { }
                try { if (File.Exists(progressFile)) File.Delete(progressFile); } catch { }
            }
            else
            {
                string outPath = Path.Combine(outputFolder, outputFileName) + "." + outputExt;

                await Task.Run(() =>
                {
                    using var image = new MagickImage(inputPath);
                    if (doResize)  image.Resize(resW, resH);
                    if (stripMeta) image.Strip();
                    image.Quality = quality;
                    image.Write(outPath);
                }, ct);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Conversion error: {ex.Message}");
            return false;
        }
    }

    private static async Task PollFfmpegProgressAsync(
        string progressFile, double totalSec, IProgress<double> progress, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(300, ct);
                if (!File.Exists(progressFile)) continue;
                try
                {
                    using var fs = new FileStream(progressFile, FileMode.Open,
                                                  FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    foreach (var line in sr.ReadToEnd().Split('\n'))
                    {
                        if (line.TrimStart().StartsWith("out_time_us=") &&
                            long.TryParse(line.Split('=')[1].Trim(), out long us))
                        {
                            double pct = Math.Min(99, us / (totalSec * 1_000_000.0) * 100.0);
                            progress.Report(pct);
                            break;
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<string?> ResolveOutputFolder()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        string? preset = vm.Settings?.GetOutputFolder();
        if (preset != null && Directory.Exists(preset)) return preset;

        var folders = await GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select Output Folder", AllowMultiple = false });

        if (folders.Count != 1) return null;
        return folders[0].Path.ToString().Remove(0, 8);
    }

    private void HandleAfterConversion(string outputFolder, string? outputFilePath)
    {
        var s = (DataContext as MainWindowViewModel)!.Settings;
        if (s == null) return;

        if (s.IsOpenFolderAfter && Directory.Exists(outputFolder))
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = outputFolder });
        else if (s.IsOpenFileAfter && outputFilePath != null && File.Exists(outputFilePath))
            Process.Start(new ProcessStartInfo { FileName = outputFilePath, UseShellExecute = true });
    }

    // ──────────────────────────────────────────────────────────────────────
    //  METADATA EXTRACTION AND DISPLAY
    // ──────────────────────────────────────────────────────────────────────

    private void ClearMediaInfoRows()
    {
        RowResolution.IsVisible  = false;
        RowColorSpace.IsVisible  = false;
        RowBitDepth.IsVisible    = false;
        RowDuration.IsVisible    = false;
        RowFPS.IsVisible         = false;
        RowBitrate.IsVisible     = false;
        RowSampleRate.IsVisible  = false;
        RowChannels.IsVisible    = false;
        RowVCodec.IsVisible      = false;
        RowACodec.IsVisible      = false;
        MediaInfoHeader.IsVisible = false;
    }

    private void ShowRow(Grid row, TextBlock label, string? value)
    {
        row.IsVisible = value != null;
        if (value != null) label.Text = value;
    }

    private async Task UpdateFileMetadataAsync()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        if (vm.SelectedFilePath == null) return;

        int fileType = MainWindowViewModel.FileTypes
            .FirstOrDefault(x => x.Item2.Equals(vm.SelectedFileType, StringComparison.OrdinalIgnoreCase))
            .Item1;

        if (fileType == 0)
        {
            (int w, int h, string cs, string bd)? info = null;
            try
            {
                string capPath = vm.SelectedFilePath;
                info = await Task.Run(() =>
                {
                    using var img = new MagickImage(capPath);
                    return ((int)img.Width, (int)img.Height,
                            img.ColorSpace.ToString(), $"{img.Depth}-bit");
                });
            }
            catch (Exception ex) { Console.WriteLine($"Image metadata error: {ex.Message}"); }

            if (info.HasValue)
            {
                MediaInfoHeader.IsVisible = true;
                ShowRow(RowResolution, MetaResolution, $"{info.Value.w} × {info.Value.h}");
                ShowRow(RowColorSpace, MetaColorSpace, info.Value.cs);
                ShowRow(RowBitDepth,   MetaBitDepth,   info.Value.bd);
                SetUIResolution(true, info.Value.w, info.Value.h);
            }
            else { SetUIResolution(false, 0, 0); }
        }
        else
        {
            SetUIResolution(false, 0, 0);

            string capPath = vm.SelectedFilePath;
            MediaMetadata? meta = await GetFfprobeMetadataAsync(capPath, vm);
            if (meta == null) return;

            MediaInfoHeader.IsVisible = true;
            ShowRow(RowDuration,   MetaDuration,  meta.Duration);
            ShowRow(RowBitrate,    MetaBitrate,   meta.Bitrate);
            ShowRow(RowFPS,        MetaFPS,       fileType == 1 ? meta.FPS : null);
            ShowRow(RowSampleRate, MetaSampleRate, meta.SampleRate);
            ShowRow(RowChannels,   MetaChannels,  meta.Channels);
            ShowRow(RowVCodec,     MetaVCodec,    meta.VideoCodec);
            ShowRow(RowACodec,     MetaACodec,    meta.AudioCodec);

            if (meta.VideoCodec != null && meta.Width.HasValue && meta.Height.HasValue)
            {
                ShowRow(RowResolution, MetaResolution, $"{meta.Width} × {meta.Height}");
                SetUIResolution(true, meta.Width.Value, meta.Height.Value);
            }
        }
    }

    private async Task<MediaMetadata?> GetFfprobeMetadataAsync(string filePath, MainWindowViewModel vm)
    {
        string json = await vm.RunFFprobeAsync(
            $"-v quiet -print_format json -show_streams -show_format \"{filePath}\"");

        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var meta = new MediaMetadata();

            if (root.TryGetProperty("format", out var fmt))
            {
                if (fmt.TryGetProperty("duration", out var dur) &&
                    double.TryParse(dur.GetString(), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out double sec))
                {
                    var ts = TimeSpan.FromSeconds(sec);
                    meta.Duration = ts.TotalHours >= 1
                        ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                        : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
                }
                if (fmt.TryGetProperty("bit_rate", out var br) &&
                    long.TryParse(br.GetString(), out long bps))
                {
                    meta.Bitrate = bps >= 1_000_000
                        ? $"{bps / 1_000_000.0:F1} Mbps"
                        : $"{bps / 1000} kbps";
                }
            }

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (!stream.TryGetProperty("codec_type", out var ct)) continue;
                    string type = ct.GetString() ?? "";

                    bool isAttachedPic = stream.TryGetProperty("disposition", out var disp) &&
                                         disp.TryGetProperty("attached_pic", out var ap) &&
                                         ap.GetInt32() == 1;

                    if (type == "video" && !isAttachedPic)
                    {
                        if (stream.TryGetProperty("codec_name", out var vc))
                            meta.VideoCodec = vc.GetString()?.ToUpper();

                        if (stream.TryGetProperty("width", out var wEl) &&
                            stream.TryGetProperty("height", out var hEl))
                        {
                            meta.Width  = wEl.GetInt32();
                            meta.Height = hEl.GetInt32();
                        }

                        if (stream.TryGetProperty("r_frame_rate", out var fpsEl))
                        {
                            string? fps = fpsEl.GetString();
                            if (fps != null && fps.Contains('/'))
                            {
                                var pts = fps.Split('/');
                                if (double.TryParse(pts[0], out double n) &&
                                    double.TryParse(pts[1], out double d) && d > 0)
                                    meta.FPS = $"{n / d:F2}";
                            }
                        }

                        if (meta.Bitrate == null &&
                            stream.TryGetProperty("bit_rate", out var sbr) &&
                            long.TryParse(sbr.GetString(), out long sBps))
                            meta.Bitrate = sBps >= 1_000_000
                                ? $"{sBps / 1_000_000.0:F1} Mbps"
                                : $"{sBps / 1000} kbps";
                    }
                    else if (type == "audio")
                    {
                        if (stream.TryGetProperty("codec_name", out var ac))
                            meta.AudioCodec = ac.GetString()?.ToUpper();

                        if (stream.TryGetProperty("sample_rate", out var sr) &&
                            int.TryParse(sr.GetString(), out int sampleHz))
                            meta.SampleRate = sampleHz >= 1000
                                ? $"{sampleHz / 1000.0:F1} kHz"
                                : $"{sampleHz} Hz";

                        if (stream.TryGetProperty("channels", out var ch))
                            meta.Channels = ch.GetInt32() switch
                            {
                                1 => "Mono",
                                2 => "Stereo",
                                6 => "5.1 Surround",
                                8 => "7.1 Surround",
                                int n => $"{n} ch"
                            };

                        if (meta.Bitrate == null &&
                            stream.TryGetProperty("bit_rate", out var abr) &&
                            long.TryParse(abr.GetString(), out long aBps))
                            meta.Bitrate = $"{aBps / 1000} kbps";
                    }
                }
            }

            return meta;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FFprobe parse error: {ex.Message}");
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  UI HELPERS
    // ──────────────────────────────────────────────────────────────────────

    private void UpdateSelectedFileSize()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        if (!File.Exists(vm.SelectedFilePath)) return;
        long bytes = new FileInfo(vm.SelectedFilePath).Length;
        MetaFileSize.Text = FmtBytes(bytes);
    }

    private async Task UpdateImageAsync()
    {
        var vm = (DataContext as MainWindowViewModel)!;
        if (vm.SelectedFilePath == null || vm.SelectedFilePath.Length <= 3)
        {
            SelectedImage.Source = null;
            VideoPreviewBadge.IsVisible = false;
            return;
        }

        string[] imgTypes = { "png","jpg","jpeg","gif","webp","bmp","tif","tiff","ico","avif","heic" };
        string[] vidTypes = { "mp4","mov","avi","mkv","webm","flv","wmv","ts","m4v" };
        string ext = vm.SelectedFileType.ToLower();

        VideoPreviewBadge.IsVisible  = false;
        PreviewPlaceholder.IsVisible = false;

        if (imgTypes.Contains(ext))
        {
            try { SelectedImage.Source = new Avalonia.Media.Imaging.Bitmap(vm.SelectedFilePath); }
            catch { SelectedImage.Source = null; }
        }
        else if (vidTypes.Contains(ext))
        {
            string tempPath     = Path.Combine(Path.GetTempPath(), $"fc_prev_{Guid.NewGuid():N}.jpg");
            string capturedPath = vm.SelectedFilePath;
            try
            {
                await vm.RunFFmpegAsync($"-y -i \"{capturedPath}\" -ss 00:00:01 -vframes 1 \"{tempPath}\"");

                if (File.Exists(tempPath))
                {
                    byte[] bytes = File.ReadAllBytes(tempPath);
                    using var ms = new System.IO.MemoryStream(bytes);
                    SelectedImage.Source        = new Avalonia.Media.Imaging.Bitmap(ms);
                    VideoPreviewBadge.IsVisible = true;
                }
                else { SelectedImage.Source = null; }
            }
            catch { SelectedImage.Source = null; }
            finally { try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { } }
        }
        else
        {
            string[] audioTypes = { "mp3","flac","ogg","aac","m4a","opus","aiff","wma","wav" };
            bool gotArt = false;

            if (audioTypes.Contains(ext))
            {
                string capturedPath = vm.SelectedFilePath;
                string tempPath     = Path.Combine(Path.GetTempPath(), $"fc_art_{Guid.NewGuid():N}.jpg");
                try
                {
                    await vm.RunFFmpegAsync($"-y -i \"{capturedPath}\" -an -vcodec copy \"{tempPath}\"");

                    if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
                    {
                        byte[] artBytes = File.ReadAllBytes(tempPath);
                        using var ms = new System.IO.MemoryStream(artBytes);
                        SelectedImage.Source = new Avalonia.Media.Imaging.Bitmap(ms);
                        gotArt = true;
                    }
                }
                catch { }
                finally { try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { } }
            }

            if (!gotArt)
            {
                SelectedImage.Source         = null;
                PreviewPlaceholder.IsVisible = true;
            }
        }
    }

    private void SetUIResolution(bool active, int w, int h)
    {
        _suppressResolutionUpdate = true;
        ResolutionWidthInput.IsEnabled  = active;
        ResolutionHeightInput.IsEnabled = active;
        ResolutionWidthInput.Text  = active ? w.ToString() : null;
        ResolutionHeightInput.Text = active ? h.ToString() : null;
        _suppressResolutionUpdate = false;

        OriginalResolutionHint.Text = active ? $"orig\n{w}×{h}" : "";

        if (active && w > 0 && h > 0)
            _aspectRatio = (double)w / h;

        _isResolutionValid = !active || AreResolutionInputsValid();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  MISC HANDLERS
    // ──────────────────────────────────────────────────────────────────────

    private void OpenEnlargedImage(object? sender, PointerPressedEventArgs e)
        => (DataContext as MainWindowViewModel)!.OpenSelectedImage();

    public void OnGithubButtonClicked(object? sender, RoutedEventArgs e)
        => (DataContext as MainWindowViewModel)!.OpenLinkInBrowser("https://github.com/wmfor");
}
