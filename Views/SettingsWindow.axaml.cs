using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using FileConvert.ViewModels;

namespace FileConvert.Views;

public partial class SettingsWindow : Window
{
    private bool _suppressEvents;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    // ── Custom title bar ──────────────────────────────────────────────────
    public void OnClickCloseButton(object? sender, RoutedEventArgs e)
    {
        (DataContext as SettingsWindowViewModel)?.SaveToFile();
        Hide();
    }

    // ── FILENAME option chips ─────────────────────────────────────────────
    private void OnFilenameOptClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        string tag = btn.Tag?.ToString() ?? "";

        SetChipActive(OptRandom,   tag == "RandomCheckBox");
        SetChipActive(OptSameName, tag == "SameCheckBox");
        SetChipActive(OptSpecific, tag == "SpecificCheckBox");

        SpecificNameBox.IsVisible = tag == "SpecificCheckBox";

        (DataContext as SettingsWindowViewModel)?.SelectFileNameOption(tag);
    }

    private void OnSpecificNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
            vm.SpecificName = SpecificNameBox.Text ?? "";
    }

    // ── FOLDER option chips ───────────────────────────────────────────────
    private void OnFolderOptClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        string tag = btn.Tag?.ToString() ?? "";

        SetChipActive(FolderAsk,    tag == "AskEachTime");
        SetChipActive(FolderLast,   tag == "UseLastFolder");
        SetChipActive(FolderCustom, tag == "CustomFolder");

        CustomFolderRow.IsVisible = tag == "CustomFolder";

        (DataContext as SettingsWindowViewModel)?.SelectFolderOption(tag);
    }

    private void OnCustomFolderChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
            vm.CustomOutputFolder = CustomFolderBox.Text ?? "";
    }

    private async void OnBrowseCustomFolderClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select Default Output Folder", AllowMultiple = false });

        if (folders.Count != 1) return;
        string path = folders[0].Path.ToString().Remove(0, 8);
        CustomFolderBox.Text = path;
        if (DataContext is SettingsWindowViewModel vm) vm.CustomOutputFolder = path;
    }

    // ── AFTER CONVERSION option chips ─────────────────────────────────────
    private void OnAfterOptClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        string tag = btn.Tag?.ToString() ?? "";

        SetChipActive(AfterNothing,    tag == "DoNothing");
        SetChipActive(AfterOpenFolder, tag == "OpenFolder");
        SetChipActive(AfterOpenFile,   tag == "OpenFile");

        (DataContext as SettingsWindowViewModel)?.SelectAfterOption(tag);
    }

    // ── VIDEO PRESET chips ────────────────────────────────────────────────
    private void OnVideoPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        int index = int.Parse(btn.Tag?.ToString() ?? "4");

        foreach (var child in VideoPresetPanel.Children.OfType<Button>())
            child.Classes.Set("presetActive", child == btn);

        if (DataContext is SettingsWindowViewModel vm)
            vm.VideoPresetIndex = index;
    }

    // ── AUDIO BITRATE chips ───────────────────────────────────────────────
    private void OnAudioBitrateClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        int index = int.Parse(btn.Tag?.ToString() ?? "2");

        // Find the audio bitrate panel (parent WrapPanel)
        var panel = btn.Parent as WrapPanel;
        if (panel != null)
            foreach (var child in panel.Children.OfType<Button>())
                child.Classes.Set("presetActive", child == btn);

        if (DataContext is SettingsWindowViewModel vm)
            vm.AudioBitrateIndex = index;
    }

    // ── GENERAL checkboxes ────────────────────────────────────────────────
    private void OnOverwriteChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
            vm.OverwriteExistingFiles = OverwriteCheck.IsChecked ?? false;
    }

    private void OnStripMetadataChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
            vm.StripImageMetadata = StripMetadataCheck.IsChecked ?? false;
    }

    private void OnHWAccelChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm)
            vm.UseHardwareAcceleration = HWAccelCheck.IsChecked ?? false;
    }

    private void OnThreadCountClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        int index = int.Parse(btn.Tag?.ToString() ?? "0");

        foreach (var child in ThreadCountPanel.Children.OfType<Button>())
            child.Classes.Set("presetActive", child == btn);

        if (DataContext is SettingsWindowViewModel vm)
            vm.ThreadCountIndex = index;
    }

    // ── PARALLEL CONVERSIONS chips ────────────────────────────────────────
    private void OnParallelCountClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _suppressEvents) return;
        int index = int.Parse(btn.Tag?.ToString() ?? "0");

        foreach (var child in ParallelCountPanel.Children.OfType<Button>())
            child.Classes.Set("presetActive", child == btn);

        if (DataContext is SettingsWindowViewModel vm)
            vm.MaxParallelIndex = index;
    }

    // ── Sync UI from ViewModel (called after loading settings from disk) ──
    public void SyncUIFromViewModel()
    {
        if (DataContext is not SettingsWindowViewModel vm) return;
        _suppressEvents = true;

        // Filename chips
        SetChipActive(OptRandom,   vm.IsRandomNameSelected);
        SetChipActive(OptSameName, vm.IsSameNameSelected);
        SetChipActive(OptSpecific, vm.IsSpecificNameSelected);
        SpecificNameBox.IsVisible = vm.IsSpecificNameSelected;
        SpecificNameBox.Text      = vm.SpecificName;

        // Folder chips
        SetChipActive(FolderAsk,    vm.IsAskFolderEachTime);
        SetChipActive(FolderLast,   vm.IsUseLastFolder);
        SetChipActive(FolderCustom, vm.IsUseCustomFolder);
        CustomFolderRow.IsVisible = vm.IsUseCustomFolder;
        CustomFolderBox.Text      = vm.CustomOutputFolder;

        // After chips
        SetChipActive(AfterNothing,    vm.IsDoNothingAfter);
        SetChipActive(AfterOpenFolder, vm.IsOpenFolderAfter);
        SetChipActive(AfterOpenFile,   vm.IsOpenFileAfter);

        // Video preset chips (Tag == index)
        foreach (var btn in VideoPresetPanel.Children.OfType<Button>())
            btn.Classes.Set("presetActive", int.Parse(btn.Tag?.ToString() ?? "-1") == vm.VideoPresetIndex);

        // Audio bitrate chips (Tag == index)
        foreach (var btn in AudioBitratePanel.Children.OfType<Button>())
            btn.Classes.Set("presetActive", int.Parse(btn.Tag?.ToString() ?? "-1") == vm.AudioBitrateIndex);

        // Thread count chips (Tag == index)
        foreach (var btn in ThreadCountPanel.Children.OfType<Button>())
            btn.Classes.Set("presetActive", int.Parse(btn.Tag?.ToString() ?? "-1") == vm.ThreadCountIndex);

        // Parallel count chips
        foreach (var btn in ParallelCountPanel.Children.OfType<Button>())
            btn.Classes.Set("presetActive", int.Parse(btn.Tag?.ToString() ?? "-1") == vm.MaxParallelIndex);

        // Checkboxes
        OverwriteCheck.IsChecked    = vm.OverwriteExistingFiles;
        StripMetadataCheck.IsChecked = vm.StripImageMetadata;
        HWAccelCheck.IsChecked      = vm.UseHardwareAcceleration;

        _suppressEvents = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SetChipActive(Button btn, bool active)
    {
        btn.Classes.Set("optActive", active);
    }
}
