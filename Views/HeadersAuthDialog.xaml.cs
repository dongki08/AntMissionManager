using System.Windows;
using System.Windows.Input;
using AntManager.ViewModels;

namespace AntManager.Views;

public partial class HeadersAuthDialog : Window
{
    public HeadersAuthDialog(ApiTestViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
