using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DynamicData;
using ReactiveUI;
using FileConvert.ViewModels;
using ImageMagick;


namespace FileConvert.Views;

public partial class MainWindow : Window
{



    public MainWindow()
    {
        InitializeComponent();
    }

    private async void UpdateImage(string imageUrl)
    {
        if (imageUrl == "")
        {
            try
            {
                using var webClient = new WebClient();
                byte[] imageData = await webClient.DownloadDataTaskAsync("https://st4.depositphotos.com/14953852/22772/v/450/depositphotos_227725020-stock-illustration-image-available-icon-flat-vector.jpg");
                Bitmap bitmap = new Bitmap(new System.IO.MemoryStream(imageData));
                SelectedImage.Source = bitmap;
            } catch (Exception ex)
            {
                Console.WriteLine("Error loading image: " + ex.Message);
            }
        }

        if (imageUrl.Length > 3)
        {
            Bitmap defaultBitmap = new Bitmap(imageUrl);
            SelectedImage.Source = defaultBitmap;
        }
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
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel);

        // Video -> Photo (NOT ALLOWED)
        // Video -> Audio (ALLOWED)
        // Video -> Video (ALLOWED)
        // Photo -> Photo (ALLOWED)
        // Photo -> Audio (NOT ALLOWED)
        // Photo -> Video (NOT ALLOWED)
        // Audio -> Video (NOT ALLOWED)
        // Audio -> Photo (NOT ALLOWED)
        // Audio -> Audio (ALLOWED)

        //The file type of the original image file.
        int originalTypeIndex = -1; //0=Photo, 1=Video, 2=Audio

        //The file type you're trying to convert to.
        int conversionIndex = -1; //0=Photo, 1=Video, 2=Audio

        (int, string) fromConversionType = viewModel!.FileTypes.FirstOrDefault(x => x.Item2.Equals(viewModel.SelectedFileType, StringComparison.OrdinalIgnoreCase));
        (int, string) toConversionType = viewModel!.FileTypes.FirstOrDefault(x => x.Item2.Equals(viewModel.SelectedConversionType, StringComparison.OrdinalIgnoreCase));

        if (fromConversionType != default)
            originalTypeIndex = fromConversionType.Item1;

        if (toConversionType != default)
            conversionIndex = toConversionType.Item1;

        //ALLOWED CASES.

        ErrorText.Content = "";

        //Converting from VIDEO to AUDIO
        if (originalTypeIndex == 1 && conversionIndex == 2)
        {
            GenerateButton.IsEnabled = true;
        }
        //Converting from SAME TO SAME
        else if (originalTypeIndex == conversionIndex)
        {
            GenerateButton.IsEnabled = true;
        }
        //If you're trying to convert types that don't allow it.
        else
        {
            GenerateButton.IsEnabled = false;

            if (originalTypeIndex != -1) //This checks if a file has been provided by the user yet.
                ErrorText.Content = "Conversion Type Not Available!";
            else
                ErrorText.Content = "File Missing!";
        }

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
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;
        string[] allowedFileTypes = { "png", "jpg", "gif", "webp", "bmp", "tif" };

        if (viewModel.SelectedFilePath != null && viewModel.SelectedFilePath.Length > 3 && allowedFileTypes.Contains(viewModel.SelectedFileType))
            UpdateImage(viewModel.SelectedFilePath);
        else //Add separate logic for previewing video files here.
            UpdateImage("");
    }


    private void UpdateFileTypeLabelContent()
    {
        ConversionTypeLabel.Content = (DataContext as MainWindowViewModel)!.SelectedFileType;
    }

    //Will be called once the select file button is pressed.
    private async void OpenFileSelectWindow()
    {
        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFile> files = await GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Convert",
            AllowMultiple = false
        });


        if (files.Count != 1)
            return;

        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        viewModel.SelectedFilePath = files[0].Path.ToString().Remove(0, 8);

        string rawName = viewModel.SelectedFilePath.Split('/').Last().Split('.').First();
        string fileType = viewModel.SelectedFilePath.Split('/').Last().Split('.').Last();

        viewModel.SelectedFileName = rawName;
        viewModel.SelectedFileType = fileType;

        UpdateImage();
        UpdateFileTypeLabelContent();

        GenerateButton.IsEnabled = true;

        UpdateTypeConversionAccessibility();

        //The file the user has inputted's type. //0=Photo, 1=Video, 2=Audio
        (int, string) fromConversionType = viewModel!.FileTypes.FirstOrDefault(x => x.Item2.Equals(viewModel.SelectedFileType, StringComparison.OrdinalIgnoreCase));

        if (fromConversionType.Item1 is 0 or 1)
        {
            string imagePath = viewModel.SelectedFilePath!;

            try
            {
                using MagickImage img = new MagickImage(imagePath);
                
                int width = img.Width;
                int height = img.Height;
                ResolutionLabel.Content = $"{width}x{height}";
            } catch (MagickException ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        else
        {
            ResolutionLabel.Content = "Unavailable";
        }

        if (File.Exists(viewModel.SelectedFilePath))
        {
            // Get the file's size in bytes.
            long fileSizeInBytes = new FileInfo(viewModel.SelectedFilePath).Length;
            
            //Convert to kb, then to mb.
            double fileSizeInMegabytes = (fileSizeInBytes / 1024.0) / 1024.0; // Convert bytes to megabytes

            //Round it down to 2 decimal places.
            double finalSizeValue = Math.Round(fileSizeInMegabytes, 2);
            
            //Set the text on the UI.
            FileSizeLabel.Content = $"{finalSizeValue} MB";
        }
    }


    //Called via the 'Generate' button.
    private async void GenerateFile()
    {
        MainWindowViewModel viewModel = DataContext as MainWindowViewModel;


        if (viewModel.SelectedFilePath == null || viewModel.SelectedFilePath.Length <= 3)
        {
            Console.WriteLine("ERROR | Must select file!");
            return;
        }

        // Open a folder select prompt to select where you'd like to file to be outputted.
        IReadOnlyList<IStorageFolder> folders = await GetTopLevel(this).StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });

        //Make sure a folder was selected, & not more than 1.
        if (folders.Count != 1)
            return;

        //Get the file path in a string.
        string outputFilePath = folders[0].Path.ToString().Remove(0, 8);

        bool useFfmpeg = viewModel.GetIsUsingFFMPEG(); //Determine what conversion library to use.
        string outputFileName = viewModel.GetFileOutputName(outputFilePath); //Figure out which file name format will be used.

        if (useFfmpeg)
        {

            string runCommand = "cd " + outputFilePath + " && ffmpeg -i " + viewModel.SelectedFilePath + " " + outputFileName + $".{viewModel.SelectedConversionType.ToLower()}";
            viewModel.RunCMDCommand(runCommand, true);
            Console.WriteLine(runCommand);
        }
        else if (!useFfmpeg)
        {

            using(MagickImage image = new MagickImage(viewModel.SelectedFilePath))
            {
                image.Write(outputFilePath + "/" + outputFileName + "." + viewModel.SelectedConversionType.ToLower());
                Console.WriteLine(outputFilePath + "/" + outputFileName + "." + viewModel.SelectedConversionType.ToLower());
            }
        }

        OutputButton.IsEnabled = true;
    }
}
