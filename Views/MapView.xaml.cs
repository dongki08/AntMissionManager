using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AntMissionManager.Models;
using AntMissionManager.ViewModels;
using AntMissionManager.Services;

namespace AntMissionManager.Views;

public partial class MapView : UserControl, INotifyPropertyChanged
{
    private Point _lastMousePosition;
    private bool _isDragging;
    private double _zoomLevel = 1.0;
    private double _offsetX = 0;
    private double _offsetY = 0;
    private double _rotationAngle = 0;
    private bool _isFlippedHorizontally = false;
    private double _nodeSize = 5;
    private double _vehicleSize = 16;
    private DispatcherTimer? _vehicleUpdateTimer;
    private const string VehicleElementTag = "__VehicleLayer";
    private readonly Dictionary<string, SolidColorBrush> _vehiclePathBrushes = new();
    private readonly Color[] _vehiclePathPalette =
    {
        Color.FromRgb(255, 99, 71),   // Tomato
        Color.FromRgb(65, 105, 225),  // Royal Blue
        Color.FromRgb(60, 179, 113),  // Medium Sea Green
        Color.FromRgb(238, 130, 238), // Violet
        Color.FromRgb(255, 215, 0),   // Gold
        Color.FromRgb(70, 130, 180),  // Steel Blue
        Color.FromRgb(255, 140, 0),   // Dark Orange
        Color.FromRgb(46, 139, 87),   // Sea Green
        Color.FromRgb(186, 85, 211),  // Medium Orchid
        Color.FromRgb(30, 144, 255)   // Dodger Blue
    };
    private bool _skipStaticRender = false;

    private struct MapTransformContext
    {
        public double Scale;
        public double CenterOffsetX;
        public double CenterOffsetY;
        public double MapCenterXScaled;
        public double MapCenterYScaled;
    }

    // Settings and optimization
    private readonly MapSettingsService _settingsService = new();
    private DispatcherTimer? _renderTimer;
    private bool _needsRender = false;
    private readonly Dictionary<string, UIElement> _cachedElements = new();
    private List<MapData>? _cachedMapData;
    private DateTime _lastRenderTime = DateTime.MinValue;
    
    // Settings panel state
    private bool _isSettingsPanelOpen = false;
    private double _originalRotationAngle = 0;
    private bool _originalFlipState = false;

