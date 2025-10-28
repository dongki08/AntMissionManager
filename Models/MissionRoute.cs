using System.ComponentModel;

namespace AntMissionManager.Models;

public class MissionRoute : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private List<string> _nodes = new();
    private string _missionType = string.Empty;
    private DateTime _createdAt;
    private bool _isActive;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public List<string> Nodes
    {
        get => _nodes;
        set { _nodes = value; OnPropertyChanged(); OnPropertyChanged(nameof(NodesPath)); }
    }

    public string NodesPath => string.Join(" → ", Nodes);

    public string MissionType
    {
        get => _missionType;
        set { _missionType = value; OnPropertyChanged(); }
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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RouteNode : INotifyPropertyChanged
{
    private int _index;
    private string _nodeName = string.Empty;

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public string NodeName
    {
        get => _nodeName;
        set { _nodeName = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}