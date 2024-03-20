using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
namespace FileConvert.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static

    
    
    public ICommand SaveSettingsSelectCommand { get; } //Used as a hook for the 'Save' button, to save your settings.
    
    public MainWindowViewModel? MainWindowViewModel; //Storing the view model so methods can be called.


    

    
    
    public SettingsWindowViewModel()
    {
        SaveSettingsSelectCommand = ReactiveCommand.Create(SaveSettings);
    }


    
    


    private void SelectFileNameOption(string option)
    {
        //Reset the current statuses.
       // IsRandomNameSelected = false;
       // IsSpecificNameSelected = false;
       // IsSameNameSelected = false;
        
        switch (option)
        {
            case "Random":
           //     IsRandomNameSelected = true;
                break;
            case "Specific":
            //    IsSpecificNameSelected = true;
                break;
            case "Same":
           //     IsSameNameSelected = true;
                break;
        }
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
