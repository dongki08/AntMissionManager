using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AntMissionManager.Models;
using AntMissionManager.ViewModels;

namespace AntMissionManager.Views;

public partial class MapView : UserControl, INotifyPropertyChanged
{
    private Point _lastMousePosition;
    private bool _isDragging;
    private double _zoomLevel = 1.0;
    private double _offsetX = 0;
    private double _offsetY = 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private const double MIN_ZOOM = 0.1;
    private const double MAX_ZOOM = 5.0;
    private const double ZOOM_FACTOR = 0.1;

    public static readonly DependencyProperty MapDataProperty =
        DependencyProperty.Register(
            nameof(MapData),
            typeof(List<MapData>),
            typeof(MapView),
            new PropertyMetadata(null, OnMapDataChanged));

    public static readonly DependencyProperty VehiclesProperty =
        DependencyProperty.Register(
            nameof(Vehicles),
            typeof(List<Vehicle>),
            typeof(MapView),
            new PropertyMetadata(null, OnVehiclesChanged));

    public List<MapData>? MapData
    {
        get => (List<MapData>?)GetValue(MapDataProperty);
        set => SetValue(MapDataProperty, value);
    }

    public List<Vehicle>? Vehicles
    {
        get => (List<Vehicle>?)GetValue(VehiclesProperty);
        set => SetValue(VehiclesProperty, value);
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            var newValue = Math.Clamp(value, MIN_ZOOM, MAX_ZOOM);
            if (Math.Abs(_zoomLevel - newValue) > 0.001)
            {
                _zoomLevel = newValue;
                OnPropertyChanged();
                RenderMap();
            }
        }
    }

    public double OffsetX
    {
        get => _offsetX;
        set
        {
            if (Math.Abs(_offsetX - value) > 0.001)
            {
                _offsetX = value;
                OnPropertyChanged();
                RenderMap();
            }
        }
    }

    public double OffsetY
    {
        get => _offsetY;
        set
        {
            if (Math.Abs(_offsetY - value) > 0.001)
            {
                _offsetY = value;
                OnPropertyChanged();
                RenderMap();
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ICommand ResetViewCommand { get; }

    public MapView()
    {
        InitializeComponent();
        // DataContext는 부모로부터 상속받아야 MapData 바인딩이 작동함
        Loaded += MapView_Loaded;
        SizeChanged += MapView_SizeChanged;

        ResetViewCommand = new RelayCommand(_ => ResetView());
    }

    private void MapView_Loaded(object sender, RoutedEventArgs e)
    {
        Services.MapLogger.Log($"MapView Loaded - Canvas Size: {MapCanvas.ActualWidth}x{MapCanvas.ActualHeight}");
        ResetView();
    }

    private void MapView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"MapView SizeChanged - Canvas Size: {MapCanvas.ActualWidth}x{MapCanvas.ActualHeight}");
        RenderMap();
    }

    private static void OnMapDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MapView mapView)
        {
            Services.MapLogger.Log($"OnMapDataChanged triggered - Old: {e.OldValue != null}, New: {e.NewValue != null}");
            if (e.NewValue is List<MapData> newData)
            {
                Services.MapLogger.Log($"  New MapData has {newData.Count} maps");
            }
            mapView.RenderMap();
        }
    }

    private static void OnVehiclesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MapView mapView)
        {
            Services.MapLogger.Log("OnVehiclesChanged triggered");
            mapView.RenderMap();
        }
    }

    private void ResetView()
    {
        Services.MapLogger.Log("ResetView called");
        _zoomLevel = 1.0;
        _offsetX = 0;
        _offsetY = 0;
        RenderMap();
    }

    private void RenderMap()
    {
        MapCanvas.Children.Clear();

        Services.MapLogger.LogSection("MapView - RenderMap Called");
        Services.MapLogger.Log($"MapData is null: {MapData == null}");

        if (MapData == null || !MapData.Any())
        {
            Services.MapLogger.Log("MapData is null or empty - showing waiting message");

            // Add debug text to canvas
            var debugText = new TextBlock
            {
                Text = "Waiting for map data...",
                Foreground = Brushes.White,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(debugText, 100);
            Canvas.SetTop(debugText, 100);
            MapCanvas.Children.Add(debugText);
            return;
        }

        Services.MapLogger.Log($"MapData count: {MapData.Count}");

        var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 800;
        var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 600;

        Services.MapLogger.Log($"Canvas Size: {canvasWidth}x{canvasHeight}");

        // Calculate bounds
        var allNodes = MapData.SelectMany(m => m.Layers.SelectMany(l => l.Nodes)).ToList();
        Services.MapLogger.Log($"Total nodes: {allNodes.Count}");

        if (!allNodes.Any())
        {
            Services.MapLogger.Log("No nodes found - showing error message");

            // Add debug text
            var debugText = new TextBlock
            {
                Text = "No nodes in map data!",
                Foreground = Brushes.Red,
                FontSize = 20
            };
            Canvas.SetLeft(debugText, 100);
            Canvas.SetTop(debugText, 100);
            MapCanvas.Children.Add(debugText);
            return;
        }

        var minX = allNodes.Min(n => n.X);
        var maxX = allNodes.Max(n => n.X);
        var minY = allNodes.Min(n => n.Y);
        var maxY = allNodes.Max(n => n.Y);

        Services.MapLogger.Log($"Bounds - X: [{minX:F2}, {maxX:F2}], Y: [{minY:F2}, {maxY:F2}]");

        var mapWidth = maxX - minX;
        var mapHeight = maxY - minY;

        Services.MapLogger.Log($"Map Size: {mapWidth:F2}x{mapHeight:F2}");

        if (mapWidth == 0 || mapHeight == 0)
        {
            Services.MapLogger.Log("Map width or height is 0 - cannot render!");
            return;
        }

        // Calculate scale with padding
        var padding = 50;
        var scaleX = (canvasWidth - padding * 2) / mapWidth;
        var scaleY = (canvasHeight - padding * 2) / mapHeight;
        var baseScale = Math.Min(scaleX, scaleY);

        // Apply zoom
        var scale = baseScale * _zoomLevel;

        // Center offset
        var centerOffsetX = canvasWidth / 2 - (minX + mapWidth / 2) * scale;
        var centerOffsetY = canvasHeight / 2 - (minY + mapHeight / 2) * scale;

        // Transform helper
        Point Transform(double x, double y)
        {
            return new Point(
                x * scale + centerOffsetX + _offsetX,
                y * scale + centerOffsetY + _offsetY
            );
        }

        // Render links (lines) - Draw ALL links from ALL layers
        int linkCount = 0;
        foreach (var map in MapData)
        {
            Services.MapLogger.Log($"Rendering Map: {map.Alias}, Layers: {map.Layers.Count}");

            foreach (var layer in map.Layers)
            {
                Services.MapLogger.Log($"  Layer: '{layer.Name}', Nodes: {layer.Nodes.Count}, Links: {layer.Links.Count}");

                // Draw ALL links regardless of layer name
                foreach (var link in layer.Links)
                {
                    var p1 = Transform(link.X1, link.Y1);
                    var p2 = Transform(link.X2, link.Y2);

                    var line = new Line
                    {
                        X1 = p1.X,
                        Y1 = p1.Y,
                        X2 = p2.X,
                        Y2 = p2.Y,
                        Stroke = new SolidColorBrush(Color.FromRgb(0, 191, 255)), // Bright cyan
                        StrokeThickness = 2.5
                    };

                    MapCanvas.Children.Add(line);
                    linkCount++;
                }
            }
        }
        Services.MapLogger.Log($"Rendered {linkCount} links");

        // Render mission paths (bright red lines)
        if (Vehicles != null)
        {
            foreach (var vehicle in Vehicles)
            {
                if (vehicle.Path != null && vehicle.Path.Count >= 2)
                {
                    for (int i = 0; i < vehicle.Path.Count - 1; i++)
                    {
                        var fromNodeName = vehicle.Path[i];
                        var toNodeName = vehicle.Path[i + 1];

                        var fromNode = allNodes.FirstOrDefault(n => n.Name == fromNodeName);
                        var toNode = allNodes.FirstOrDefault(n => n.Name == toNodeName);

                        if (fromNode != null && toNode != null)
                        {
                            var p1 = Transform(fromNode.X, fromNode.Y);
                            var p2 = Transform(toNode.X, toNode.Y);

                            var pathLine = new Line
                            {
                                X1 = p1.X,
                                Y1 = p1.Y,
                                X2 = p2.X,
                                Y2 = p2.Y,
                                Stroke = new SolidColorBrush(Color.FromRgb(255, 50, 50)), // Bright red
                                StrokeThickness = 4
                            };

                            MapCanvas.Children.Add(pathLine);
                        }
                    }
                }
            }
        }

        // Render nodes - Draw ALL nodes from ALL layers
        int nodeCount = 0;
        foreach (var map in MapData)
        {
            foreach (var layer in map.Layers)
            {
                // Draw ALL nodes regardless of layer name
                foreach (var node in layer.Nodes)
                {
                    var pos = Transform(node.X, node.Y);

                    // Outer glow effect
                    var outerGlow = new Ellipse
                    {
                        Width = 18,
                        Height = 18,
                        Fill = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)), // Gold glow
                        StrokeThickness = 0
                    };

                    Canvas.SetLeft(outerGlow, pos.X - 9);
                    Canvas.SetTop(outerGlow, pos.Y - 9);
                    MapCanvas.Children.Add(outerGlow);

                    // Main node circle
                    var nodeEllipse = new Ellipse
                    {
                        Width = 14,
                        Height = 14,
                        Fill = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                        Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // White border
                        StrokeThickness = 2.5
                    };

                    Canvas.SetLeft(nodeEllipse, pos.X - 7);
                    Canvas.SetTop(nodeEllipse, pos.Y - 7);

                    MapCanvas.Children.Add(nodeEllipse);

                    // Node label
                    if (!string.IsNullOrEmpty(node.Name))
                    {
                        var label = new TextBlock
                        {
                            Text = node.Name,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Bright yellow
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                            Padding = new Thickness(5, 2, 5, 2)
                        };

                        Canvas.SetLeft(label, pos.X + 10);
                        Canvas.SetTop(label, pos.Y - 7);

                        MapCanvas.Children.Add(label);
                    }

                    nodeCount++;
                }
            }
        }
        Services.MapLogger.Log($"Rendered {nodeCount} nodes");
        Services.MapLogger.Log($"Total canvas children: {MapCanvas.Children.Count}");
        Services.MapLogger.Log("RenderMap completed successfully");

        // Render vehicles (only insert state)
        if (Vehicles != null)
        {
            foreach (var vehicle in Vehicles)
            {
                // Only show vehicle if it's in insert state (operatingstate != extract)
                // Assuming operatingstate: 0=extract, 1=insert, etc.
                if (vehicle.OperatingState == 0)
                    continue;

                if (vehicle.Coordinates != null && vehicle.Coordinates.Count >= 2)
                {
                    var pos = Transform(vehicle.Coordinates[0], vehicle.Coordinates[1]);

                    // Outer pulse effect
                    var outerPulse = new Ellipse
                    {
                        Width = 26,
                        Height = 26,
                        Fill = new SolidColorBrush(Color.FromArgb(100, 0, 255, 0)), // Green glow
                        StrokeThickness = 0
                    };

                    Canvas.SetLeft(outerPulse, pos.X - 13);
                    Canvas.SetTop(outerPulse, pos.Y - 13);
                    MapCanvas.Children.Add(outerPulse);

                    // Vehicle circle
                    var vehicleEllipse = new Ellipse
                    {
                        Width = 18,
                        Height = 18,
                        Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Bright green
                        Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Yellow border
                        StrokeThickness = 3
                    };

                    Canvas.SetLeft(vehicleEllipse, pos.X - 9);
                    Canvas.SetTop(vehicleEllipse, pos.Y - 9);

                    MapCanvas.Children.Add(vehicleEllipse);

                    // Vehicle label
                    var vehicleLabel = new TextBlock
                    {
                        Text = vehicle.Name,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Bright green
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush(Color.FromArgb(230, 0, 0, 0)),
                        Padding = new Thickness(6, 3, 6, 3)
                    };

                    Canvas.SetLeft(vehicleLabel, pos.X + 12);
                    Canvas.SetTop(vehicleLabel, pos.Y - 10);

                    MapCanvas.Children.Add(vehicleLabel);
                }
            }
        }
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom in/out with mouse wheel
        var delta = e.Delta > 0 ? ZOOM_FACTOR : -ZOOM_FACTOR;
        ZoomLevel += delta;
        e.Handled = true;
    }

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastMousePosition = e.GetPosition(MapCanvas);
        MapCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        var currentPosition = e.GetPosition(MapCanvas);
        var delta = currentPosition - _lastMousePosition;

        // Vertical drag for zoom
        if (Math.Abs(delta.Y) > Math.Abs(delta.X))
        {
            // Drag up = zoom out, drag down = zoom in
            var zoomDelta = -delta.Y * 0.01;
            ZoomLevel += zoomDelta;
        }
        else
        {
            // Horizontal drag for pan
            OffsetX += delta.X;
            OffsetY += delta.Y;
        }

        _lastMousePosition = currentPosition;
        e.Handled = true;
    }

    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        MapCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logPath = Services.MapLogger.GetLogFilePath();
            var logDir = System.IO.Path.GetDirectoryName(logPath);

            if (!string.IsNullOrEmpty(logDir))
            {
                // Open folder and select the log file
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{logPath}\"");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening log folder: {ex.Message}");
        }
    }
}
