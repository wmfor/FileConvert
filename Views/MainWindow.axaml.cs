using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FileConvert.ViewModels;
using ImageMagick;


namespace FileConvert.Views;

public partial class MainWindow : Window
{

    private bool _IsConversionValid; //Is the current conversion you're trying to do allowed by the client?
    private bool _IsResolutionValid; //Is the resolution numbers? and is it over 0?
    private bool _IsFilePresent; //Is there actually a file selected or not?



    public MainWindow()
    {
        InitializeComponent();

        SetUIResolution(false, 0, 0);
    }

    private async void UpdateImage(string imageUrl)
    {
        if (imageUrl == "")
        {
            try
            {
                using WebClient webClient = new WebClient();
                byte[] imageData = await webClient.DownloadDataTaskAsync("https://st4.depositphotos.com/14953852/22772/v/450/depositphotos_227725020-stock-illustration-image-available-icon-flat-vector.jpg");
                Bitmap bitmap = new Bitmap(new MemoryStream(imageData));
                SelectedImage.Source = bitmap;
            } catch (Exception ex)
            {
                Console.WriteLine("Error loading image: " + ex.Message);
            }
        }

        if (imageUrl.Length <= 3)
            return;

        Bitmap defaultBitmap = new Bitmap(imageUrl);
        SelectedImage.Source = defaultBitmap;
    }

    private void OpenEnlargedImage(object? sender, PointerPressedEventArgs e) => (DataContext as MainWindowViewModel)!.OpenSelectedImage();

    private void OnComboBoxChangeSelected(object? sender, SelectionChangedEventArgs e)
    {
        string? text = (e.AddedItems[0] as ComboBoxItem)!.Content as string;
        
        if (text != default)
            (DataContext as MainWindowViewModel)!.SelectedConversionType = text;

        UpdateFileTypeLabelContent();
        UpdateTypeConversionAccessibility();
    }

    private void UpdateTypeConversionAccessibility()
    { //Makes sure you're doing a conversion within the same class of file. (e.g. Photo -> Photo, Video -> Video.)

        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        //The file type of the original image file.
        int originalFileType = -1; //0=Photo, 1=Video, 2=Audio
        //The file type you're trying to convert to.
        int desiredFileType = -1; //0=Photo, 1=Video, 2=Audio

        (int, string) fromConversionType = MainWindowViewModel.FileTypes.FirstOrDefault(x => x.Item2.Equals(viewModel.SelectedFileType, StringComparison.OrdinalIgnoreCase));
        (int, string) toConversionType = MainWindowViewModel.FileTypes.FirstOrDefault(x => x.Item2.Equals(viewModel.SelectedConversionType, StringComparison.OrdinalIgnoreCase));

        if (fromConversionType != default)
            originalFileType = fromConversionType.Item1;

        if (toConversionType != default)
            desiredFileType = toConversionType.Item1;

        //OriginalFileType stays -1, it indicates there's no file present.
        _IsFilePresent = originalFileType != -1;

        //Converting from VIDEO to AUDIO
        if (originalFileType == 1 && desiredFileType == 2)
            _IsConversionValid = true;
        //Converting from SAME TO SAME
        else if (originalFileType == desiredFileType)
            _IsConversionValid = true;
        //If you're trying to convert types that don't allow it.
        else
            _IsConversionValid = false;

        IsGenerationViable();
    }

    private void IsGenerationViable()
    {
        if (_IsFilePresent == false)
            ErrorText.Content = "File Missing!";
        else if (_IsConversionValid == false)
            ErrorText.Content = "Conversion Type Not Available!";
        else if (_IsResolutionValid == false)
            ErrorText.Content = "Resolution Not Valid!";
        else
            ErrorText.Content = "";

        if (_IsResolutionValid && _IsConversionValid && _IsFilePresent)
            GenerateButton.IsEnabled = true;
        else
            GenerateButton.IsEnabled = false;
    }


    private void OnResolutionInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        _IsResolutionValid = AreResolutionInputsValid();

