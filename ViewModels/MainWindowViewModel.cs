using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReactiveUI;
namespace FileConvert.ViewModels;

public class MainWindowViewModel : Window
{
#pragma warning disable CA1822 // Mark members as static
    public string Greeting
    {
        get
        {
            return "";
        }
    }


    public ICommand OpenFileSelectCommand { get; }

    public MainWindowViewModel()
    {
        OpenFileSelectCommand = ReactiveCommand.Create(OpenFileSelectWindow);
    }
    
    
    public async void OpenFileSelectWindow()
    {
        //Will be called once the select file button is pressed.
        
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        TopLevel? topLevel = GetTopLevel(this);
        
        // Start async operation to open the dialog.
        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Convert",
            AllowMultiple = false
        });

        
        
        if (files.Count == 1)
        {
            //Greeting = files[0].Name;
            // Open reading stream from the first file.
            await using Stream stream = await files[0].OpenReadAsync();
            using StreamReader streamReader = new StreamReader(stream);
            // Reads all the content of file as a text.
            string fileContent = await streamReader.ReadToEndAsync();
        }
    }
    
    
#pragma warning restore CA1822 // Mark members as static
}
