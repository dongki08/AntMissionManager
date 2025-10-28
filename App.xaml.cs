using System.Windows;
using AntMissionManager.Views;

namespace AntMissionManager;

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