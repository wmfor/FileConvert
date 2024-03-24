using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FileConvert.ViewModels;
using ImageMagick;


namespace FileConvert.Views;

public partial class MainWindow : Window
{

    private bool IsConversionValid;
    private bool IsResolutionValid;
    private bool IsFilePresent;
    
    

    public MainWindow()
    {
        InitializeComponent();
        
        SetUIResolution(false, 0,0);
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

    private void OnComboBoxChangeSelected(object? sender, SelectionChangedEventArgs e)
    {
        string? text = (e.AddedItems[0] as ComboBoxItem)!.Content as string;

        Console.WriteLine(text);


        if (text != default)
            (DataContext as MainWindowViewModel)!.SelectedConversionType = text;

        UpdateFileTypeLabelContent();

        UpdateTypeConversionAccessibility();

    }

    private void UpdateTypeConversionAccessibility()
    { //Makes sure you're doing a conversion within the same class of file. (e.g. Photo -> Photo, Video -> Video.)

        MainWindowViewModel ViewModel = DataContext as MainWindowViewModel;

        //The file type of the original image file.
        int originalTypeIndex = -1; //0=Photo, 1=Video, 2=Audio

        //The file type you're trying to convert to.
        int conversionIndex = -1; //0=Photo, 1=Video, 2=Audio

        (int, string) fromConversionType = ViewModel.FileTypes.FirstOrDefault(x => x.Item2.Equals(ViewModel.SelectedFileType, StringComparison.OrdinalIgnoreCase));
        (int, string) toConversionType = ViewModel.FileTypes.FirstOrDefault(x => x.Item2.Equals(ViewModel.SelectedConversionType, StringComparison.OrdinalIgnoreCase));

        if (fromConversionType != default)
            originalTypeIndex = fromConversionType.Item1;

        if (toConversionType != default)
            conversionIndex = toConversionType.Item1;

        if (originalTypeIndex == -1)
            IsFilePresent = false;
        else
            IsFilePresent = true;
        
        //Converting from VIDEO to AUDIO
        if (originalTypeIndex == 1 && conversionIndex == 2)
            IsConversionValid = true;
        //Converting from SAME TO SAME
        else if (originalTypeIndex == conversionIndex)
            IsConversionValid = true;
        //If you're trying to convert types that don't allow it.
        else
            IsConversionValid = false;
        
        IsGenerationViable();
    }

    private void IsGenerationViable()
    {
        if (IsFilePresent == false)
            ErrorText.Content = "File Missing!"; 
        else if (IsConversionValid == false)
            ErrorText.Content = "Conversion Type Not Available!"; 
        else if (IsResolutionValid == false)
            ErrorText.Content = "Resolution Not Valid!"; 
        else
            ErrorText.Content = "";
        
        if (IsResolutionValid && IsConversionValid && IsFilePresent)
            GenerateButton.IsEnabled = true;
        else
            GenerateButton.IsEnabled = false;
    }
    

    private void OnResolutionInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        IsResolutionValid = AreResolutionInputsValid();
        
        IsGenerationViable();
    }

    private void OnSelectFileButtonClicked(object? sender, RoutedEventArgs e)
    {
        OpenFileSelectWindow();
    }

    private void OnGenerateButtonClicked(object? sender, RoutedEventArgs e)
    {
        GenerateFile();
    }

