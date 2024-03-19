using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReactiveUI;
namespace FileConvert.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static

    
    
    public ICommand SaveSettingsSelectCommand { get; }
    
    public MainWindowViewModel? MainWindowViewModel;


    
    public SettingsWindowViewModel()
    {
        SaveSettingsSelectCommand = ReactiveCommand.Create(SaveSettings);
    }
    
    
    
    
    private void SaveSettings()
    {
        if (MainWindowViewModel == null)
            return;
        
        //Check if directory exists,
        //If directory doesn't exist, create it.
        
        Console.WriteLine("Saved!");
        
       
        //Write new data to save file.
        
        //Close Settings Window
    }

#pragma warning restore CA1822 // Mark members as static
}
