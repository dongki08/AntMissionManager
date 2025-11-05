using System.ComponentModel;

namespace AntManager.Models;

public class AlarmInfo : INotifyPropertyChanged
{
    private string _uuid = string.Empty;
    private string _sourceId = string.Empty;
    private string _sourceType = string.Empty;
    private string _eventName = string.Empty;
    private string _alarmMessage = string.Empty;
    private int _eventCount;
    private DateTime _firstEventAt;
    private DateTime _lastEventAt;
    private DateTime _timestamp;
    private int _state;
    private DateTime? _closedAt;
    private DateTime? _clearedAt;

    public string Uuid
    {
        get => _uuid;
        set { _uuid = value; OnPropertyChanged(); }
    }

    public string SourceId
    {
        get => _sourceId;
        set { _sourceId = value; OnPropertyChanged(); }
    }

    public string SourceType
    {
        get => _sourceType;
        set { _sourceType = value; OnPropertyChanged(); OnPropertyChanged(nameof(SourceTypeText)); }
    }

    public string SourceTypeText
    {
        get
        {
            return SourceType switch
            {
                "vehicle" => "차량",
                "mission" => "미션",
                "system" => "시스템",
                _ => SourceType
            };
        }
    }

    public string EventName
    {
        get => _eventName;
        set { _eventName = value; OnPropertyChanged(); OnPropertyChanged(nameof(EventDisplayName)); OnPropertyChanged(nameof(SeverityColor)); }
    }

    public string EventDisplayName
    {
        get
        {
            // Extract the last part of the event name for display
            var parts = EventName.Split('.');
            return parts.Length > 0 ? parts[^1] : EventName;
        }
    }

    public string AlarmMessage
    {
        get => _alarmMessage;
        set { _alarmMessage = value; OnPropertyChanged(); }
    }

    public int EventCount
    {
        get => _eventCount;
        set { _eventCount = value; OnPropertyChanged(); }
    }

    public DateTime FirstEventAt
    {
        get => _firstEventAt;
        set { _firstEventAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(FirstEventAtText)); }
    }

    public string FirstEventAtText => FirstEventAt.ToString("yyyy-MM-dd HH:mm:ss");

    public DateTime LastEventAt
    {
        get => _lastEventAt;
        set { _lastEventAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastEventAtText)); }
    }

    public string LastEventAtText => LastEventAt.ToString("yyyy-MM-dd HH:mm:ss");

    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimestampText)); }
    }

    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    public int State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); OnPropertyChanged(nameof(StateColor)); }
    }

    public string StateText
    {
        get
        {
            return State switch
            {
                0 => "발생",
                1 => "진행중",
                2 => "닫힘",
                3 => "해결됨",
                _ => $"알 수 없음 ({State})"
            };
        }
    }

    public string StateColor
    {
        get
        {
            return State switch
            {
                0 => "#EF4444", // Red - Active
                1 => "#F59E0B", // Orange - In Progress
                2 => "#6B7280", // Gray - Closed
                3 => "#10B981", // Green - Cleared
                _ => "#6B7280"  // Gray - Unknown
            };
        }
    }

    public string SeverityColor
    {
        get
        {
            // Determine severity based on event name
            if (EventName.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                EventName.Contains("failed", StringComparison.OrdinalIgnoreCase))
                return "#EF4444"; // Red - Error
            else if (EventName.Contains("warning", StringComparison.OrdinalIgnoreCase))
                return "#F59E0B"; // Orange - Warning
            else if (EventName.Contains("info", StringComparison.OrdinalIgnoreCase))
                return "#3B82F6"; // Blue - Info
            else
                return "#6B7280"; // Gray - Default
        }
    }

    public DateTime? ClosedAt
    {
        get => _closedAt;
        set { _closedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClosedAtText)); }
    }

    public string ClosedAtText => ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public DateTime? ClearedAt
    {
        get => _clearedAt;
        set { _clearedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClearedAtText)); }
    }

    public string ClearedAtText => ClearedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
