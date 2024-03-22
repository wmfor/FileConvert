using System;
using System.Linq;
using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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
        
        if(text != default)
            (DataContext as MainWindowViewModel)!.SelectedConversionType = text;

        UpdateConversionLabelContent();
    }

    private void OnSelectFileButtonClicked(object? sender, RoutedEventArgs e)
    {
        UpdateConversionLabelContent();

        MainWindowViewModel viewModel = (DataContext as MainWindowViewModel)!;

        string[] allowedFileTypes = ["png", "jpg", "gif", "webp", "bmp", "tif"];
        
        if(viewModel.SelectedFilePath != null && viewModel.SelectedFilePath.Length > 3 && allowedFileTypes.Contains(viewModel.SelectedFileType))
            UpdateImage(viewModel.SelectedFilePath);
        else
            UpdateImage("");
    }

    private void UpdateConversionLabelContent()
    {
        (DataContext as MainWindowViewModel)!.DataConversionType = (DataContext as MainWindowViewModel)!.SelectedFileType.ToUpper() + " to " + (DataContext as MainWindowViewModel)!.SelectedConversionType.ToUpper();
    }
}
