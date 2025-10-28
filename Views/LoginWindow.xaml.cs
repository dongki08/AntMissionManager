using System.Windows;
using System.Windows.Input;
using AntMissionManager.ViewModels;

namespace AntMissionManager.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel(this);
        PasswordBox.KeyDown += PasswordBox_KeyDown;
        
        // PasswordBox 바인딩을 위한 이벤트 핸들러
        PasswordBox.PasswordChanged += (s, e) =>
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        };
    }
    
    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
            {
                vm.LoginCommand.Execute(null);
            }
        }
    }
    
    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = PasswordBox.Password;
            if (vm.LoginCommand.CanExecute(null))
            {
                vm.LoginCommand.Execute(null);
            }
        }
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            this.DragMove();
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}