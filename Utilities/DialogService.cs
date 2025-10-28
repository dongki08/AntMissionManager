using System.Windows;
using AntMissionManager.Views.Common;

namespace AntMissionManager.Utilities;

public enum MessageType
{
    Information,
    Warning,
    Error,
    Question
}

public enum MessageResult
{
    OK,
    Cancel,
    Yes,
    No
}

public interface IDialogService
{
    MessageResult ShowMessage(string message, string title = "메시지", MessageType messageType = MessageType.Information, bool showCancel = false);
    string? ShowOpenFileDialog(string filter = "모든 파일 (*.*)|*.*", string title = "파일 열기");
    string? ShowSaveFileDialog(string filter = "모든 파일 (*.*)|*.*", string title = "파일 저장", string defaultFileName = "");
}

public class DialogService : IDialogService
{
    public MessageResult ShowMessage(string message, string title = "메시지", MessageType messageType = MessageType.Information, bool showCancel = false)
    {
        var dialog = new MessageDialog
        {
            Owner = Application.Current.MainWindow,
            Title = title
        };

        dialog.SetMessage(message, messageType, showCancel);
        
        var result = dialog.ShowDialog();
        
        return result == true ? MessageResult.OK : MessageResult.Cancel;
    }

    public string? ShowOpenFileDialog(string filter = "모든 파일 (*.*)|*.*", string title = "파일 열기")
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
            CheckPathExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter = "모든 파일 (*.*)|*.*", string title = "파일 저장", string defaultFileName = "")
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            Title = title,
            FileName = defaultFileName,
            AddExtension = true,
            CheckPathExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}