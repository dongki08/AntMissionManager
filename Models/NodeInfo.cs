using System.ComponentModel;

namespace AntMissionManager.Models;

public class NodeInfo : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _id = string.Empty;
    private double _x;
    private double _y;
    private bool _isAvailable = true;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public double X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set { _isAvailable = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class NodeData
{
    public string Orientation { get; set; } = string.Empty;
    public NodeInnerData Data { get; set; } = new();
}

public class NodeInnerData
{
    public List<NodeLayer> Layers { get; set; } = new();
}

public class NodeLayer
{
    public string Name { get; set; } = string.Empty;
    public List<NodeSymbol> Symbols { get; set; } = new();
}

public class NodeSymbol
{
    public string SymbolId { get; set; } = string.Empty;
    public List<double> Coord { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class NodePayload
{
    public List<NodeData> Data { get; set; } = new();
}