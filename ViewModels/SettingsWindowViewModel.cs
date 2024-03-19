using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ReactiveUI;
namespace FileConvert.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static

    
    
    public ICommand SaveSettingsSelectCommand { get; } //Used as a hook for the 'Save' button, to save your settings.
    
    public ReactiveCommand<Unit, Unit> ToggleRandomNameCheckBoxCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSpecificNameCheckBoxCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSameNameCheckBoxCommand { get; }
    
    public MainWindowViewModel? MainWindowViewModel; //Storing the view model so methods can be called.

    public bool IsRandomNameSelected {
        get => _isRandomNameSelected; 
        set => this.RaiseAndSetIfChanged(ref _isRandomNameSelected, value);
    }
    public bool IsSpecificNameSelected {
        get => _isSpecificNameSelected; 
        set => this.RaiseAndSetIfChanged(ref _isSpecificNameSelected, value);
    }
    public bool IsSameNameSelected {
        get => _isSameNameSelected; 
        set => this.RaiseAndSetIfChanged(ref _isSameNameSelected, value);
    }
    

    private bool _isRandomNameSelected; //Is the 'random name' option selected.
    private bool _isSpecificNameSelected; //Is the 'specific name' option selected.
    private bool _isSameNameSelected; //Is the 'same name' option selected.
    

    
    public SettingsWindowViewModel()
    {
        SaveSettingsSelectCommand = ReactiveCommand.Create(SaveSettings);
        
        ToggleSpecificNameCheckBoxCommand = ReactiveCommand.Create(SelectSpecificName);
        ToggleRandomNameCheckBoxCommand = ReactiveCommand.Create(SelectRandomName);
        ToggleSameNameCheckBoxCommand = ReactiveCommand.Create(SelectSameName);
        
        InitializeComponent();
    }


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    
    private void SelectRandomName() => SelectFileNameOption("Random");

    private void SelectSpecificName() => SelectFileNameOption("Specific");

    private void SelectSameName() => SelectFileNameOption("Same");
    

    private void SelectFileNameOption(string option)
    {
        //Reset the current statuses.
        IsRandomNameSelected = false;
        IsSpecificNameSelected = false;
        IsSameNameSelected = false;
        
        switch (option)
        {
            case "Random":
                IsRandomNameSelected = true;
                break;
            case "Specific":
                IsSpecificNameSelected = true;
                break;
            case "Same":
                IsSameNameSelected = true;
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
