using System.Windows;
using System.Windows.Controls;
using AntManager.ViewModels;

namespace AntManager.Views;

public partial class ApiTestView : UserControl
{
    public ApiTestView()
    {
        InitializeComponent();
        DataContext = new ApiTestViewModel();
    }
    
    private void OpenHeadersDialog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ApiTestViewModel viewModel)
        {
            var dialog = new HeadersAuthDialog(viewModel);
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }

    private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ApiTestViewModel viewModel)
        {
            viewModel.IsUrlFocused = true;

            // Popup이 열리면 Popup 내부 TextBox로 포커스 이동
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (UrlPopup.Child is System.Windows.Controls.Border border &&
                    border.Child is System.Windows.Controls.TextBox popupTextBox)
                {
                    popupTextBox.Focus();
                    popupTextBox.SelectionStart = UrlTextBox.SelectionStart;
                    popupTextBox.SelectionLength = UrlTextBox.SelectionLength;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void UrlTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Popup으로 포커스가 이동하는 경우 무시
    }

    private void UrlPopupTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ApiTestViewModel viewModel)
        {
            viewModel.IsUrlFocused = true;
        }
    }

    private void UrlPopupTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ApiTestViewModel viewModel)
        {
            viewModel.IsUrlFocused = false;
        }
    }
}
