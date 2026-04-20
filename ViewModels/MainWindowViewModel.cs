using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using FileConvert.Models;
using FileConvert.Views;
using ReactiveUI;

namespace FileConvert.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822

    // ── File-type registry ────────────────────────────────────────────────
    // Category: 0 = Image, 1 = Video, 2 = Audio
    public static (int, string)[] FileTypes
    {
        get => new[]
        {
            // IMAGE
            (0, "PNG"),  (0, "JPG"),  (0, "JPEG"), (0, "WEBP"), (0, "GIF"),
            (0, "BMP"),  (0, "TIFF"), (0, "TIF"),  (0, "ICO"),  (0, "AVIF"),
            (0, "HEIC"),

            // VIDEO
            (1, "MP4"),  (1, "MOV"),  (1, "AVI"),  (1, "MKV"),  (1, "WEBM"),
            (1, "FLV"),  (1, "WMV"),  (1, "TS"),   (1, "M4V"),  (1, "GIF"),

            // AUDIO
            (2, "MP3"),  (2, "WAV"),  (2, "FLAC"), (2, "OGG"),  (2, "AAC"),
            (2, "M4A"),  (2, "OPUS"), (2, "AIFF"), (2, "WMA"),
        };
    }

    // ── Selected file state ───────────────────────────────────────────────
    public string SelectedFileName = "";
    public string SelectedFileType = "";

    private string? _selectedFilePath = "";
    private string  _selectedConversionType = "PNG";
    private string  _selectedFileNameWithType = "";

    public string SelectedFileNameWithType
    {
        get => _selectedFileNameWithType;
        set => this.RaiseAndSetIfChanged(ref _selectedFileNameWithType, value);
    }

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFilePath, value);
            SelectedFileNameWithType = value != null ? Path.GetFileName(value) : "";
        }
    }

    public string SelectedConversionType
    {
        get => _selectedConversionType;
        set => this.RaiseAndSetIfChanged(ref _selectedConversionType, value);
    }

    // ── Batch queue ───────────────────────────────────────────────────────
    public ObservableCollection<BatchFileItem> BatchQueue { get; } = new();

    public void AddFilesToBatch(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            string ext  = Path.GetExtension(path).TrimStart('.').ToLower();
            string name = Path.GetFileName(path);
            BatchQueue.Add(new BatchFileItem { FilePath = path, FileName = name, FileType = ext });
        }
    }

    public void ClearBatch() => BatchQueue.Clear();

    // ── Conversion history ────────────────────────────────────────────────
    public ObservableCollection<ConversionHistoryEntry> ConversionHistory { get; } = new();

    public void AddToHistory(ConversionHistoryEntry entry)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ConversionHistory.Insert(0, entry);
            while (ConversionHistory.Count > 50)
                ConversionHistory.RemoveAt(ConversionHistory.Count - 1);
        });
    }

    // ── Commands ──────────────────────────────────────────────────────────
    public ICommand OpenSettingsSelectCommand   { get; }
    public ICommand OpenLastOutputFolderCommand { get; }

    // ── Sub-window instances ──────────────────────────────────────────────
    private SettingsWindow?          _settingsInstance;
    private SettingsWindowViewModel? _settingsViewModel;

    private EnlargedImageWindow?     _enlargedInstance;
    private EnlargedImageViewModel?  _enlargedViewModel;

    public SettingsWindowViewModel? Settings => _settingsViewModel;

    // ── Constructor ───────────────────────────────────────────────────────
    public MainWindowViewModel()
    {
        SelectedConversionType        = "PNG";
        OpenSettingsSelectCommand     = ReactiveCommand.Create(OpenSettingsWindow);
        OpenLastOutputFolderCommand   = ReactiveCommand.Create(OpenFolderWindow);
        CreateSettingsInstance();
    }

    // ── Settings window ───────────────────────────────────────────────────
    private void CreateSettingsInstance()
    {
        if (_settingsViewModel != null) return;
        _settingsViewModel = SettingsWindowViewModel.LoadFromFile();
        _settingsInstance  = new SettingsWindow { DataContext = _settingsViewModel };
        _settingsViewModel.MainWindowViewModel = this;
        _settingsInstance.SyncUIFromViewModel();
        _settingsInstance.Hide();
    }

    private void OpenSettingsWindow()
    {
        if (_settingsInstance == null) CreateSettingsInstance();
        if (_settingsInstance!.IsVisible) _settingsInstance.Hide();
        else                              _settingsInstance.Show();
    }

    // ── Enlarged image window ─────────────────────────────────────────────
    private void CreateEnlargedImageInstance()
    {
        if (_enlargedInstance != null) return;
        _enlargedViewModel = new EnlargedImageViewModel();
        _enlargedInstance  = new EnlargedImageWindow { DataContext = _enlargedViewModel };
        _enlargedViewModel.MainWindowViewModel = this;
        _enlargedInstance.Hide();
    }

    public void OpenSelectedImage()
    {
        if (_enlargedInstance == null) CreateEnlargedImageInstance();
        if (!File.Exists(SelectedFilePath)) return;

        if (_enlargedInstance!.IsVisible) _enlargedInstance.Hide();
        else                              _enlargedInstance.Show();

        _enlargedInstance.EnlargedImageWindowImage.Source = new Bitmap(SelectedFilePath!);
        _enlargedInstance.FileNameText.Content = SelectedFileName;
    }

    // ── Open output folder ────────────────────────────────────────────────
    private void OpenFolderWindow()
    {
        if (string.IsNullOrEmpty(_selectedFilePath) || _selectedFilePath.Length <= 3) return;
        string dir = Path.GetDirectoryName(_selectedFilePath) ?? "";
        if (Directory.Exists(dir))
            Process.Start(new ProcessStartInfo
            {
                FileName  = "explorer.exe",
                Arguments = $"\"{dir}\""
            });
    }

    // ── Conversion helpers ────────────────────────────────────────────────

    public bool GetIsUsingFfmpeg()
    {
        bool outputIsAV = FileTypes.Any(x =>
            (x.Item1 == 1 || x.Item1 == 2) &&
            x.Item2.Equals(SelectedConversionType, StringComparison.OrdinalIgnoreCase));

        if (!outputIsAV) return false;

        if (SelectedConversionType.Equals("GIF", StringComparison.OrdinalIgnoreCase))
        {
            bool inputIsImage = FileTypes.Any(x =>
                x.Item1 == 0 &&
                x.Item2.Equals(SelectedFileType, StringComparison.OrdinalIgnoreCase));
            if (inputIsImage) return false;
        }

        return true;
    }

    public string GetFileOutputName(string outputFilePath)
    {
        if (_settingsViewModel == null) return $"ConvertedFile{Guid.NewGuid()}";

        bool dupSpecific = false, dupSame = false;
        foreach (string file in Directory.GetFiles(outputFilePath))
        {
            string name = Path.GetFileName(file);
            if (_settingsViewModel.SpecificName != null && name.Contains(_settingsViewModel.SpecificName))
                dupSpecific = true;
            if (name.Contains(SelectedFileName))
                dupSame = true;
        }

        if (_settingsViewModel.IsRandomNameSelected)
            return $"ConvertedFile{Guid.NewGuid()}";

        if (_settingsViewModel.IsSameNameSelected)
            return dupSame
                ? $"{SelectedFileName}_{Guid.NewGuid():N}"
                : SelectedFileName;

        if (_settingsViewModel.IsSpecificNameSelected &&
            !string.IsNullOrEmpty(_settingsViewModel.SpecificName))
            return dupSpecific
                ? $"{_settingsViewModel.SpecificName}_{Guid.NewGuid():N}"
                : _settingsViewModel.SpecificName;

        return $"ConvertedFile{Guid.NewGuid()}";
    }

    // ── Direct process execution (no cmd.exe wrapper) ────────────────────

    /// <summary>Runs FFprobe asynchronously and returns stdout.</summary>
    public async Task<string> RunFFprobeAsync(string args, CancellationToken ct = default)
    {
        try
        {
            var si = new ProcessStartInfo
            {
                FileName               = "ffprobe",
                Arguments              = args,
                WindowStyle            = ProcessWindowStyle.Hidden,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = false,
            };
            using var proc = Process.Start(si)!;
            using var reg  = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            });
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);
            return output;
        }
        catch { return ""; }
    }

    /// <summary>Runs FFmpeg asynchronously (no output capture).</summary>
    public async Task RunFFmpegAsync(string args, CancellationToken ct = default)
    {
        try
        {
            var si = new ProcessStartInfo
            {
                FileName        = "ffmpeg",
                Arguments       = args,
                WindowStyle     = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            using var proc = Process.Start(si)!;
            using var reg  = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            });
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"FFmpeg error: {ex.Message}"); }
    }

    /// <summary>Runs FFprobe synchronously (for use inside Task.Run). Returns stdout.</summary>
    public string RunFFprobeSync(string args, CancellationToken ct = default)
    {
        try
        {
            var si = new ProcessStartInfo
            {
                FileName               = "ffprobe",
                Arguments              = args,
                WindowStyle            = ProcessWindowStyle.Hidden,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = false,
            };
            using var proc = Process.Start(si)!;
            using var reg  = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            });
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output;
        }
        catch { return ""; }
    }

    /// <summary>Runs FFmpeg synchronously (for use inside Task.Run).</summary>
    public void RunFFmpegSync(string args, CancellationToken ct = default)
    {
        try
        {
            var si = new ProcessStartInfo
            {
                FileName        = "ffmpeg",
                Arguments       = args,
                WindowStyle     = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            using var proc = Process.Start(si)!;
            using var reg  = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            });
            proc.WaitForExit();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"FFmpeg error: {ex.Message}"); }
    }

    // ── Utilities ─────────────────────────────────────────────────────────
    public void OpenLinkInBrowser(string link)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("cmd", $"/c start {link}") { WindowStyle = ProcessWindowStyle.Hidden });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", link);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", link);
    }

#pragma warning restore CA1822
}
