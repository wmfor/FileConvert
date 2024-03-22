using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileConvert.ViewModels;
using ReactiveUI;

namespace FileConvert.Views;

public partial class SettingsWindow : Window
{
    //Stores the checkboxes as variables.
    private readonly CheckBox? _SameNameCheckbox;
    private readonly CheckBox? _RandomNameCheckbox;
    private readonly CheckBox? _SpecificNameCheckbox;
    
    //This makes sure that the OnCheckBoxChanged doesn't happen infinitely.
    private bool _IsProcessingCheckboxChanges;
    
    
    //Primary Constructor.
    public SettingsWindow()
    {
        InitializeComponent();

        //Find the checkboxes.
        _SameNameCheckbox = this.FindControl<CheckBox>("SameCheckBox");
        _SpecificNameCheckbox = this.FindControl<CheckBox>("SpecificCheckBox");
        _RandomNameCheckbox = this.FindControl<CheckBox>("RandomCheckBox");
    }


    private void OnTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        (DataContext as SettingsWindowViewModel)!.SpecificName = textBox.Text;
    }
    
    //When you check a checkbox.
    private void OnCheckboxChanged(object sender, RoutedEventArgs e)
    {
        //If the checkbox is null, return.
        if (sender is not CheckBox checkBox)
            return;
        
        if (_IsProcessingCheckboxChanges)
            return;
        
        _IsProcessingCheckboxChanges = true; //Start the operation.
        

        //Store the checkbox values.
        bool isChecked = (bool)checkBox.IsChecked!;
        string? checkBoxName = checkBox.Name;

        _SameNameCheckbox!.IsChecked = false;
        _SpecificNameCheckbox!.IsChecked = false;
        _RandomNameCheckbox!.IsChecked = false;
            
        switch (checkBoxName)
        {
            case "SameCheckBox":
                _SameNameCheckbox.IsChecked = true;
                break;
            case "SpecificCheckBox":
                _SpecificNameCheckbox.IsChecked = true;
                break;
            case "RandomCheckBox":
                _RandomNameCheckbox.IsChecked = true;
                break;
        }

        //If it's set to checked, pass the checked box, otherwise pass default to reset all.
        (DataContext as SettingsWindowViewModel)!.SelectFileNameOption(isChecked ? checkBoxName : default);
         
        _IsProcessingCheckboxChanges = false; //End the operation.
    }
    
}

