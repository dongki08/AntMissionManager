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
    private List<string> _path = new();
    private string _vehicleState = string.Empty;
    private string _targetNode = string.Empty;

    // 추가 필드들
    private bool _coverage;
    private int _port;
    private bool _isOmni;
    private bool _forceCharge;
    private string _actionName = string.Empty;
    private string _actionSourceId = string.Empty;
    private string _arrivalDate = string.Empty;
    private string _absArrivalDate = string.Empty;
    private string _actionNodeId = string.Empty;
    private int _currentNodeId;
    private string _mapName = string.Empty;
    private string _groupName = string.Empty;
    private List<double> _uncertainty = new();
    private string _connectionOk = string.Empty;
    private string _batteryMaxTemp = string.Empty;
    private string _batteryVoltage = string.Empty;
    private string _vehicleType = string.Empty;
    private string _lockUuid = string.Empty;
    private string _lockOwnerApp = string.Empty;
    private string _lockOwnerPc = string.Empty;
    private string _lockOwnerUser = string.Empty;
    private string _missionFrom = string.Empty;
    private string _missionTo = string.Empty;
    private string _missionFinal = string.Empty;
    private List<string> _errors = new();
    private bool _missionBlocked;

    // state 객체의 추가 필드들
    private string _actionSourceType = string.Empty;
    private List<string> _bodyShape = new();
    private List<string> _trafficInfo = new();
    private List<string> _missionProgress = new();
    private List<string> _errorBits = new();
    private List<string> _sharedMemoryOut = new();
    private List<string> _sharedMemoryIn = new();
    private List<string> _vehicleShape = new();
    private List<string> _errorDetailsLabel = new();
    private List<string> _messages = new();
    private List<string> _errorDetails = new();

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
        set { _alarms = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAlarms)); OnPropertyChanged(nameof(AlarmsText)); }
    }

    public bool HasAlarms => Alarms.Any();

    public Visibility HasAlarmsVisibility => HasAlarms ? Visibility.Visible : Visibility.Collapsed;

    public string AlarmsText => Alarms.Count > 0 ? string.Join(", ", Alarms) : "없음";

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

    public List<string> Path
    {
        get => _path;
        set { _path = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetNode)); }
    }

    public string VehicleState
    {
        get => _vehicleState;
        set { _vehicleState = value; OnPropertyChanged(); OnPropertyChanged(nameof(VehicleStateText)); }
    }

    public string VehicleStateText
    {
        get
        {
            return string.IsNullOrEmpty(VehicleState) ? "알 수 없음" : VehicleState;
        }
    }

    public string TargetNode
    {
        get
        {
            if (Path != null && Path.Count > 0)
            {
                return Path[Path.Count - 1];
            }
            return "없음";
        }
    }

    // 추가 프로퍼티들
    public bool Coverage
    {
        get => _coverage;
        set { _coverage = value; OnPropertyChanged(); }
    }

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public bool IsOmni
    {
        get => _isOmni;
        set { _isOmni = value; OnPropertyChanged(); }
    }

    public bool ForceCharge
    {
        get => _forceCharge;
        set { _forceCharge = value; OnPropertyChanged(); }
    }

    public string ActionName
    {
        get => _actionName;
        set { _actionName = value; OnPropertyChanged(); }
    }

    public string ActionSourceId
    {
        get => _actionSourceId;
        set { _actionSourceId = value; OnPropertyChanged(); }
    }

    public string ArrivalDate
    {
        get => _arrivalDate;
        set { _arrivalDate = value; OnPropertyChanged(); }
    }

    public string AbsArrivalDate
    {
        get => _absArrivalDate;
        set { _absArrivalDate = value; OnPropertyChanged(); }
    }

    public string ActionNodeId
    {
        get => _actionNodeId;
        set { _actionNodeId = value; OnPropertyChanged(); }
    }

    public int CurrentNodeId
    {
        get => _currentNodeId;
        set { _currentNodeId = value; OnPropertyChanged(); }
    }

    public string MapName
    {
        get => _mapName;
        set { _mapName = value; OnPropertyChanged(); }
    }

    public string GroupName
    {
        get => _groupName;
        set { _groupName = value; OnPropertyChanged(); }
    }

    public List<double> Uncertainty
    {
        get => _uncertainty;
        set { _uncertainty = value; OnPropertyChanged(); OnPropertyChanged(nameof(UncertaintyText)); }
    }

    public string UncertaintyText => Uncertainty.Count >= 2 ? $"({Uncertainty[0]:F4}, {Uncertainty[1]:F4})" : "Unknown";

    public string ConnectionOk
    {
        get => _connectionOk;
        set { _connectionOk = value; OnPropertyChanged(); }
    }

    public string BatteryMaxTemp
    {
        get => _batteryMaxTemp;
        set { _batteryMaxTemp = value; OnPropertyChanged(); }
    }

    public string BatteryVoltage
    {
        get => _batteryVoltage;
        set { _batteryVoltage = value; OnPropertyChanged(); }
    }

    public string VehicleType
    {
        get => _vehicleType;
        set { _vehicleType = value; OnPropertyChanged(); }
    }

    public string LockUuid
    {
        get => _lockUuid;
        set { _lockUuid = value; OnPropertyChanged(); }
    }

    public string LockOwnerApp
    {
        get => _lockOwnerApp;
        set { _lockOwnerApp = value; OnPropertyChanged(); }
    }

    public string LockOwnerPc
    {
        get => _lockOwnerPc;
        set { _lockOwnerPc = value; OnPropertyChanged(); }
    }

    public string LockOwnerUser
    {
        get => _lockOwnerUser;
        set { _lockOwnerUser = value; OnPropertyChanged(); }
    }

    public string MissionFrom
    {
        get => _missionFrom;
        set { _missionFrom = value; OnPropertyChanged(); }
    }

    public string MissionTo
    {
        get => _missionTo;
        set { _missionTo = value; OnPropertyChanged(); }
    }

    public string MissionFinal
    {
        get => _missionFinal;
        set { _missionFinal = value; OnPropertyChanged(); }
    }

    public List<string> Errors
    {
        get => _errors;
        set { _errors = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorsText)); }
    }

    public string ErrorsText => Errors.Count > 0 ? string.Join(", ", Errors) : "없음";

    public bool MissionBlocked
    {
        get => _missionBlocked;
        set { _missionBlocked = value; OnPropertyChanged(); }
    }

    // state 객체의 추가 프로퍼티들
    public string ActionSourceType
    {
        get => _actionSourceType;
        set { _actionSourceType = value; OnPropertyChanged(); }
    }

    public List<string> BodyShape
    {
        get => _bodyShape;
        set { _bodyShape = value; OnPropertyChanged(); OnPropertyChanged(nameof(BodyShapeText)); }
    }

    public string BodyShapeText => BodyShape.Count > 0 ? string.Join(", ", BodyShape) : "없음";

    public List<string> TrafficInfo
    {
        get => _trafficInfo;
        set { _trafficInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrafficInfoText)); }
    }

    public string TrafficInfoText => TrafficInfo.Count > 0 ? string.Join(", ", TrafficInfo) : "없음";

    public List<string> MissionProgress
    {
        get => _missionProgress;
        set { _missionProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(MissionProgressText)); }
    }

    public string MissionProgressText => MissionProgress.Count > 0 ? string.Join(", ", MissionProgress) : "없음";

    public List<string> ErrorBits
    {
        get => _errorBits;
        set { _errorBits = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorBitsText)); }
    }

    public string ErrorBitsText => ErrorBits.Count > 0 ? string.Join(", ", ErrorBits) : "없음";

    public List<string> SharedMemoryOut
    {
        get => _sharedMemoryOut;
        set { _sharedMemoryOut = value; OnPropertyChanged(); OnPropertyChanged(nameof(SharedMemoryOutText)); }
    }

    public string SharedMemoryOutText => SharedMemoryOut.Count > 0 ? string.Join(", ", SharedMemoryOut) : "없음";

    public List<string> SharedMemoryIn
    {
        get => _sharedMemoryIn;
        set { _sharedMemoryIn = value; OnPropertyChanged(); OnPropertyChanged(nameof(SharedMemoryInText)); }
    }

    public string SharedMemoryInText => SharedMemoryIn.Count > 0 ? string.Join(", ", SharedMemoryIn) : "없음";

    public List<string> VehicleShape
    {
        get => _vehicleShape;
        set { _vehicleShape = value; OnPropertyChanged(); OnPropertyChanged(nameof(VehicleShapeText)); }
    }

    public string VehicleShapeText => VehicleShape.Count > 0 ? string.Join(", ", VehicleShape) : "없음";

    public List<string> ErrorDetailsLabel
    {
        get => _errorDetailsLabel;
        set { _errorDetailsLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorDetailsLabelText)); }
    }

    public string ErrorDetailsLabelText => ErrorDetailsLabel.Count > 0 ? string.Join(", ", ErrorDetailsLabel) : "없음";

    public List<string> Messages
    {
        get => _messages;
        set { _messages = value; OnPropertyChanged(); OnPropertyChanged(nameof(MessagesText)); }
    }

    public string MessagesText => Messages.Count > 0 ? string.Join(", ", Messages) : "없음";

    public List<string> ErrorDetails
    {
        get => _errorDetails;
        set { _errorDetails = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorDetailsText)); }
    }

    public string ErrorDetailsText => ErrorDetails.Count > 0 ? string.Join(", ", ErrorDetails) : "없음";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
