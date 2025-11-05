using System.Windows;
using AntManager.Views;

namespace AntManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Console.WriteLine("App.xaml.cs: Creating LoginWindow");
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }
}