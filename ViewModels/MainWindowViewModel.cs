using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FileConvert.Views;
using ReactiveUI;
namespace FileConvert.ViewModels;

enum ConversionType
{
    PNG,
    JPG,
    GIF,
    WEBP,
    ICO,
    MP3,
    MP4,
    FLAC,
    WAV,
    OGG,
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

    //This is used to get the string value of the combobox for output type options.
    public string SelectedConversionType
    {
        get => _SelectedConversionType;
        set
        {
            this.RaiseAndSetIfChanged(ref _SelectedConversionType, value);
            SelectedConversionTypeIndex = (int)Enum.Parse(typeof(ConversionType), _SelectedConversionType);
            Console.WriteLine("Selected Type Changed To " + _SelectedConversionType);
        }
    }
     
    //This is used for storing the specific index of the selected output type.
    public int SelectedConversionTypeIndex
    {
        get => _SelectedConversionTypeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _SelectedConversionTypeIndex, value);
            Console.WriteLine("Selected Conversion Index Changed To " + _SelectedConversionTypeIndex);
        }
    }
    private int _SelectedConversionTypeIndex;



    private string _SelectedConversionType;
    
    
    public ICommand OpenFileSelectCommand { get; }
    public ICommand OpenSettingsSelectCommand { get; }
    public ICommand GenerateFileSelectCommand { get; }



    
    
    //The instance of the settings window.
    public SettingsWindow? SettingsInstance;
    private SettingsWindowViewModel? _SettingsInstanceViewModel;

  

    
    

    
    //Window Constructor.
    public MainWindowViewModel()
    {
        _SelectedConversionType = "PNG";
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
        if (SettingsInstance != null && SettingsInstance.IsVisible)
        {
            Console.WriteLine("Window is already visible, hide it.");
            
            CloseSettingsWindow();
        }
        
        //If the settings window hasn't been instantiated yet, create it.
        else if (SettingsInstance == null || !SettingsInstance.IsVisible)
        {
            Console.WriteLine("Window hasn't been made yet, create it.");
            
            CloseSettingsWindow();

            _SettingsInstanceViewModel = new SettingsWindowViewModel();
            
            SettingsInstance = new SettingsWindow
            {
                DataContext = _SettingsInstanceViewModel,
           };

            _SettingsInstanceViewModel.MainWindowViewModel = this;
            
            SettingsInstance.Show();
        }

    }
    
    //Called by clicking on the 'Settings' button while the window is open.
    private void CloseSettingsWindow()
    {
        if (SettingsInstance == null)
        {
            Console.WriteLine("ERROR | Tried to close settings window while it doesn't exist!");
            return;
        }
        
        
        SettingsInstance.Close();
        SettingsInstance = null;
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
        
        // Open a folder select prompt to select where you'd like to file to be outputted.
        IReadOnlyList<IStorageFolder> folders = await GetTopLevel().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });
        
        //Make sure a folder was selected, & not more than 1.
        if (folders.Count != 1)
            return;
        
        //Get the file path in a string.
        _outputFilePath = folders[0].Path.ToString().Remove(0, 8);

        switch (SelectedConversionType)
        {
            case "PNG":
                break;
            case "JPG":
                break;
            case "GIF":
                break;
            case "WEBP":
                break;
            case "ICO":
                break;
            case "MP3":
                break;
            case "MP4":
                break;
            case "WAV":
                break;
            case "OGG":
                break;
        }
        

        string outputFileName = $"ConvertedFile{Guid.NewGuid()}";
        string runCommand = "cd " + _outputFilePath + " && ffmpeg -i " + _selectedFilePath + " " + outputFileName + $".{SelectedConversionType.ToLower()}";
        
        RunCMDCommand(runCommand, true);
        Console.WriteLine(runCommand);
        Console.WriteLine("CONVERTING TO TYPE -!--!- " + SelectedConversionType.ToLower());
    }

    
    
    public void OpenLinkInBrowser(string link)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ProcessStartInfo windowsProcess = new ProcessStartInfo("cmd", $"/c start {link}");
            windowsProcess.WindowStyle = ProcessWindowStyle.Hidden;
            
            Process.Start(windowsProcess);
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

  
    
    
    public static string RunCMDCommand(string arguments, bool readOutput)
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
