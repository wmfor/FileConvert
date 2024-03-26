using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace FileConvert.Views;

public partial class EnlargedImageWindow : Window
{
    
    
    public EnlargedImageWindow()
    {
        InitializeComponent();
    }

    public void OnClickCloseButton(object? sender, RoutedEventArgs e)
    {
        Hide();
    }
    
}

