using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FileConvert.Views;
using ReactiveUI;
namespace FileConvert.ViewModels;


public enum FileConvertSelect
{
    PNG,
    JPG,
    GIF,
    WEBP,
    MP3,
    MP4,
    OGG,
    WEBM,
}

public class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static



    private string? _selectedFilePath = "C:/";
    private string _outputFilePath = "C:/";
    
    //The path you've selected for the file you wish to convert.
    public string? SelectedFilePath
    {
        get => _selectedFilePath; 
        set => this.RaiseAndSetIfChanged(ref _selectedFilePath, value);
    }
    
    //Where the newly converted files will be output.
    public string OutputFilePath  {
        get => _outputFilePath; 
        set => this.RaiseAndSetIfChanged(ref _outputFilePath, value);
    }
    
    //The type of conversion desired.
    public FileConvertSelect SelectedConversionType;

    
    
    public ICommand OpenFileSelectCommand { get; }
    public ICommand OpenSettingsSelectCommand { get; }
    public ICommand GenerateFileSelectCommand { get; }

    //The instance of the settings window.
    public SettingsWindow? _SettingsInstance;
    private SettingsWindowViewModel? _SettingsInstanceViewModel;
    
    
    
    
    //Window Constructor.
    public MainWindowViewModel()
    {
        
        OpenFileSelectCommand = ReactiveCommand.Create(OpenFileSelectWindow);
        OpenSettingsSelectCommand = ReactiveCommand.Create(OpenSettingsWindow);
        GenerateFileSelectCommand = ReactiveCommand.Create(GenerateFile);
    }

    private WindowBase GetTopLevel()
    {
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            return desktopLifetime.MainWindow;
        
        return null;
    }
    
    //Called via the dropdown menu for conversion type selection.
    public void SelectConversionType(int index) =>  SelectedConversionType = (FileConvertSelect)index;
    
    
    private async void OpenFileSelectWindow()
    {
        //Will be called once the select file button is pressed.
        
        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFile> files = await GetTopLevel().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Convert",
            AllowMultiple = false
        });


        if (files.Count != 1)
            return;
        
        SelectedFilePath = files[0].Path.ToString().Remove(0,8);
        Console.WriteLine(SelectedFilePath);
        // Now feed this into ffplay.
    }

    //Called via the 'Settings' gear button.
    private void OpenSettingsWindow()
    {
        
        //If the settings window is already visible, hide it.
        if (_SettingsInstance != null && _SettingsInstance.IsVisible)
        {
            CloseSettingsWindow();
        }
        
        //If the settings window hasn't been instantiated yet, create it.
        if (_SettingsInstance == null || !_SettingsInstance.IsVisible)
        {
            CloseSettingsWindow();

            _SettingsInstanceViewModel = new SettingsWindowViewModel();
            
            _SettingsInstance = new SettingsWindow
            {
                DataContext = _SettingsInstanceViewModel,
            };

            _SettingsInstanceViewModel.MainWindowViewModel = this;
            
            _SettingsInstance.Show();
        }

    }
    
    //Called by clicking on the 'Settings' button while the window is open.
    private void CloseSettingsWindow()
    {
        if (_SettingsInstance == null)
        {
            Console.WriteLine("ERROR | Tried to close settings window while it doesn't exist!");
            return;
        }
        
        
        
        _SettingsInstance.Close();
        _SettingsInstance = null;
        _SettingsInstanceViewModel = null;
    }
    
    
    //Called via the 'Generate' button.
    private async void GenerateFile()
    {
        if (_selectedFilePath == null || _selectedFilePath.Length <= 3)
        {
            Console.WriteLine("ERROR | Must select file!");
            return;
        }
        
        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFolder> folders = await GetTopLevel().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });


        if (folders.Count != 1)
            return;
        
        _outputFilePath = folders[0].Path.ToString().Remove(0, 8);

        string outputFileName = $"ConvertedFile{Guid.NewGuid()}";
        string runCommand = "cd " + _outputFilePath + " && ffmpeg -i " + _selectedFilePath + " " + outputFileName + ".ogg";
        
        RunCommand(runCommand, true);
        Console.WriteLine(runCommand);
    }


    public void OpenGitHubLink() => OpenLinkInBrowser("https://github.com/turacept");

    public void OpenTwitterLink() =>  OpenLinkInBrowser("https://twitter.com/WestonFor");


    private void OpenLinkInBrowser(string link)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {link}"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", link);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", link);
        }
    }
    
    public static string RunCommand(string arguments, bool readOutput)
    {
        var output = string.Empty;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                Verb = "runas",
                FileName = "cmd.exe",
                Arguments = "/C "+arguments,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false
            };

            var proc = Process.Start(startInfo);

            if (readOutput)
            {
                output = proc.StandardOutput.ReadToEnd(); 
            }

            proc.WaitForExit(60000);

            return output;
        }
        catch (Exception)
        {
            return output;
        }
    }
    
#pragma warning restore CA1822 // Mark members as static
}
