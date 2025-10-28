using System.Windows;
using System.Windows.Controls;
using AntMissionManager.Models;
using AntMissionManager.ViewModels;

namespace AntMissionManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        
        // 서버 비밀번호 바인딩
        ServerPasswordBox.PasswordChanged += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Password = ServerPasswordBox.Password;
            }
        };
    }
    
    private void EditRoute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MissionRoute route && DataContext is MainViewModel vm)
        {
            vm.EditRouteCommand.Execute(route);
        }
    }

    private void DeleteRoute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MissionRoute route && DataContext is MainViewModel vm)
        {
            vm.DeleteRouteCommand.Execute(route);
        }
    }
}