using System.Windows;
using System.Windows.Input;
using AntManager.Services;
using AntManager.Views;

namespace AntManager.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly AntApiService _antApiService;

    private string _serverIp = "localhost";
    private const string FixedPort = "8081";
    private const string FixedUsername = "admin";
    private const string FixedPassword = "123456";
    private string _errorMessage = string.Empty;
    private Visibility _isErrorVisible = Visibility.Collapsed;
    private bool _isLoggingIn = false;

    public string ServerUrl
    {
        get => _serverIp;
        set => SetProperty(ref _serverIp, value);
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

    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        set => SetProperty(ref _isLoggingIn, value);
    }

    public ICommand LoginCommand { get; }

    private Window _loginWindow;

    public LoginViewModel(Window loginWindow)
    {
        _loginWindow = loginWindow;
        _antApiService = AntApiService.Instance;
        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, CanExecuteLogin);
    }

    private bool CanExecuteLogin()
    {
        return !string.IsNullOrWhiteSpace(_serverIp) &&
               !IsLoggingIn;
    }

    private async Task ExecuteLoginAsync()
    {
        IsLoggingIn = true;
        IsErrorVisible = Visibility.Collapsed;
        ErrorMessage = string.Empty;

        try
        {
            var serverUrlWithPort = $"{_serverIp}:{FixedPort}";
            Console.WriteLine($"LoginViewModel: Attempting connection to {serverUrlWithPort}");
            Console.WriteLine($"LoginViewModel: Using fixed credentials (admin) and fixed port (8081)");
            var loginResponse = await _antApiService.LoginAsync(serverUrlWithPort, FixedUsername, FixedPassword);

            if (loginResponse.Success)
            {
                // 로그인 성공 - 토큰 정보 출력
                Console.WriteLine($"LoginViewModel: Connection and login successful");
                Console.WriteLine($"  Token: {loginResponse.Token}");
                Console.WriteLine($"  ApiVersion: {loginResponse.ApiVersion}");
                Console.WriteLine($"  DisplayName: {loginResponse.DisplayName}");

                // MainWindow 열기 (이미 연결된 상태)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    _loginWindow.Close();
                });
            }
            else
            {
                // 로그인 실패 - 상태 코드에 따라 에러 메시지 표시
                Console.WriteLine($"LoginViewModel: Login failed - StatusCode: {loginResponse.StatusCode}");
                ErrorMessage = loginResponse.ErrorMessage ?? "서버 연결 또는 로그인에 실패했습니다.";
                IsErrorVisible = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoginViewModel: Exception - {ex.Message}");
            ErrorMessage = $"로그인 오류: {ex.Message}";
            IsErrorVisible = Visibility.Visible;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }
}