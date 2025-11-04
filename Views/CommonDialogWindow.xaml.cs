using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AntMissionManager.Views;

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
                break;
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CommonDialogResult.Yes;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CommonDialogResult.No;
        DialogResult = false;
        Close();
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
