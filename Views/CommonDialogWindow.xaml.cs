using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AntManager.Views;

public partial class CommonDialogWindow : Window
{
    public enum DialogType
    {
        Question,
        Warning,
        Error,
        Info
    }

    public enum CommonDialogResult
    {
        None,
        Yes,
        No
    }

    public CommonDialogResult Result { get; private set; } = CommonDialogResult.None;
    public StackPanel ContentHost => ContentRootPanel;
    public Button PrimaryButton => ConfirmButton;
    public Button SecondaryButton => CancelButton;
    public TextBlock MessageBlock => MessageText;

    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;

    public CommonDialogWindow(string message, string title = "Confirmation", DialogType type = DialogType.Question)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Visibility = Visibility.Visible;
        MessageText.Text = message;

        // Set icon and color based on dialog type
        switch (type)
        {
            case DialogType.Question:
                IconType.Kind = MaterialDesignThemes.Wpf.PackIconKind.HelpCircle;
                IconType.Foreground = (Brush)FindResource("PrimaryBrush");
                break;
            case DialogType.Warning:
                IconType.Kind = MaterialDesignThemes.Wpf.PackIconKind.Alert;
                IconType.Foreground = (Brush)FindResource("WarningBrush");
                break;
            case DialogType.Error:
                IconType.Kind = MaterialDesignThemes.Wpf.PackIconKind.CloseCircle;
                IconType.Foreground = (Brush)FindResource("ErrorBrush");
                break;
            case DialogType.Info:
                IconType.Kind = MaterialDesignThemes.Wpf.PackIconKind.Information;
                IconType.Foreground = (Brush)FindResource("AccentBrush");
                // Info 타입은 확인 버튼만 표시
                CancelButton.Visibility = Visibility.Collapsed;
                ConfirmButton.Content = "확인";
                // 3초 후 자동으로 닫기
                _autoCloseTimer = new System.Windows.Threading.DispatcherTimer();
                _autoCloseTimer.Interval = TimeSpan.FromSeconds(3);
                _autoCloseTimer.Tick += (s, e) =>
                {
                    _autoCloseTimer.Stop();
                    if (IsLoaded && !IsClosing())
                    {
                        Result = CommonDialogResult.Yes;
                        DialogResult = true;
                        Close();
                    }
                };
                _autoCloseTimer.Start();
                break;
        }
    }

    private bool IsClosing()
    {
        return PresentationSource.FromVisual(this) == null;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        Result = CommonDialogResult.Yes;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        Result = CommonDialogResult.No;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        Result = CommonDialogResult.None;
        DialogResult = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            this.DragMove();
        }
    }

    public static bool ShowDialog(string message, string title = "Confirmation", DialogType type = DialogType.Question, Window? owner = null)
    {
        var dialog = new CommonDialogWindow(message, title, type);

        if (owner != null)
        {
            dialog.Owner = owner;
        }
        else
        {
            // Find the main window
            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive)
                {
                    dialog.Owner = window;
                    break;
                }
            }
        }

        dialog.ShowDialog();
        return dialog.Result == CommonDialogResult.Yes;
    }
}
