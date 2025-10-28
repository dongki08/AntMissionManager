using System.Windows;
using MaterialDesignThemes.Wpf;
using AntMissionManager.Utilities;

namespace AntMissionManager.Views.Common;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }
    
    public static MessageBoxResult Show(string message, AntMissionManager.Utilities.MessageType type = AntMissionManager.Utilities.MessageType.Information, MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var dialog = new MessageDialog();
        dialog.MessageText.Text = message;
        dialog.SetMessageType(dialog.ConvertMessageType(type));
        dialog.SetButtons(buttons);
        
        return dialog.ShowDialog() == true ? MessageBoxResult.Yes : 
               dialog.DialogResult == false ? MessageBoxResult.No : MessageBoxResult.Cancel;
    }
    
    public void SetMessage(string message, AntMissionManager.Utilities.MessageType type, bool showCancel = false)
    {
        MessageText.Text = message;
        SetMessageType(ConvertMessageType(type));
        
        if (showCancel)
        {
            CancelButton.Visibility = Visibility.Visible;
        }
        else
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }
    }
    
    private LocalMessageType ConvertMessageType(AntMissionManager.Utilities.MessageType type)
    {
        return type switch
        {
            AntMissionManager.Utilities.MessageType.Information => LocalMessageType.Information,
            AntMissionManager.Utilities.MessageType.Warning => LocalMessageType.Warning,
            AntMissionManager.Utilities.MessageType.Error => LocalMessageType.Error,
            AntMissionManager.Utilities.MessageType.Question => LocalMessageType.Information,
            _ => LocalMessageType.Information
        };
    }
    
    private void SetMessageType(LocalMessageType type)
    {
        switch (type)
        {
            case LocalMessageType.Information:
                MessageIcon.Kind = PackIconKind.Information;
                MessageIcon.Foreground = System.Windows.Media.Brushes.DodgerBlue;
                break;
            case LocalMessageType.Warning:
                MessageIcon.Kind = PackIconKind.Warning;
                MessageIcon.Foreground = System.Windows.Media.Brushes.Orange;
                break;
            case LocalMessageType.Error:
                MessageIcon.Kind = PackIconKind.Error;
                MessageIcon.Foreground = System.Windows.Media.Brushes.Red;
                break;
            case LocalMessageType.Success:
                MessageIcon.Kind = PackIconKind.CheckCircle;
                MessageIcon.Foreground = System.Windows.Media.Brushes.Green;
                break;
        }
    }

    private enum LocalMessageType
    {
        Information,
        Warning,
        Error,
        Success
    }
    
    private void SetButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case MessageBoxButton.YesNo:
                OkButton.Content = "예";
                CancelButton.Content = "아니오";
                CancelButton.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.OKCancel:
                CancelButton.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNoCancel:
                OkButton.Content = "예";
                CancelButton.Content = "아니오";
                CancelButton.Visibility = Visibility.Visible;
                
                var cancelBtn = new System.Windows.Controls.Button
                {
                    Content = "취소",
                    Width = 80,
                    Margin = new Thickness(8, 4, 8, 4)
                };
                cancelBtn.Click += (s, e) => { DialogResult = null; Close(); };
                ButtonPanel.Children.Add(cancelBtn);
                break;
        }
    }
    
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}