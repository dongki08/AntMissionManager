using System.Collections.Generic;

namespace AntManager.Models;

/// <summary>
/// 맵 전체 데이터
/// </summary>
public class MapData
{
    public int Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<MapLayer> Layers { get; set; } = new();
}

/// <summary>
/// 맵 레이어 (노드/링크 레이어, 로컬라이제이션 레이어 등)
/// </summary>
public class MapLayer
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<MapNode> Nodes { get; set; } = new();
    public List<MapLink> Links { get; set; } = new();
}

/// <summary>
/// 맵 노드 (x, y 좌표를 가진 포인트)
/// </summary>
public class MapNode
{
    public string Name { get; set; } = string.Empty;
    public string SymbolId { get; set; } = string.Empty;
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

/// <summary>
/// 맵 링크 (두 노드를 연결하는 선)
/// </summary>
public class MapLink
{
    public string Name { get; set; } = string.Empty;
    public string StyleId { get; set; } = string.Empty;
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    /// <summary>
    /// 이 링크가 연결하는 노드 ID 배열 (예: [22, 444])
    /// </summary>
    public List<int> NodeIds { get; set; } = new();
}
