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
    
    
    public bool IsRandomNameSelected {
        get => _IsRandomNameSelected;
        set => this.RaiseAndSetIfChanged(ref _IsRandomNameSelected, value);
    }
    public bool IsSpecificNameSelected{
        get => _IsSpecificNameSelected;
        set => this.RaiseAndSetIfChanged(ref _IsSpecificNameSelected, value);
    }
    public bool IsSameNameSelected
    {
        get =>  _IsSameNameSelected;
        set => this.RaiseAndSetIfChanged(ref _IsSameNameSelected, value);
    }

    public string SpecificName
    {
        get => _SpecificName;
        set => this.RaiseAndSetIfChanged(ref _SpecificName, value);
    }

    private bool _IsRandomNameSelected;
    private bool _IsSpecificNameSelected;
    private bool _IsSameNameSelected;
    private string _SpecificName;

    
    
    public SettingsWindowViewModel()
    {
        //Replace this when save/load is in. !!!!!!!!!!!!!!!! ##### !!!!!!!!!
        IsRandomNameSelected = true; //Replace this when save/load is in.
        //Replace this when save/load is in. !!!!!!!!!!!!!!!! ##### !!!!!!!!!
        
        SaveSettingsSelectCommand = ReactiveCommand.Create(SaveSettings);
    }


    
    


    public void SelectFileNameOption(string? option)
    {
        //Reset the current statuses.
        IsRandomNameSelected = false;
        IsSpecificNameSelected = false;
        IsSameNameSelected = false;

        //If you're just trying to reset the values.
        if (option == default)
            return;
        
        switch (option)
        {
            case "RandomCheckBox":
                IsRandomNameSelected = true;
                break;
            case "SpecificCheckBox":
                IsSpecificNameSelected = true;
                break;
            case "SameCheckBox":
               IsSameNameSelected = true;
                break;
        }
        
        Console.WriteLine($"Random Name - {IsRandomNameSelected}");
        Console.WriteLine($"Specific Name - {IsSpecificNameSelected}");
        Console.WriteLine($"Same Name - {IsSameNameSelected}");
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
