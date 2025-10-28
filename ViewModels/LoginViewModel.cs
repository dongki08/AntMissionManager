using System.Windows;
using System.Windows.Input;
using AntMissionManager.Views;

namespace AntMissionManager.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private string _username = "admin";
    private string _password = "123456";
    private string _errorMessage = string.Empty;
    private Visibility _isErrorVisible = Visibility.Collapsed;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public Visibility IsErrorVisible
    {
        get => _isErrorVisible;
        set => SetProperty(ref _isErrorVisible, value);
    }

    public ICommand LoginCommand { get; }

    private Window _loginWindow;

    public LoginViewModel(Window loginWindow)
    {
        _loginWindow = loginWindow;
        LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
    }

    private bool CanExecuteLogin()
    {
        return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }

    private void ExecuteLogin()
    {
        // 간단한 인증 로직
        if (Username == "admin" && Password == "123456")
        {
            // 로그인 성공
            Console.WriteLine("LoginViewModel.cs: Creating MainWindow");
            var mainWindow = new MainWindow();
            mainWindow.Show();

            // 현재 로그인 창 닫기
            Console.WriteLine("LoginViewModel.cs: Closing LoginWindow");
            _loginWindow.Close();
        }
        else
        {
            // 로그인 실패
            ErrorMessage = "사용자명 또는 비밀번호가 올바르지 않습니다.";
            IsErrorVisible = Visibility.Visible;
        }
    }
}