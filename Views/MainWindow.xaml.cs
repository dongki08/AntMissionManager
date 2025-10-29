using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AntMissionManager.Models;
using AntMissionManager.ViewModels;

namespace AntMissionManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            this.DragMove();
        }
        else if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            this.WindowState = WindowState.Normal;
        }
        else
        {
            this.WindowState = WindowState.Maximized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
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

    private void VehicleRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row && row.Item is Vehicle vehicle)
        {
            var detailWindow = new VehicleDetailWindow(vehicle);
            detailWindow.ShowDialog();
        }
    }
}