        IsGenerationViable();
    }

    private void OnSelectFileButtonClicked(object? sender, RoutedEventArgs e) => OpenFileSelectWindow();

    private void OnGenerateButtonClicked(object? sender, RoutedEventArgs e) => GenerateFile();


    public void OnGithubButtonClicked(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)!.OpenLinkInBrowser("https://github.com/turacept");
    }
    
    private void UpdateImage()
    {
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        string[] allowedFileTypes = { "png", "jpg", "gif", "webp", "bmp", "tif", "ico", "tiff" };

        if (viewModel.SelectedFilePath != null && viewModel.SelectedFilePath.Length > 3 && allowedFileTypes.Contains(viewModel.SelectedFileType))
            UpdateImage(viewModel.SelectedFilePath);
        else //Add separate logic for previewing video files here.
            UpdateImage(""); //Giving an empty string will show the 'Missing Image' img.
    }


    private void UpdateFileTypeLabelContent()
    {
        ConversionTypeLabel.Content = (DataContext as MainWindowViewModel)!.SelectedFileType;
    }

    //Will be called once the select file button is pressed.
    private async void OpenFileSelectWindow()
    {
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFile> files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Convert",
            AllowMultiple = false
        });

        if (files.Count != 1) return;

        viewModel.SelectedFilePath = files[0].Path.ToString().Remove(0, 8);
        viewModel.SelectedFileName = viewModel.SelectedFilePath.Split('/').Last().Split('.').First();
        viewModel.SelectedFileType = viewModel.SelectedFilePath.Split('/').Last().Split('.').Last();

        UpdateImage();
        UpdateFileTypeLabelContent();
        //Updates whether the conversion you're trying to do is possible, also calls UpdateGenerateViability at end of method.
        UpdateTypeConversionAccessibility();
        //Updates the UI for the newly selected file's resolution (if available)
        UpdateSelectedFileResolution();
        //Updates the UI for the newly selected file's file size (always available).
        UpdateSelectedFileSize();
        //Updates the dropdown UI for which conversions are possible.
        UpdateAccessibleDropdownOptions();
    }

    private void UpdateSelectedFileSize()
    {
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        //FILE SIZE CALCULATION 
        if (!File.Exists(viewModel.SelectedFilePath)) return;

        // Get the file's size in bytes.
        long fileSizeInBytes = new FileInfo(viewModel.SelectedFilePath).Length;

        //Convert to kb, then to mb.
        double fileSizeInMegabytes = (fileSizeInBytes / 1024.0) / 1024.0; // Convert bytes to megabytes

        //Round it down to 2 decimal places.
        double finalSizeValue = Math.Round(fileSizeInMegabytes, 2);

        //Set the text on the UI.
        FileSizeLabel.Content = $"{finalSizeValue} MB";
    }

    //Returns an int representing the type of file type it is, 0-2 inclusive. 
    private int GetCurrentConversionType()
    {
        // 0 = PHOTO TYPE,     (png, jpg, etc)    
        // 1 = VIDEO TYPE,     (mp4, mov, etc)
        // 2 = AUDIO TYPE.     (flac, mp3, etc)
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;
        return MainWindowViewModel.FileTypes.FirstOrDefault(x => x.Item2.Equals(viewModel.SelectedFileType, StringComparison.OrdinalIgnoreCase)).Item1;
    }

    private void UpdateSelectedFileResolution()
    {
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        bool isPhotoOrVideo = GetCurrentConversionType() is 0 or 1;

        //RESOLUTION CALCULATION
        if (isPhotoOrVideo)
        {
            string imagePath = viewModel.SelectedFilePath!;

            try
            {
                using MagickImage img = new MagickImage(imagePath);
                
                SetUIResolution(true, img.Width, img.Height);
            } catch (MagickException ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        else //if it's an audio file.
        {
            SetUIResolution(false, 0, 0);
        }
    }

    private void SetUIResolution(bool shouldBeActive, int width, int height)
    {
        ResolutionLabel.Content = shouldBeActive ? $"{width}x{height}" : "Unavailable";

        ResolutionWidthInput.IsEnabled = shouldBeActive;
        ResolutionHeightInput.IsEnabled = shouldBeActive;

        ResolutionWidthInput.Text = shouldBeActive ? width.ToString() : default;
        ResolutionHeightInput.Text = shouldBeActive ? height.ToString() : default;
    }


    //Called via the 'Generate' button.
    private async void GenerateFile()
    {
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        if (viewModel.SelectedFilePath == null || viewModel.SelectedFilePath.Length <= 3) return;

        // Open a folder select prompt to select where you'd like to file to be outputted.
        IReadOnlyList<IStorageFolder> folders = await GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });

        //Make sure a folder was selected, & not more than 1.
        if (folders.Count != 1) return;

        //Get the file path in a string.
        string outputFilePath = folders[0].Path.ToString().Remove(0, 8);
        //Determine what conversion library to use. (FFMPEG for Audio/Video, ImageMagick for Photo)
        bool useFfmpeg = viewModel.GetIsUsingFfmpeg();
        //Determine which file name format will be used. (e.g. Specific, Same, Random)
        string outputFileName = viewModel.GetFileOutputName(outputFilePath);

        switch (useFfmpeg)
        {
            case true:
            {
                string runCommand = "cd " + outputFilePath + " && ffmpeg -i " + viewModel.SelectedFilePath + " " + outputFileName + $".{viewModel.SelectedConversionType.ToLower()}";
                viewModel.RunCmdCommand(runCommand, true);
                break;
            }
            case false:
            {
                using MagickImage image = new MagickImage(viewModel.SelectedFilePath);

                if (AreResolutionInputsValid())
                    image.Resize(int.Parse(ResolutionWidthInput.Text!), int.Parse(ResolutionHeightInput.Text!));

                //Reversing it.
                image.Quality = 100 - (int)CompressionSlider.Value;

                await image.WriteAsync(outputFilePath + "/" + outputFileName + "." + viewModel.SelectedConversionType.ToLower());
                break;
            }
        }

        OutputButton.IsEnabled = true;
    }


    //Determines which conversion types are possible, and turns on/off the required selection areas.
    private void UpdateAccessibleDropdownOptions()
    {
        ComboBoxItem[] photoSelects =
        {
            PNGSelect,
            JPGSelect,
            WEBPSelect,
            ICOSelect,
            BMPSelect,
            TIFFSelect,
            GIFSelect
        };

        ComboBoxItem[] audioSelects =
        {
            WAVSelect,
            OGGSelect,
            FLACSelect,
            MP3Select
        };

        ComboBoxItem[] videoSelects =
        {
            MP4Select,
            MOVSelect,
            AVISelect
        };

        foreach (ComboBoxItem item in photoSelects)
            item.IsEnabled = false;

        foreach (ComboBoxItem item in audioSelects)
            item.IsEnabled = false;

        foreach (ComboBoxItem item in videoSelects)
            item.IsEnabled = false;


        if (GetCurrentConversionType() == 0)
        {
            foreach (ComboBoxItem item in photoSelects)
                item.IsEnabled = true;
        }
        else if (GetCurrentConversionType() == 1)
        {
            foreach (ComboBoxItem item in videoSelects)
                item.IsEnabled = true;
            // If you're converting a video, the audio select options are also available.
            foreach (ComboBoxItem item in audioSelects)
                item.IsEnabled = true;
        }
        else if (GetCurrentConversionType() == 2)
        {
            foreach (ComboBoxItem item in audioSelects)
                item.IsEnabled = true;
        }
    }


    private bool AreResolutionInputsValid()
    {
        string width = ResolutionWidthInput.Text!;
        string height = ResolutionHeightInput.Text!;

        if (width == "")
            return false;
        if (height == "")
            return false;


        if (Regex.IsMatch(width, @"^\d+$") && Regex.IsMatch(height, @"^\d+$"))
        {
            if (int.Parse(width) <= 0)
                return false;
            if (int.Parse(height) <= 0)
                return false;

            return true;
        }

        return false;
    }
}
