using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace AntMissionManager.Models;

public class Vehicle : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private int _operatingState;
    private string _location = string.Empty;
    private string _missionId = string.Empty;
    private int _batteryLevel;
    private List<string> _alarms = new();
    private DateTime _lastUpdate;
    private string _ipAddress = string.Empty;
    private bool _isSimulated;
    private bool _isLoaded;
    private string _payload = string.Empty;
    private List<double> _coordinates = new();
    private double _course;
    private string _currentNodeName = string.Empty;
    private int _traveledDistance;
    private int _cumulativeUptime;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public int OperatingState
    {
        get => _operatingState;
        set { _operatingState = value; OnPropertyChanged(); OnPropertyChanged(nameof(OperatingStateText)); }
    }

    public string OperatingStateText
    {
        get
        {
            return OperatingState switch
            {
                0 => "Idle (대기)",
                1 => "Running (실행중)",
                2 => "Charging (충전중)",
                3 => "Error (오류)",
                4 => "Maintenance (정비)",
                _ => $"Unknown ({OperatingState})"
            };
        }
    }

    public string Location
    {
        get => _location;
        set { _location = value; OnPropertyChanged(); }
    }

    public string MissionId
    {
        get => _missionId;
        set { _missionId = value; OnPropertyChanged(); }
    }

    public int BatteryLevel
    {
        get => _batteryLevel;
        set { _batteryLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLowBattery)); }
    }

    public bool HasLowBattery => BatteryLevel <= 30;

    public List<string> Alarms
    {
        get => _alarms;
        set { _alarms = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAlarms)); }
    }

    public bool HasAlarms => Alarms.Any();
    
    public Visibility HasAlarmsVisibility => HasAlarms ? Visibility.Visible : Visibility.Collapsed;

    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set { _lastUpdate = value; OnPropertyChanged(); }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(); }
    }

    public bool IsSimulated
    {
        get => _isSimulated;
        set { _isSimulated = value; OnPropertyChanged(); }
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set { _isLoaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoadStatusText)); }
    }

    public string LoadStatusText => IsLoaded ? "적재됨" : "비어있음";

    public string Payload
    {
        get => _payload;
        set { _payload = value; OnPropertyChanged(); }
    }

    public List<double> Coordinates
    {
        get => _coordinates;
        set { _coordinates = value; OnPropertyChanged(); OnPropertyChanged(nameof(CoordinatesText)); }
    }

    public string CoordinatesText => Coordinates.Count >= 2 ? $"({Coordinates[0]:F1}, {Coordinates[1]:F1})" : "Unknown";

    public double Course
    {
        get => _course;
        set { _course = value; OnPropertyChanged(); OnPropertyChanged(nameof(CourseText)); }
    }

    public string CourseText => $"{Course * 180 / Math.PI:F1}°";

    public string CurrentNodeName
    {
        get => _currentNodeName;
        set { _currentNodeName = value; OnPropertyChanged(); }
    }

    public int TraveledDistance
    {
        get => _traveledDistance;
        set { _traveledDistance = value; OnPropertyChanged(); OnPropertyChanged(nameof(TraveledDistanceText)); }
    }

    public string TraveledDistanceText => $"{TraveledDistance / 1000.0:F1} km";

    public int CumulativeUptime
    {
        get => _cumulativeUptime;
        set { _cumulativeUptime = value; OnPropertyChanged(); OnPropertyChanged(nameof(UptimeText)); }
    }

    public string UptimeText
    {
        get
        {
            var hours = CumulativeUptime / 3600;
            var minutes = (CumulativeUptime % 3600) / 60;
            return $"{hours}h {minutes}m";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}