    // Snackbar functionality
    private Border? _snackbar;
    private DispatcherTimer? _snackbarTimer;
    private string? _hoveredNodeName;

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
            typeof(ObservableCollection<Vehicle>),
            typeof(MapView),
            new PropertyMetadata(null, OnVehiclesChanged));

    public List<MapData>? MapData
    {
        get => (List<MapData>?)GetValue(MapDataProperty);
        set => SetValue(MapDataProperty, value);
    }

    public ObservableCollection<Vehicle>? Vehicles
    {
        get => (ObservableCollection<Vehicle>?)GetValue(VehiclesProperty);
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
                ScheduleRender();
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
                ScheduleRender();
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
                ScheduleRender();
            }
        }
    }

    public double RotationAngle
    {
        get => _rotationAngle;
        set
        {
            var newValue = value % 360;
            if (Math.Abs(_rotationAngle - newValue) > 0.001)
            {
                _rotationAngle = newValue;
                OnPropertyChanged();
                ScheduleRender();
            }
        }
    }

    public bool IsFlippedHorizontally
    {
        get => _isFlippedHorizontally;
        set
        {
            if (_isFlippedHorizontally != value)
            {
                _isFlippedHorizontally = value;
                OnPropertyChanged();
                ScheduleRender();
            }
        }
    }

    public double NodeSize
    {
        get => _nodeSize;
        set
        {
            var newValue = Math.Clamp(value, 1, 10);
            if (Math.Abs(_nodeSize - newValue) > 0.001)
            {
                _nodeSize = newValue;
                OnPropertyChanged();
                ScheduleRender();
            }
        }
    }

    public double VehicleSize
    {
        get => _vehicleSize;
        set
        {
            var newValue = Math.Clamp(value, 8, 40);
            if (Math.Abs(_vehicleSize - newValue) > 0.001)
            {
                _vehicleSize = newValue;
                OnPropertyChanged();
                ScheduleRender();
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
        Unloaded += MapView_Unloaded;
        SizeChanged += MapView_SizeChanged;

        ResetViewCommand = new RelayCommand(_ => ResetView());
        _vehicleUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _vehicleUpdateTimer.Tick += VehicleUpdateTimer_Tick;
        
        // Initialize render timer for optimization
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(8) // ~120 FPS for smoother dragging
        };
        _renderTimer.Tick += RenderTimer_Tick;

        // Initialize snackbar timer
        _snackbarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _snackbarTimer.Tick += SnackbarTimer_Tick;

        // Load settings
        _ = LoadSettingsAsync();
    }

    private void MapView_Loaded(object sender, RoutedEventArgs e)
    {
        Services.MapLogger.Log($"MapView Loaded - Canvas Size: {MapCanvas.ActualWidth}x{MapCanvas.ActualHeight}");
        _vehicleUpdateTimer?.Start();
        ResetView();
    }

    private void MapView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"MapView SizeChanged - Canvas Size: {MapCanvas.ActualWidth}x{MapCanvas.ActualHeight}");
        ScheduleRender();
    }

    private static void OnMapDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MapView mapView)
        {
            Services.MapLogger.Log($"OnMapDataChanged triggered - Old: {e.OldValue != null}, New: {e.NewValue != null}");
            if (e.NewValue is List<MapData> newData)
            {
                Services.MapLogger.Log($"  New MapData has {newData.Count} maps");
                mapView._cachedMapData = newData;
            }
            mapView.ScheduleRender();
        }
    }

    private static void OnVehiclesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MapView mapView)
        {
            Services.MapLogger.Log($"OnVehiclesChanged triggered - Old: {e.OldValue != null}, New: {e.NewValue != null}");
            if (e.NewValue is ObservableCollection<Vehicle> newVehicles)
            {
                Services.MapLogger.Log($"  New Vehicles list has {newVehicles.Count} vehicles");
                foreach (var vehicle in newVehicles)
                {
                    Services.MapLogger.Log($"    Vehicle: {vehicle.Name}, State: {vehicle.VehicleState}, Coords: {vehicle.Coordinates?.Count ?? 0}");
                }
            }
            mapView.ScheduleRender();
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
        if (!_skipStaticRender)
        {
            MapCanvas.Children.Clear();
            _cachedElements.Clear();
        }

        var mapData = _cachedMapData ?? MapData;

        if (mapData == null || !mapData.Any())
        {
            _skipStaticRender = false;
            MapCanvas.Children.Clear();
            _cachedElements.Clear();

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

        var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 800;
        var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 600;

        // Calculate bounds
        var allNodes = mapData.SelectMany(m => m.Layers.SelectMany(l => l.Nodes)).ToList();

        if (!allNodes.Any())
        {
            _skipStaticRender = false;
            MapCanvas.Children.Clear();
            _cachedElements.Clear();

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

        var mapWidth = maxX - minX;
        var mapHeight = maxY - minY;

        if (mapWidth == 0 || mapHeight == 0)
        {
            _skipStaticRender = false;
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

        // Transform helper with rotation and flip
        Point Transform(double x, double y)
        {
            // Apply scaling first
            var scaledX = x * scale;
            var scaledY = y * scale;

            // Apply horizontal flip if enabled
            if (_isFlippedHorizontally)
            {
                var mapCenterX = (minX + maxX) / 2 * scale;
                scaledX = 2 * mapCenterX - scaledX;
            }

            // Apply rotation if any
            if (Math.Abs(_rotationAngle) > 0.001)
            {
                var radians = _rotationAngle * Math.PI / 180.0;
                var cos = Math.Cos(radians);
                var sin = Math.Sin(radians);

                // Rotate around the center of the map
                var mapCenterX = (minX + maxX) / 2 * scale;
                var mapCenterY = (minY + maxY) / 2 * scale;

                var relativeX = scaledX - mapCenterX;
                var relativeY = scaledY - mapCenterY;

                scaledX = mapCenterX + (relativeX * cos - relativeY * sin);
                scaledY = mapCenterY + (relativeX * sin + relativeY * cos);
            }

            return new Point(
                scaledX + centerOffsetX + _offsetX,
                scaledY + centerOffsetY + _offsetY
            );
        }

        if (!_skipStaticRender)
        {
            foreach (var map in mapData)
            {
                foreach (var layer in map.Layers)
                {
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
                            Stroke = new SolidColorBrush(Color.FromRgb(0, 191, 255)),
                            StrokeThickness = 2.5
                        };

                        MapCanvas.Children.Add(line);
                    }
                }
            }
        }

        var nodeLookup = allNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Name))
            .GroupBy(node => node.Name)
            .ToDictionary(group => group.Key, group => group.First());

        RemoveVehicleElements();
        RenderMissionPaths(Transform, nodeLookup);

        if (!_skipStaticRender)
        {
            foreach (var map in mapData)
            {
                foreach (var layer in map.Layers)
                {
                    foreach (var node in layer.Nodes)
                    {
                        var pos = Transform(node.X, node.Y);

                        var nodeSize = _nodeSize * _zoomLevel;
                        var glowSize = nodeSize + 4;
                        var strokeWidth = Math.Max(1, 2 * _zoomLevel);

                        var outerGlow = new Ellipse
                        {
                            Width = glowSize,
                            Height = glowSize,
                            Fill = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
                            StrokeThickness = 0
                        };

                        Canvas.SetLeft(outerGlow, pos.X - glowSize / 2);
                        Canvas.SetTop(outerGlow, pos.Y - glowSize / 2);
                        MapCanvas.Children.Add(outerGlow);

                        var nodeEllipse = new Ellipse
                        {
                            Width = nodeSize,
                            Height = nodeSize,
                            Fill = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                            Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                            StrokeThickness = strokeWidth
                        };

                        Canvas.SetLeft(nodeEllipse, pos.X - nodeSize / 2);
                        Canvas.SetTop(nodeEllipse, pos.Y - nodeSize / 2);
                        MapCanvas.Children.Add(nodeEllipse);
                    }
                }
            }
        }

        RenderVehicles(Transform, nodeLookup);

        _skipStaticRender = false;
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom around the cursor position
        var delta = e.Delta > 0 ? ZOOM_FACTOR : -ZOOM_FACTOR;
        var targetZoom = _zoomLevel + delta;
        var anchor = e.GetPosition(MapCanvas);
        ApplyZoom(targetZoom, anchor);
        e.Handled = true;
    }

    #region Zoom Helpers

    private void ApplyZoom(double targetZoom, Point anchor)
    {
        var clampedZoom = Math.Clamp(targetZoom, MIN_ZOOM, MAX_ZOOM);
        if (Math.Abs(clampedZoom - _zoomLevel) <= 0.001)
        {
            return;
        }

        Point? anchorMapPoint = null;
        if (TryGetTransformContext(_zoomLevel, out var beforeContext))
        {
            anchorMapPoint = ScreenToMap(anchor, beforeContext);
        }

        _zoomLevel = clampedZoom;
        OnPropertyChanged(nameof(ZoomLevel));

        var previousOffsetX = _offsetX;
        var previousOffsetY = _offsetY;

        if (anchorMapPoint.HasValue && TryGetTransformContext(_zoomLevel, out var afterContext))
        {
            var projected = MapToScreen(anchorMapPoint.Value, afterContext);
            _offsetX += anchor.X - projected.X;
            _offsetY += anchor.Y - projected.Y;
        }

        if (Math.Abs(_offsetX - previousOffsetX) > 0.001)
        {
            OnPropertyChanged(nameof(OffsetX));
        }

        if (Math.Abs(_offsetY - previousOffsetY) > 0.001)
        {
            OnPropertyChanged(nameof(OffsetY));
        }

        ScheduleRender();
    }

    private bool TryGetTransformContext(double zoomLevel, out MapTransformContext context)
    {
        context = default;

        var mapData = _cachedMapData ?? MapData;
        if (mapData == null || !mapData.Any())
        {
            return false;
        }

        var allNodes = mapData.SelectMany(m => m.Layers.SelectMany(l => l.Nodes)).ToList();
        if (!allNodes.Any())
        {
            return false;
        }

        var minX = allNodes.Min(n => n.X);
        var maxX = allNodes.Max(n => n.X);
        var minY = allNodes.Min(n => n.Y);
        var maxY = allNodes.Max(n => n.Y);

        var mapWidth = maxX - minX;
        var mapHeight = maxY - minY;

        if (mapWidth == 0 || mapHeight == 0)
        {
            return false;
        }

        var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 800;
        var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 600;

        const double padding = 50;
        var scaleX = (canvasWidth - padding * 2) / mapWidth;
        var scaleY = (canvasHeight - padding * 2) / mapHeight;
        var baseScale = Math.Min(scaleX, scaleY);
        var scale = baseScale * zoomLevel;

        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return false;
        }

        var mapCenterX = (minX + maxX) / 2;
        var mapCenterY = (minY + maxY) / 2;

        var mapCenterXScaled = mapCenterX * scale;
        var mapCenterYScaled = mapCenterY * scale;

        var centerOffsetX = canvasWidth / 2 - mapCenterXScaled;
        var centerOffsetY = canvasHeight / 2 - mapCenterYScaled;

        context = new MapTransformContext
        {
            Scale = scale,
            CenterOffsetX = centerOffsetX,
            CenterOffsetY = centerOffsetY,
            MapCenterXScaled = mapCenterXScaled,
            MapCenterYScaled = mapCenterYScaled
        };

        return true;
    }

    private Point? ScreenToMap(Point screenPoint, MapTransformContext context)
    {
        if (context.Scale <= 0)
        {
            return null;
        }

        var scaledX = screenPoint.X - _offsetX - context.CenterOffsetX;
        var scaledY = screenPoint.Y - _offsetY - context.CenterOffsetY;

        if (Math.Abs(_rotationAngle) > 0.001)
        {
            var radians = -_rotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var relativeX = scaledX - context.MapCenterXScaled;
            var relativeY = scaledY - context.MapCenterYScaled;
            var rotatedX = relativeX * cos - relativeY * sin;
            var rotatedY = relativeX * sin + relativeY * cos;
            scaledX = rotatedX + context.MapCenterXScaled;
            scaledY = rotatedY + context.MapCenterYScaled;
        }

        if (_isFlippedHorizontally)
        {
            scaledX = 2 * context.MapCenterXScaled - scaledX;
        }

        var worldX = scaledX / context.Scale;
        var worldY = scaledY / context.Scale;

        if (double.IsNaN(worldX) || double.IsNaN(worldY) || double.IsInfinity(worldX) || double.IsInfinity(worldY))
        {
            return null;
        }

        return new Point(worldX, worldY);
    }

    private Point MapToScreen(Point mapPoint, MapTransformContext context)
    {
        var scaledX = mapPoint.X * context.Scale;
        var scaledY = mapPoint.Y * context.Scale;

        if (_isFlippedHorizontally)
        {
            scaledX = 2 * context.MapCenterXScaled - scaledX;
        }

        if (Math.Abs(_rotationAngle) > 0.001)
        {
            var radians = _rotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var relativeX = scaledX - context.MapCenterXScaled;
            var relativeY = scaledY - context.MapCenterYScaled;
            var rotatedX = relativeX * cos - relativeY * sin;
            var rotatedY = relativeX * sin + relativeY * cos;
            scaledX = rotatedX + context.MapCenterXScaled;
            scaledY = rotatedY + context.MapCenterYScaled;
        }

        return new Point(
            scaledX + context.CenterOffsetX + _offsetX,
            scaledY + context.CenterOffsetY + _offsetY);
    }

    #endregion

    private void VehicleUpdateTimer_Tick(object? sender, EventArgs e)
    {
        _skipStaticRender = true;
        RenderMap();
    }

    private void RemoveVehicleElements()
    {
        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement element && Equals(element.Tag, VehicleElementTag))
            {
                MapCanvas.Children.RemoveAt(i);
            }
        }
    }

    private void RenderMissionPaths(Func<double, double, Point> transform, Dictionary<string, MapNode> nodeLookup)
    {
        if (Vehicles == null)
        {
            return;
        }

        var activeKeys = new HashSet<string>();

        foreach (var vehicle in Vehicles)
        {
            if (vehicle.Path == null || vehicle.Path.Count < 2)
            {
                continue;
            }

            var key = NormalizeVehicleKey(vehicle.Name);
            activeKeys.Add(key);
            var pathBrush = GetVehiclePathBrush(key);
            var pathThickness = Math.Max(2, Math.Min(8, 4 * _zoomLevel));

            for (int i = 0; i < vehicle.Path.Count - 1; i++)
            {
                if (!nodeLookup.TryGetValue(vehicle.Path[i], out var fromNode) ||
                    !nodeLookup.TryGetValue(vehicle.Path[i + 1], out var toNode))
                {
                    continue;
                }

                var p1 = transform(fromNode.X, fromNode.Y);
                var p2 = transform(toNode.X, toNode.Y);

                var pathLine = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = pathBrush,
                    StrokeThickness = pathThickness,
                    Tag = VehicleElementTag
                };

                MapCanvas.Children.Add(pathLine);
            }
        }

        if (_vehiclePathBrushes.Count > 0)
        {
            var keysToRemove = _vehiclePathBrushes.Keys
                .Where(key => !activeKeys.Contains(key))
                .ToList();

            foreach (var removeKey in keysToRemove)
            {
                _vehiclePathBrushes.Remove(removeKey);
            }
        }
    }

    private void RenderVehicles(Func<double, double, Point> transform, Dictionary<string, MapNode> nodeLookup)
    {
        if (Vehicles == null || Vehicles.Count == 0)
        {
            return;
        }

        foreach (var vehicle in Vehicles)
        {
            if (vehicle.VehicleState == "extracted")
            {
                continue;
            }

            if (vehicle.Coordinates == null || vehicle.Coordinates.Count < 2)
            {
                continue;
            }

            var pos = transform(vehicle.Coordinates[0], vehicle.Coordinates[1]);
            var (vehicleColor, _, _) = GetVehicleColors(vehicle);
            var vehicleSize = _vehicleSize * _zoomLevel;
            var strokeWidth = Math.Max(1, _zoomLevel);
            var vehicleWidth = vehicleSize * 0.65;
            var vehicleHeight = vehicleSize * 1.2;

            var vehicleRect = new Rectangle
            {
                Width = vehicleWidth,
                Height = vehicleHeight,
                Fill = new SolidColorBrush(vehicleColor),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                StrokeThickness = strokeWidth,
                Tag = VehicleElementTag
            };

            var heading = ComputeVehicleAngle(vehicle, transform, nodeLookup);
            if (heading.HasValue)
            {
                vehicleRect.RenderTransformOrigin = new Point(0.5, 0.5);
                vehicleRect.RenderTransform = new RotateTransform(NormalizeAngle(heading.Value - 90));
            }

            Canvas.SetLeft(vehicleRect, pos.X - vehicleWidth / 2);
            Canvas.SetTop(vehicleRect, pos.Y - vehicleHeight / 2);
            MapCanvas.Children.Add(vehicleRect);

            var vehicleLabel = new TextBlock
            {
                Text = $"{vehicle.Name}",
                Foreground = new SolidColorBrush(vehicleColor),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(230, 0, 0, 0)),
                Padding = new Thickness(6, 3, 6, 3),
                Tag = VehicleElementTag
            };

            Canvas.SetLeft(vehicleLabel, pos.X + 12);
            Canvas.SetTop(vehicleLabel, pos.Y - 10);
            MapCanvas.Children.Add(vehicleLabel);

            var stateLabel = new TextBlock
            {
                Text = vehicle.VehicleStateText,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 9,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(4, 1, 4, 1),
                Tag = VehicleElementTag
            };

            Canvas.SetLeft(stateLabel, pos.X + 12);
            Canvas.SetTop(stateLabel, pos.Y + 5);
            MapCanvas.Children.Add(stateLabel);
        }
    }

    private static string NormalizeVehicleKey(string vehicleName)
    {
        return string.IsNullOrWhiteSpace(vehicleName) ? "__DEFAULT_VEHICLE__" : vehicleName;
    }

    private SolidColorBrush GetVehiclePathBrush(string vehicleKey)
    {
        if (_vehiclePathBrushes.TryGetValue(vehicleKey, out var cached))
        {
            return cached;
        }

        var color = _vehiclePathPalette[_vehiclePathBrushes.Count % _vehiclePathPalette.Length];
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        _vehiclePathBrushes[vehicleKey] = brush;
        return brush;
    }

    private double? ComputeVehicleAngle(Vehicle vehicle, Func<double, double, Point> transform, Dictionary<string, MapNode> nodeLookup)
    {
        Point? start = null;
        Point? end = null;

        if (vehicle.Coordinates != null && vehicle.Coordinates.Count >= 2)
        {
            start = transform(vehicle.Coordinates[0], vehicle.Coordinates[1]);

            if (!double.IsNaN(vehicle.Course) && !double.IsInfinity(vehicle.Course))
            {
                var dx = Math.Cos(vehicle.Course);
                var dy = Math.Sin(vehicle.Course);
                if (Math.Abs(dx) > 0.0001 || Math.Abs(dy) > 0.0001)
                {
                    end = transform(vehicle.Coordinates[0] + dx, vehicle.Coordinates[1] + dy);
                }
            }
        }

        if ((start == null || end == null) && vehicle.Path != null && vehicle.Path.Count >= 2)
        {
            for (int i = 0; i < vehicle.Path.Count - 1; i++)
            {
                if (!nodeLookup.TryGetValue(vehicle.Path[i], out var fromNode) ||
                    !nodeLookup.TryGetValue(vehicle.Path[i + 1], out var toNode))
                {
                    continue;
                }

                start ??= transform(fromNode.X, fromNode.Y);
                var candidate = transform(toNode.X, toNode.Y);

                if (Math.Abs(candidate.X - start.Value.X) > 0.001 ||
                    Math.Abs(candidate.Y - start.Value.Y) > 0.001)
                {
                    end = candidate;
                    break;
                }
            }
        }

        if (start.HasValue && end.HasValue)
        {
            var dx = end.Value.X - start.Value.X;
            var dy = end.Value.Y - start.Value.Y;

            if (Math.Abs(dx) > 0.0001 || Math.Abs(dy) > 0.0001)
            {
                return Math.Atan2(dy, dx) * 180.0 / Math.PI;
            }
        }

        return null;
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

        // Update offset directly
        _offsetX += delta.X;
        _offsetY += delta.Y;

        _lastMousePosition = currentPosition;

        // Throttle rendering during drag - only render every 33ms (~30 FPS)
        var now = DateTime.Now;
        if ((now - _lastRenderTime).TotalMilliseconds >= 33)
        {
            _lastRenderTime = now;
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            RenderMap();
        }

        e.Handled = true;
    }

    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        MapCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    #region UI Event Handlers

    private void SettingsToggle_Click(object sender, RoutedEventArgs e)
    {
        _isSettingsPanelOpen = !_isSettingsPanelOpen;
        
        if (_isSettingsPanelOpen)
        {
            _originalRotationAngle = _rotationAngle;
            _originalFlipState = _isFlippedHorizontally;
            SettingsPanel.Visibility = Visibility.Visible;
            SettingsToggleButton.Content = "✕";
            SettingsToggleButton.ToolTip = "Close Settings";
        }
        else
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsToggleButton.Content = "⚙️";
            SettingsToggleButton.ToolTip = "Map Settings";
        }
    }

    private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
        {
            // Apply rotation immediately for preview
            RotationAngle = slider.Value;
        }
    }

    private void ResetRotation_Click(object sender, RoutedEventArgs e)
    {
        RotationSlider.Value = 0;
    }

    private async void CompleteSettings_Click(object sender, RoutedEventArgs e)
    {
        // Save the current settings
        await SaveSettingsAsync();
        
        // Close the settings panel
        _isSettingsPanelOpen = false;
        SettingsPanel.Visibility = Visibility.Collapsed;
        SettingsToggleButton.Content = "⚙️";
        SettingsToggleButton.ToolTip = "Map Settings";
        
        // Brief feedback
        var button = sender as Button;
        if (button != null)
        {
            var originalContent = button.Content;
            button.Content = "Saved!";
            button.IsEnabled = false;
            
            await Task.Delay(1000);
            
            button.Content = originalContent;
            button.IsEnabled = true;
        }
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        // Restore original rotation angle and flip state
        RotationAngle = _originalRotationAngle;
        RotationSlider.Value = _originalRotationAngle;
        IsFlippedHorizontally = _originalFlipState;
        FlipCheckBox.IsChecked = _originalFlipState;
        
        // Close the settings panel
        _isSettingsPanelOpen = false;
        SettingsPanel.Visibility = Visibility.Collapsed;
        SettingsToggleButton.Content = "⚙️";
        SettingsToggleButton.ToolTip = "Map Settings";
    }

    private void Rotate90_Click(object sender, RoutedEventArgs e)
    {
        RotationSlider.Value = (_rotationAngle + 90) % 360;
    }

    private void Rotate180_Click(object sender, RoutedEventArgs e)
    {
        RotationSlider.Value = (_rotationAngle + 180) % 360;
    }

    private void Rotate270_Click(object sender, RoutedEventArgs e)
    {
        RotationSlider.Value = (_rotationAngle + 270) % 360;
    }

    private void FlipHorizontal_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            IsFlippedHorizontally = checkBox.IsChecked == true;
        }
    }

    private void ToggleFlip_Click(object sender, RoutedEventArgs e)
    {
        FlipCheckBox.IsChecked = !FlipCheckBox.IsChecked;
        IsFlippedHorizontally = FlipCheckBox.IsChecked == true;
    }

    #endregion

    #region Optimization and Settings Methods

    private void ScheduleRender()
    {
        if (!_needsRender)
        {
            _needsRender = true;
            _renderTimer?.Start();
        }
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        _renderTimer?.Stop();
        if (_needsRender)
        {
            _needsRender = false;
            RenderMap();
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            _offsetX = settings.OffsetX;
            _offsetY = settings.OffsetY;
            _rotationAngle = settings.RotationAngle;
            _zoomLevel = settings.ZoomLevel;
            _isFlippedHorizontally = settings.IsFlippedHorizontally;
            _nodeSize = settings.NodeSize;
            _vehicleSize = settings.VehicleSize;

            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(RotationAngle));
            OnPropertyChanged(nameof(ZoomLevel));
            OnPropertyChanged(nameof(IsFlippedHorizontally));
            OnPropertyChanged(nameof(NodeSize));
            OnPropertyChanged(nameof(VehicleSize));

            ScheduleRender();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load map settings: {ex.Message}");
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new MapSettingsService.MapSettings
            {
                OffsetX = _offsetX,
                OffsetY = _offsetY,
                RotationAngle = _rotationAngle,
                ZoomLevel = _zoomLevel,
                IsFlippedHorizontally = _isFlippedHorizontally,
                NodeSize = _nodeSize,
                VehicleSize = _vehicleSize
            };
            
            await _settingsService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save map settings: {ex.Message}");
        }
    }

    #endregion

    #region Snackbar Methods

    private void ShowNodeSnackbar(string nodeName, Point position)
    {
        HideSnackbar();

        _snackbar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 0, 0, 0)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            Child = new TextBlock
            {
                Text = $"Node: {nodeName}",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            }
        };

        Canvas.SetLeft(_snackbar, Math.Max(0, Math.Min(position.X + 15, MapCanvas.ActualWidth - 150)));
        Canvas.SetTop(_snackbar, Math.Max(0, position.Y - 30));
        Canvas.SetZIndex(_snackbar, 1000);

        MapCanvas.Children.Add(_snackbar);
        
        _snackbarTimer?.Stop();
        _snackbarTimer?.Start();
    }

    private void HideSnackbar()
    {
        if (_snackbar != null)
        {
            MapCanvas.Children.Remove(_snackbar);
            _snackbar = null;
        }
        _snackbarTimer?.Stop();
    }

    private void SnackbarTimer_Tick(object? sender, EventArgs e)
    {
        HideSnackbar();
    }

    private MapNode? GetNodeAtPosition(Point position)
    {
        if (_cachedMapData == null) return null;

        var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 800;
        var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 600;

        var allNodes = _cachedMapData.SelectMany(m => m.Layers.SelectMany(l => l.Nodes)).ToList();
        if (!allNodes.Any()) return null;

        var minX = allNodes.Min(n => n.X);
        var maxX = allNodes.Max(n => n.X);
        var minY = allNodes.Min(n => n.Y);
        var maxY = allNodes.Max(n => n.Y);
        var mapWidth = maxX - minX;
        var mapHeight = maxY - minY;

        if (mapWidth == 0 || mapHeight == 0) return null;

        var padding = 50;
        var scaleX = (canvasWidth - padding * 2) / mapWidth;
        var scaleY = (canvasHeight - padding * 2) / mapHeight;
        var baseScale = Math.Min(scaleX, scaleY);
        var scale = baseScale * _zoomLevel;

        var centerOffsetX = canvasWidth / 2 - (minX + mapWidth / 2) * scale;
        var centerOffsetY = canvasHeight / 2 - (minY + mapHeight / 2) * scale;

        foreach (var node in allNodes)
        {
            // Apply same transform as in rendering
            var scaledX = node.X * scale;
            var scaledY = node.Y * scale;
            
            // Apply horizontal flip if enabled
            if (_isFlippedHorizontally)
            {
                var mapCenterX = (minX + maxX) / 2 * scale;
                scaledX = 2 * mapCenterX - scaledX;
            }
            
            // Apply rotation if any
            if (Math.Abs(_rotationAngle) > 0.001)
            {
                var radians = _rotationAngle * Math.PI / 180.0;
                var cos = Math.Cos(radians);
                var sin = Math.Sin(radians);
                
                var mapCenterX = (minX + maxX) / 2 * scale;
                var mapCenterY = (minY + maxY) / 2 * scale;
                
                var relativeX = scaledX - mapCenterX;
                var relativeY = scaledY - mapCenterY;
                
                scaledX = mapCenterX + (relativeX * cos - relativeY * sin);
                scaledY = mapCenterY + (relativeX * sin + relativeY * cos);
            }
            
            var nodeX = scaledX + centerOffsetX + _offsetX;
            var nodeY = scaledY + centerOffsetY + _offsetY;

            var distance = Math.Sqrt(Math.Pow(position.X - nodeX, 2) + Math.Pow(position.Y - nodeY, 2));
            if (distance <= 15) // Node hit radius
            {
                return node;
            }
        }

        return null;
    }

    #endregion

    #region Mouse Event Overrides

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        
        if (!_isDragging)
        {
            var position = e.GetPosition(MapCanvas);
            var hitNode = GetNodeAtPosition(position);
            
            if (hitNode != null && !string.IsNullOrEmpty(hitNode.Name) && hitNode.Name != _hoveredNodeName)
            {
                _hoveredNodeName = hitNode.Name;
                ShowNodeSnackbar(hitNode.Name, position);
            }
            else if (hitNode == null && _hoveredNodeName != null)
            {
                _hoveredNodeName = null;
                HideSnackbar();
            }
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredNodeName = null;
        HideSnackbar();
    }

    #endregion

    private void MapView_Unloaded(object sender, RoutedEventArgs e)
    {
        _ = SaveSettingsAsync();
        _renderTimer?.Stop();
        _snackbarTimer?.Stop();
        _vehicleUpdateTimer?.Stop();
    }

    private (Color vehicleColor, Color glowColor, string colorName) GetVehicleColors(Vehicle vehicle)
    {
        return vehicle.VehicleState.ToLower() switch
        {
            "runningamission" => (Color.FromRgb(255, 165, 0), Color.FromArgb(100, 255, 165, 0), "orange (mission)"),
            "charging" => (Color.FromRgb(0, 191, 255), Color.FromArgb(100, 0, 191, 255), "cyan (charging)"),
            "parking" => (Color.FromRgb(128, 128, 128), Color.FromArgb(100, 128, 128, 128), "gray (parking)"),
            "movingtonode" => (Color.FromRgb(255, 255, 0), Color.FromArgb(100, 255, 255, 0), "yellow (moving)"),
            _ => (Color.FromRgb(0, 255, 0), Color.FromArgb(100, 0, 255, 0), "default green")
        };
    }

    private double NormalizeAngle(double angle)
    {
        return (angle % 360 + 360) % 360;
    }
}
