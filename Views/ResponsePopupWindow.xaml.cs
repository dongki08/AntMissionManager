using System.Windows;
using System.Windows.Input;

namespace AntManager.Views;

public partial class ResponsePopupWindow : Window
{
    public ResponsePopupWindow(string responseBody, string responseStatus, int responseTime)
    {
        InitializeComponent();

        ResponseBodyTextBox.Text = responseBody;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 더블클릭 시 최대화/복원 토글
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            e.Handled = true;
        }
        else if (e.ClickCount == 1 && e.ChangedButton == MouseButton.Left)
        {
            // 싱글클릭 시 창 이동
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove 예외 무시
            }
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(ResponseBodyTextBox.Text))
            {
                Clipboard.SetText(ResponseBodyTextBox.Text);

                CommonDialogWindow.ShowDialog(
                    "클립보드에 복사되었습니다.",
                    "복사 완료",
                    CommonDialogWindow.DialogType.Info);
            }
        }
        catch
        {
            CommonDialogWindow.ShowDialog(
                "클립보드 복사에 실패했습니다.",
                "복사 오류",
                CommonDialogWindow.DialogType.Error);
        }
    }
}
