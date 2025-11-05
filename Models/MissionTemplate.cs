using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AntManager.Models;

public enum MissionTemplateType
{
    Moving = 1,           // 무빙 미션 (API: 8)
    PickAndDrop = 2,      // 픽앤드롭 미션 (API: 7)
    Dynamic = 3           // 다이나믹 미션 (API: 7)
}

public class MissionTemplate : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = string.Empty;
    private MissionTemplateType _templateType = MissionTemplateType.Moving;
    private string _fromNode = string.Empty;
    private string _toNode = string.Empty;
    private string _vehicle = string.Empty;
    private int _priority = 2;
    private string _priorityDescription = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private bool _isActive = true;

    // Dynamic Mission 전용
    private DynamicNodeConfig? _fromNodeConfig;
    private DynamicNodeConfig? _toNodeConfig;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public MissionTemplateType TemplateType
    {
        get => _templateType;
        set
        {
            _templateType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TemplateTypeName));
            OnPropertyChanged(nameof(IsMoving));
            OnPropertyChanged(nameof(IsPickAndDrop));
            OnPropertyChanged(nameof(IsDynamic));
            OnPropertyChanged(nameof(MissionTypeNumber));
        }
    }

    public string TemplateTypeName => TemplateType switch
    {
        MissionTemplateType.Moving => "무빙 미션",
        MissionTemplateType.PickAndDrop => "픽앤드롭 미션",
        MissionTemplateType.Dynamic => "다이나믹 미션",
        _ => "알 수 없음"
    };

    public bool IsMoving => TemplateType == MissionTemplateType.Moving;
    public bool IsPickAndDrop => TemplateType == MissionTemplateType.PickAndDrop;
    public bool IsDynamic => TemplateType == MissionTemplateType.Dynamic;

    // API 전송용 미션 타입 번호
    public int MissionTypeNumber => TemplateType switch
    {
        MissionTemplateType.Moving => 8,
        MissionTemplateType.PickAndDrop => 7,
        MissionTemplateType.Dynamic => 7,
        _ => 7
    };

    public string FromNode
    {
        get => _fromNode;
        set { _fromNode = value; OnPropertyChanged(); OnPropertyChanged(nameof(RouteDisplay)); }
    }

    public string ToNode
    {
        get => _toNode;
        set { _toNode = value; OnPropertyChanged(); OnPropertyChanged(nameof(RouteDisplay)); }
    }

    public string Vehicle
    {
        get => _vehicle;
        set { _vehicle = value; OnPropertyChanged(); }
    }

    public int Priority
    {
        get => _priority;
        set { _priority = value; OnPropertyChanged(); }
    }

    public string PriorityDescription
    {
        get => _priorityDescription;
        set { _priorityDescription = value; OnPropertyChanged(); }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public DynamicNodeConfig? FromNodeConfig
    {
        get => _fromNodeConfig;
        set { _fromNodeConfig = value; OnPropertyChanged(); }
    }

    public DynamicNodeConfig? ToNodeConfig
    {
        get => _toNodeConfig;
        set { _toNodeConfig = value; OnPropertyChanged(); }
    }

    public string RouteDisplay
    {
        get
        {
            var from = string.IsNullOrWhiteSpace(FromNode) ? "N/A" : FromNode;
            var to = string.IsNullOrWhiteSpace(ToNode) ? "N/A" : ToNode;
            return $"{from} → {to}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DynamicNodeConfig : INotifyPropertyChanged
{
    private string _nodeType = "Dynamic_Lift";
    private ObservableCollection<DynamicNodeVar> _vars = new();

    public string NodeType
    {
        get => _nodeType;
        set { _nodeType = value; OnPropertyChanged(); }
    }

    public ObservableCollection<DynamicNodeVar> Vars
    {
        get => _vars;
        set { _vars = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DynamicNodeVar : INotifyPropertyChanged
{
    private string _key = string.Empty;
    private string _value = string.Empty;

    public string Key
    {
        get => _key;
        set { _key = value; OnPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
