using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

//Make sure the ComboBox options match these exactly, uses this are reference to get index of option.
internal enum ConversionType
{
    PNG,
    JPG,
    WEBP,
    ICO,
    BMP,
    TIFF,
    GIF,
    WAV,
    OGG,
    FLAC,
    MP3,
    MP4,
    MOV,
    AVI
}
public class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static

    public (int, string)[] FileTypes  // || 0 = PHOTO |||| 1 = VIDEO ||||| 2 = AUDIO ||
    {
        get
        {
            return new[]
            {
                // PHOTO SECTION
                (0, "PNG"),
                (0, "JPG"),
                (0, "WEBP"),
                (0, "ICO"),
                (0, "BMP"),
                (0, "TIFF"),
                (0, "GIF"),

                // VIDEO SECTION
                (1, "MP4"),
                (1, "MOV"),
                (1, "AVI"),

                // AUDIO SECTION
                (2, "MP3"),
                (2, "FLAC"),
                (2, "WAV"),
                (2, "OGG"),
            };
        }
    }


    public string SelectedFileName = ""; //What the raw name of the selected file is e.g "FileName", or "MyFileWestonForbes", etc.
    public string SelectedFileType = ""; //What the selected file's type is e.g. png, jpg, etc.
    public string SelectedFileResolution = ""; //What the select (image/photo) file's resolution is.
    public string SelectedFileSize = ""; //What the selected file's size is, e.g 32.52mb

    private string _DataConversionType; //A string that holds the conversion type in plaintext (e.g. (PNG to GIF), (MP4 to MP3), etc etc.)
    private string? _SelectedFilePath = ""; //The full path to the selected file.
    private int _SelectedConversionTypeIndex; //The index of the dropdown option that is currently selected.
    private string _SelectedConversionType; //The literal type of the selected dropdown type (e.g. png, jpg, mp3, etc)
    private string _SelectedFileNameWithType; //The name of the file, combined with the type ( e.g. "MyFile.JPG" )

    public string SelectedFileNameWithType { get => _SelectedFileNameWithType; set => this.RaiseAndSetIfChanged(ref _SelectedFileNameWithType, value); } //The file's name with the type extension, e.g filename.ogg, or myfile.mp3.
    public string DataConversionType
    {
        get => _DataConversionType;
        set => this.RaiseAndSetIfChanged(ref _DataConversionType, value);
    }

    //The path you've selected for the file you wish to convert.
    public string? SelectedFilePath
    {
        get => _SelectedFilePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _SelectedFilePath, value);
            SelectedFileNameWithType = SelectedFilePath.Split('/').Last();
        }
    }

    //This is used to get the string value of the combobox for output type options.
    public string SelectedConversionType
    {
        get => _SelectedConversionType;
        set
        {
            this.RaiseAndSetIfChanged(ref _SelectedConversionType, value);
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

            SelectedConversionType = ((ConversionType)SelectedConversionTypeIndex).ToString();
        }
    }



    public ICommand OpenSettingsSelectCommand { get; }
    public ICommand OpenLastOutputFolderCommand { get; }




    //The instance of the settings window.
    private SettingsWindow? SettingsInstance;
    private SettingsWindowViewModel? _SettingsInstanceViewModel;







    //Window Constructor.
    public MainWindowViewModel()
    {
        SelectedConversionTypeIndex = 0; //Default conversion type. (PNG)

        OpenSettingsSelectCommand = ReactiveCommand.Create(OpenSettingsWindow);
        OpenLastOutputFolderCommand = ReactiveCommand.Create(OpenFolderWindow);


         /*
        _SettingsInstanceViewModel = new SettingsWindowViewModel();

        SettingsInstance = new SettingsWindow
        {
            DataContext = _SettingsInstanceViewModel
        };
        _SettingsInstanceViewModel.MainWindowViewModel = this;
       SettingsInstance.Hide();
        */
        // ^^ UNCOMMENT THIS SECTION FOR SETTINGS FUNCTIONALITY, COMMENT TO GET LIVE PREVIEW TO WORK.
    }





    //Called via the 'Settings' gear button.
    private void OpenSettingsWindow()
    {
        //If the settings window is already visible, hide it.
        if (SettingsInstance.IsVisible)
        {
            SettingsInstance.Hide();
        }
        //If the settings window hasn't been instantiated yet, create it.
        else if (!SettingsInstance.IsVisible)
        {
            SettingsInstance.Show();
        }
    }



    private void OpenFolderWindow()
    {
        string realDirectory = "";

        string[] splitStrings = _SelectedFilePath!.Split('/');

        if (_SelectedFilePath.Length > 3)
        {
            for (int i = 0; i < splitStrings.Length - 1; i++)
            {
                realDirectory += splitStrings[i];

                if (i != splitStrings.Length - 2)
                    realDirectory += "\\";
            }
        }
        else
        {
            Console.Write("Directory does not exist!");
            return;
        }


        if (Directory.Exists(realDirectory))
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                Arguments = realDirectory,
                FileName = "explorer.exe"
            };

            Process.Start(startInfo);
            Console.WriteLine(realDirectory);
        }
        else
        {

            Console.WriteLine(realDirectory);
            Console.Write("Directory does not exist!");
        }
    }


    public bool GetIsUsingFFMPEG()
    {
        //This just checks if your current selected conversion type is an AUDIO/VIDEO file, and if so, use FFMPEG, but if not, use ImageMagick.
        if (FileTypes.Any(x => (x.Item1 == 1 || x.Item1 == 2) && x.Item2.ToUpper() == SelectedConversionType.ToUpper()))
            return true;
        
        return false;
    }

    public string GetFileOutputName(string outputFilePath)
    {
        bool doesContainDuplicateSpecificName = false;
        bool doesContainDuplicateSameName = false;

        if (_SettingsInstanceViewModel == null)
            Console.WriteLine("View Model NULL!!!!");

        //Get all files in the chosen output directory.
        foreach (string file in Directory.GetFiles(outputFilePath))
        {
            string fixedFile = file.Remove(0, outputFilePath.Length + 1); //Removes the directory prefix.

            Console.WriteLine(fixedFile);

            if (_SettingsInstanceViewModel!.SpecificName != null && fixedFile.Contains(_SettingsInstanceViewModel!.SpecificName)) //There's already a file with the same specific name in that directory.
                doesContainDuplicateSpecificName = true;
            else if (fixedFile.Contains("originalFileName")) //There's already a file with the same original name in that directory.
                doesContainDuplicateSameName = true;
        }


        //Set the file to a random name by default.
        string? outputFileName = default;


        if (_SettingsInstanceViewModel!.IsRandomNameSelected) // RANDOM NAME
        {
            outputFileName = $"ConvertedFile{Guid.NewGuid()}";
        }
        else if (_SettingsInstanceViewModel.IsSameNameSelected) // SAME NAME
        {
            outputFileName = doesContainDuplicateSameName ? $"{SelectedFileName}{Guid.NewGuid()}" : SelectedFileName;
        }
        else if (_SettingsInstanceViewModel.IsSpecificNameSelected) //SPECIFIC NAME
        {
            //Make sure there's text entered in the TextBox.
            if (_SettingsInstanceViewModel!.SpecificName != default && _SettingsInstanceViewModel!.SpecificName != "")
            {
                outputFileName = doesContainDuplicateSpecificName ? $"{_SettingsInstanceViewModel.SpecificName}{Guid.NewGuid()}" : _SettingsInstanceViewModel.SpecificName;
            }
            else //If there's no specific name entered.
            {
                outputFileName = $"ConvertedFile{Guid.NewGuid()}";
            }
        }

        return outputFileName!;
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




    public string RunCMDCommand(string arguments, bool readOutput)
    {
        var output = string.Empty;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                Verb = "runas",
                FileName = "cmd.exe",
                Arguments = "/C " + arguments,
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
        } catch (Exception)
        {
            return output;
        }
    }

#pragma warning restore CA1822 // Mark members as static
}
