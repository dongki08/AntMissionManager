using System.ComponentModel;

namespace AntManager.Models;

public class MissionInfo : INotifyPropertyChanged
{
    private string _missionId = string.Empty;
    private string _missionType = string.Empty;
    private string _fromNode = string.Empty;
    private string _toNode = string.Empty;
    private string _assignedVehicle = string.Empty;
    private int _navigationState;
    private int _transportState;
    private int _priority;
    private DateTime _createdAt;
    private DateTime? _arrivingTime;
    private string _createdAtDisplay = string.Empty;

    public string MissionId
    {
        get => _missionId;
        set
        {
            _missionId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MissionIdSortValue));
        }
    }

    public int MissionIdSortValue
    {
        get
        {
            if (int.TryParse(MissionId, out var numericId))
            {
                return numericId;
            }

            return int.MinValue;
        }
    }

    public string MissionType
    {
        get => _missionType;
        set { _missionType = value; OnPropertyChanged(); }
    }

    public string FromNode
    {
        get => _fromNode;
        set { _fromNode = value; OnPropertyChanged(); }
    }

    public string ToNode
    {
        get => _toNode;
        set { _toNode = value; OnPropertyChanged(); }
    }

    public string AssignedVehicle
    {
        get => _assignedVehicle;
        set { _assignedVehicle = value; OnPropertyChanged(); }
    }

    public int NavigationState
    {
        get => _navigationState;
        set
        {
            _navigationState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NavigationStateText));
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    public string NavigationStateText
    {
        get
        {
            return NavigationState switch
            {
                1 => "대기(1)",
                2 => "거부(2)",
                3 => "진행중(3)",
                4 => "완료(4)",
                5 => "취소(5)",
                0 => "수신(0)",
                _ => $"알 수 없음 ({NavigationState})"
            };
        }
    }

    public bool CanCancel => NavigationState == 1 || NavigationState == 3;

    public int TransportState
    {
        get => _transportState;
        set { _transportState = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransportStateText)); }
    }

    public string TransportStateText
    {
        get
        {
            return TransportState switch
            {
                0 => "새로운 작업 (New)",
                1 => "승인됨 (Accepted)",
                3 => "할당됨 (Assigned)",
                4 => "이동중 (Moving)",
                5 => "운송중 (Transporting)",
                6 => "배송지 선택중 (Selecting)",
                7 => "배송중 (Delivering)",
                8 => "완료됨 (Completed)",
                9 => "취소됨 (Cancelled)",
                10 => "오류 (Error)",
                11 => "취소 처리중 (Cancelling)",
                15 => "일시정지 (Paused)",
                _ => $"알 수 없음 ({TransportState})"
            };
        }
    }

    public int Priority
    {
        get => _priority;
        set { _priority = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriorityText)); }
    }

    public string PriorityText
    {
        get
        {
            return Priority switch
            {
                0 => "없음",
                1 => "낮음",
                2 => "보통",
                3 => "높음",
                _ => Priority.ToString()
            };
        }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set
        {
            _createdAt = value;
            if (string.IsNullOrEmpty(_createdAtDisplay) && _createdAt != DateTime.MinValue)
            {
                _createdAtDisplay = _createdAt.ToString("yyyy-MM-dd HH:mm:ss");
                OnPropertyChanged(nameof(CreatedAtDisplay));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(QueueTimestamp));
        }
    }

    public DateTime? ArrivingTime
    {
        get => _arrivingTime;
        set { _arrivingTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(QueueTimestamp)); }
    }

    public string CreatedAtDisplay
    {
        get => _createdAtDisplay;
        set { _createdAtDisplay = value; OnPropertyChanged(); }
    }

    public DateTime QueueTimestamp => ArrivingTime ?? CreatedAt;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