    private void UpdateImage()
    {
        MainWindowViewModel ViewModel = (DataContext as MainWindowViewModel);
        
        string[] allowedFileTypes = { "png", "jpg", "gif", "webp", "bmp", "tif", "ico", "tiff"};

        if (ViewModel.SelectedFilePath != null && ViewModel.SelectedFilePath.Length > 3 && allowedFileTypes.Contains(ViewModel.SelectedFileType))
            UpdateImage(ViewModel.SelectedFilePath);
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
        MainWindowViewModel ViewModel = (DataContext as MainWindowViewModel);
        
        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFile> files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Convert",
            AllowMultiple = false
        });


        if (files.Count != 1)
            return;

        ViewModel.SelectedFilePath = files[0].Path.ToString().Remove(0, 8);

        string rawName = ViewModel.SelectedFilePath.Split('/').Last().Split('.').First();
        string fileType = ViewModel.SelectedFilePath.Split('/').Last().Split('.').Last();

        ViewModel.SelectedFileName = rawName;
        ViewModel.SelectedFileType = fileType;

        UpdateImage();
        UpdateFileTypeLabelContent();

        GenerateButton.IsEnabled = true;

        UpdateTypeConversionAccessibility(); 

        //Updates the UI for the newly selected file's resolution (if available)
        UpdateSelectedFileResolution();
       
        //Updates the UI for the newly selected file's file size (always available).
        UpdateSelectedFileSize();
    }

    private void UpdateSelectedFileSize()
    {
        MainWindowViewModel ViewModel = (DataContext as MainWindowViewModel);
        
        //FILE SIZE CALCULATION 
        if (File.Exists(ViewModel.SelectedFilePath))
        {
            // Get the file's size in bytes.
            long fileSizeInBytes = new FileInfo(ViewModel.SelectedFilePath).Length;
            
            //Convert to kb, then to mb.
            double fileSizeInMegabytes = (fileSizeInBytes / 1024.0) / 1024.0; // Convert bytes to megabytes

            //Round it down to 2 decimal places.
            double finalSizeValue = Math.Round(fileSizeInMegabytes, 2);
            
            //Set the text on the UI.
            FileSizeLabel.Content = $"{finalSizeValue} MB";
        }
    }

    private void UpdateSelectedFileResolution()
    {
        MainWindowViewModel ViewModel = (DataContext as MainWindowViewModel);
        
        //The file the user has inputted's type. //0=Photo, 1=Video, 2=Audio
        (int, string) fromConversionType = ViewModel!.FileTypes.FirstOrDefault(x => x.Item2.Equals(ViewModel.SelectedFileType, StringComparison.OrdinalIgnoreCase));

        bool isPhotoOrVideo = fromConversionType.Item1 is 0 or 1;
        
        //RESOLUTION CALCULATION
        if (isPhotoOrVideo)
        {
            string imagePath = ViewModel.SelectedFilePath!;

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
            SetUIResolution(false, 0,0);
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
        MainWindowViewModel ViewModel = (DataContext as MainWindowViewModel);
        
        if (ViewModel.SelectedFilePath == null || ViewModel.SelectedFilePath.Length <= 3)
            return;

        // Open a folder select prompt to select where you'd like to file to be outputted.
        IReadOnlyList<IStorageFolder> folders = await GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });

        //Make sure a folder was selected, & not more than 1.
        if (folders.Count != 1)
            return;

        //Get the file path in a string.
        string outputFilePath = folders[0].Path.ToString().Remove(0, 8);

        //Determine what conversion library to use. (FFMPEG for Audio/Video, ImageMagick for Photo)
        bool useFfmpeg = ViewModel.GetIsUsingFFMPEG(); 
        
        //Determine which file name format will be used. (e.g. Specific, Same, Random)
        string outputFileName = ViewModel.GetFileOutputName(outputFilePath);

        switch (useFfmpeg)
        {
            case true:
            {
                string runCommand = "cd " + outputFilePath + " && ffmpeg -i " + ViewModel.SelectedFilePath + " " + outputFileName + $".{ViewModel.SelectedConversionType.ToLower()}";
                ViewModel.RunCMDCommand(runCommand, true);
                break;
            }
            case false:
            {
                using MagickImage image = new MagickImage(ViewModel.SelectedFilePath);

                if (AreResolutionInputsValid())
                    image.Resize(int.Parse(ResolutionWidthInput.Text!), int.Parse(ResolutionHeightInput.Text!));

                image.Quality = 100 - (int)CompressionSlider.Value;
                
                await image.WriteAsync(outputFilePath + "/" + outputFileName + "." + ViewModel.SelectedConversionType.ToLower());
                break;
            }
        }

        OutputButton.IsEnabled = true;
    }

    private bool AreResolutionInputsValid()
    {
        string width = ResolutionWidthInput.Text!;
        string height = ResolutionHeightInput.Text!;

        if (width == "" || width == default)
            return false;
        if (height == "" || height == default)
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
