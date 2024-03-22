using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ReactiveUI;
using FileConvert.ViewModels;


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
            }
            catch (Exception ex)
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
        
        if(text != default)
            (DataContext as MainWindowViewModel)!.SelectedConversionType = text;

        UpdateConversionLabelContent();
    }

    private void OnSelectFileButtonClicked(object? sender, RoutedEventArgs e)
    {
        OpenFileSelectWindow();
        
        UpdateConversionLabelContent();

        UpdateImage();

       
    }

    private void UpdateImage()
    {
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;
        string[] allowedFileTypes = ["png", "jpg", "gif", "webp", "bmp", "tif"];
        
        if(viewModel.SelectedFilePath != null && viewModel.SelectedFilePath.Length > 3 && allowedFileTypes.Contains(viewModel.SelectedFileType))
            UpdateImage(viewModel.SelectedFilePath);
        else //Add separate logic for previewing video files here.
            UpdateImage("");
    }
    

    private void UpdateConversionLabelContent()
    {
        (DataContext as MainWindowViewModel)!.DataConversionType = (DataContext as MainWindowViewModel)!.SelectedFileType.ToUpper() + " to " + (DataContext as MainWindowViewModel)!.SelectedConversionType.ToUpper();
    }
    
    //Will be called once the select file button is pressed.
    private async void OpenFileSelectWindow()
    {
        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFile> files = await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Convert",
            AllowMultiple = false
        });


        if (files.Count != 1)
            return;
        
        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel);
        
        viewModel!.SelectedFilePath = files[0].Path.ToString().Remove(0,8);
        
        string rawName = viewModel!.SelectedFilePath.Split('/').Last().Split('.').First();
        string fileType = viewModel!.SelectedFilePath.Split('/').Last().Split('.').Last();

        viewModel!.SelectedFileName = rawName;
        viewModel!.SelectedFileType = fileType;
        
        UpdateImage();
        UpdateConversionLabelContent();
    }
}
