using System;
using Avalonia.Controls;
using ReactiveUI;
using FileConvert.ViewModels;


namespace FileConvert.Views;

public partial class MainWindow : Window
{
    
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnComboBoxChangeSelected(object? sender, SelectionChangedEventArgs e)
    {
        string text = (e.AddedItems[0] as ComboBoxItem).Content as string;
        
        if(text != default)
            (DataContext as MainWindowViewModel).SelectedConversionType = text;
    }
}
