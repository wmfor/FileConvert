using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using ReactiveUI;

namespace FileConvert.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822

    public MainWindowViewModel? MainWindowViewModel;

    // ── Output — filename ─────────────────────────────────────────────────
    private bool   _isRandomNameSelected   = true;
    private bool   _isSameNameSelected;
    private bool   _isSpecificNameSelected;
    private string _specificName           = "";

    public bool   IsRandomNameSelected   { get => _isRandomNameSelected;   set => this.RaiseAndSetIfChanged(ref _isRandomNameSelected,   value); }
    public bool   IsSameNameSelected     { get => _isSameNameSelected;     set => this.RaiseAndSetIfChanged(ref _isSameNameSelected,     value); }
    public bool   IsSpecificNameSelected { get => _isSpecificNameSelected; set => this.RaiseAndSetIfChanged(ref _isSpecificNameSelected, value); }
    public string SpecificName           { get => _specificName;           set => this.RaiseAndSetIfChanged(ref _specificName,           value); }

    // ── Output — folder ───────────────────────────────────────────────────
    private bool   _isAskFolderEachTime   = true;
    private bool   _isUseLastFolder;
    private bool   _isUseCustomFolder;
    private string _customOutputFolder    = "";
    public  string LastUsedFolder         = "";

    public bool   IsAskFolderEachTime   { get => _isAskFolderEachTime;   set => this.RaiseAndSetIfChanged(ref _isAskFolderEachTime,   value); }
    public bool   IsUseLastFolder       { get => _isUseLastFolder;       set => this.RaiseAndSetIfChanged(ref _isUseLastFolder,       value); }
    public bool   IsUseCustomFolder     { get => _isUseCustomFolder;     set => this.RaiseAndSetIfChanged(ref _isUseCustomFolder,     value); }
    public string CustomOutputFolder    { get => _customOutputFolder;    set => this.RaiseAndSetIfChanged(ref _customOutputFolder,    value); }

    // ── Output — after conversion ─────────────────────────────────────────
    private bool _isDoNothingAfter    = true;
    private bool _isOpenFolderAfter;
    private bool _isOpenFileAfter;

    public bool IsDoNothingAfter    { get => _isDoNothingAfter;    set => this.RaiseAndSetIfChanged(ref _isDoNothingAfter,    value); }
    public bool IsOpenFolderAfter   { get => _isOpenFolderAfter;   set => this.RaiseAndSetIfChanged(ref _isOpenFolderAfter,   value); }
    public bool IsOpenFileAfter     { get => _isOpenFileAfter;     set => this.RaiseAndSetIfChanged(ref _isOpenFileAfter,     value); }

    // ── Encoding — video ──────────────────────────────────────────────────
    private int _videoPresetIndex = 4; // "fast"

    public int VideoPresetIndex
    {
        get => _videoPresetIndex;
        set => this.RaiseAndSetIfChanged(ref _videoPresetIndex, value);
    }

    public static string[] VideoPresets { get; } =
    {
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow"
    };

    public string VideoPreset => VideoPresets[Math.Clamp(_videoPresetIndex, 0, VideoPresets.Length - 1)];

    // ── Encoding — audio ──────────────────────────────────────────────────
    private int _audioBitrateIndex = 2; // "192k"

    public int AudioBitrateIndex
    {
        get => _audioBitrateIndex;
        set => this.RaiseAndSetIfChanged(ref _audioBitrateIndex, value);
    }

    public static string[] AudioBitrates { get; } = { "96k", "128k", "192k", "256k", "320k" };

    public string AudioBitrate => AudioBitrates[Math.Clamp(_audioBitrateIndex, 0, AudioBitrates.Length - 1)];

    // ── Encoding — performance ────────────────────────────────────────────
    private bool _useHardwareAcceleration;
    private int  _threadCountIndex = 0; // 0 = auto

    public bool UseHardwareAcceleration
    {
        get => _useHardwareAcceleration;
        set => this.RaiseAndSetIfChanged(ref _useHardwareAcceleration, value);
    }
    public int ThreadCountIndex
    {
        get => _threadCountIndex;
        set => this.RaiseAndSetIfChanged(ref _threadCountIndex, value);
    }

    public static int[] ThreadCounts { get; } = { 0, 2, 4, 8, 16 };
    public int ThreadCount => ThreadCounts[Math.Clamp(_threadCountIndex, 0, ThreadCounts.Length - 1)];

    // ── Batch parallel conversions ────────────────────────────────────────
    private int _maxParallelIndex = 0; // 0 = 1 (sequential)

    public int MaxParallelIndex
    {
        get => _maxParallelIndex;
        set => this.RaiseAndSetIfChanged(ref _maxParallelIndex, value);
    }

    public static int[] ParallelCounts { get; } = { 1, 2, 4 };
    public int MaxParallelConversions => ParallelCounts[Math.Clamp(_maxParallelIndex, 0, ParallelCounts.Length - 1)];

    // ── General ───────────────────────────────────────────────────────────
    private bool _overwriteExistingFiles;
    private bool _stripImageMetadata;

    public bool OverwriteExistingFiles { get => _overwriteExistingFiles; set => this.RaiseAndSetIfChanged(ref _overwriteExistingFiles, value); }
    public bool StripImageMetadata     { get => _stripImageMetadata;     set => this.RaiseAndSetIfChanged(ref _stripImageMetadata,     value); }

    // ── Constructor ───────────────────────────────────────────────────────
    public SettingsWindowViewModel() { }

    // ── Helpers called by code-behind ─────────────────────────────────────
    public void SelectFileNameOption(string? option)
    {
        IsRandomNameSelected   = option == "RandomCheckBox";
        IsSameNameSelected     = option == "SameCheckBox";
        IsSpecificNameSelected = option == "SpecificCheckBox";
    }

    public void SelectFolderOption(string? option)
    {
        IsAskFolderEachTime = option == "AskEachTime";
        IsUseLastFolder     = option == "UseLastFolder";
        IsUseCustomFolder   = option == "CustomFolder";
    }

    public void SelectAfterOption(string? option)
    {
        IsDoNothingAfter  = option == "DoNothing";
        IsOpenFolderAfter = option == "OpenFolder";
        IsOpenFileAfter   = option == "OpenFile";
    }

    public string? GetOutputFolder()
    {
        if (IsUseCustomFolder && !string.IsNullOrWhiteSpace(CustomOutputFolder))
            return CustomOutputFolder;
        if (IsUseLastFolder && !string.IsNullOrWhiteSpace(LastUsedFolder))
            return LastUsedFolder;
        return null;
    }

    public string GetPreviewOutputName(string currentFileName, string extension)
    {
        if (IsRandomNameSelected) return $"[random].{extension}";
        if (IsSameNameSelected)   return $"{(string.IsNullOrEmpty(currentFileName) ? "file" : currentFileName)}.{extension}";
        if (IsSpecificNameSelected && !string.IsNullOrEmpty(SpecificName)) return $"{SpecificName}.{extension}";
        return $"[random].{extension}";
    }

    // ── Persistence ───────────────────────────────────────────────────────
    private static string SettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileConvert", "settings.json");

    public void SaveToFile()
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);

            var dto = new
            {
                FileNameOption          = IsRandomNameSelected ? "RandomCheckBox"
                                        : IsSameNameSelected   ? "SameCheckBox" : "SpecificCheckBox",
                SpecificName,
                FolderOption            = IsAskFolderEachTime ? "AskEachTime"
                                        : IsUseLastFolder      ? "UseLastFolder" : "CustomFolder",
                CustomOutputFolder,
                AfterOption             = IsDoNothingAfter   ? "DoNothing"
                                        : IsOpenFolderAfter   ? "OpenFolder" : "OpenFile",
                VideoPresetIndex,
                AudioBitrateIndex,
                OverwriteExistingFiles,
                StripImageMetadata,
                UseHardwareAcceleration,
                ThreadCountIndex,
                MaxParallelIndex,
            };

            File.WriteAllText(SettingsFilePath,
                JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Console.WriteLine($"Settings save error: {ex.Message}"); }
    }

    public static SettingsWindowViewModel LoadFromFile()
    {
        var vm = new SettingsWindowViewModel();
        try
        {
            if (!File.Exists(SettingsFilePath)) return vm;

            using var doc  = JsonDocument.Parse(File.ReadAllText(SettingsFilePath));
            var        root = doc.RootElement;

            void TryStr(string key,  Action<string> set) { if (root.TryGetProperty(key, out var e)) set(e.GetString() ?? ""); }
            void TryBool(string key, Action<bool>   set) { if (root.TryGetProperty(key, out var e)) set(e.GetBoolean()); }
            void TryInt(string key,  Action<int>    set) { if (root.TryGetProperty(key, out var e)) set(e.GetInt32()); }

            TryStr ("FileNameOption",          v => vm.SelectFileNameOption(v));
            TryStr ("SpecificName",            v => vm.SpecificName = v);
            TryStr ("FolderOption",            v => vm.SelectFolderOption(v));
            TryStr ("CustomOutputFolder",      v => vm.CustomOutputFolder = v);
            TryStr ("AfterOption",             v => vm.SelectAfterOption(v));
            TryInt ("VideoPresetIndex",        v => vm.VideoPresetIndex = Math.Clamp(v, 0, VideoPresets.Length - 1));
            TryInt ("AudioBitrateIndex",       v => vm.AudioBitrateIndex = Math.Clamp(v, 0, AudioBitrates.Length - 1));
            TryBool("OverwriteExistingFiles",  v => vm.OverwriteExistingFiles = v);
            TryBool("StripImageMetadata",      v => vm.StripImageMetadata = v);
            TryBool("UseHardwareAcceleration", v => vm.UseHardwareAcceleration = v);
            TryInt ("ThreadCountIndex",        v => vm.ThreadCountIndex = Math.Clamp(v, 0, ThreadCounts.Length - 1));
            TryInt ("MaxParallelIndex",        v => vm.MaxParallelIndex = Math.Clamp(v, 0, ParallelCounts.Length - 1));
        }
        catch (Exception ex) { Console.WriteLine($"Settings load error: {ex.Message}"); }
        return vm;
    }

#pragma warning restore CA1822
}
