using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private const string LinkElementTag = "__LinkLayer";
    private const string NodeElementTag = "__NodeMarker";
    private const string NodeGlowElementTag = "__NodeGlow";
    private const double BaseLinkStrokeThickness = 2.5;
    private const double BaseNodeStrokeThickness = 1.6;
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
    private double _lastStyleZoom = double.NaN;
    private TextBlock _debugMessage;
    
    // Transform fields for viewport manipulation
    private readonly Canvas _drawingSurface = new Canvas();
    private readonly ScaleTransform _scaleTransform = new ScaleTransform();
    private readonly RotateTransform _rotateTransform = new RotateTransform();
    private readonly TranslateTransform _translateTransform = new TranslateTransform();
    private readonly TransformGroup _transformGroup = new TransformGroup();
    
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
                UpdateViewTransform();
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
                UpdateViewTransform();
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
                UpdateViewTransform();
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
                UpdateViewTransform();
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
                UpdateViewTransform();
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

    private void UpdateViewTransform()
    {
        // The center of rotation/scaling is the center of the viewport.
        var centerX = MapCanvas.ActualWidth / 2;
        var centerY = MapCanvas.ActualHeight / 2;

        _scaleTransform.CenterX = centerX;
        _scaleTransform.CenterY = centerY;
        _rotateTransform.CenterX = centerX;
        _rotateTransform.CenterY = centerY;

        _scaleTransform.ScaleX = _zoomLevel * (_isFlippedHorizontally ? -1 : 1);
        _scaleTransform.ScaleY = _zoomLevel;
        _rotateTransform.Angle = _rotationAngle;
        _translateTransform.X = _offsetX;
        _translateTransform.Y = _offsetY;

        UpdateStaticElementAppearance();
    }

    public ICommand ResetViewCommand { get; }

    public MapView()
    {
        InitializeComponent();
        
        // Setup viewport model
        _transformGroup.Children.Add(_scaleTransform);
        _transformGroup.Children.Add(_rotateTransform);
        _transformGroup.Children.Add(_translateTransform);
        _drawingSurface.RenderTransform = _transformGroup;
        MapCanvas.Children.Add(_drawingSurface);
        
        // Bind the drawing surface size to the parent canvas size
        _drawingSurface.SetBinding(WidthProperty, new Binding("ActualWidth") { Source = MapCanvas });
        _drawingSurface.SetBinding(HeightProperty, new Binding("ActualHeight") { Source = MapCanvas });
        
        // Initialize and add the debug message TextBlock
        _debugMessage = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(_debugMessage, 100);
        Canvas.SetTop(_debugMessage, 100);
        MapCanvas.Children.Add(_debugMessage);
        
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
        // In the new model, we must update the transforms after resetting state.
        UpdateViewTransform();
        RenderMap();
    }

    private void RenderMap()
    {
        var mapData = _cachedMapData ?? MapData;

        if (mapData == null || !mapData.Any())
        {
            _drawingSurface.Visibility = Visibility.Collapsed;
            _debugMessage.Text = mapData == null ? "Waiting for map data..." : "No nodes in map data!";
            _debugMessage.Visibility = Visibility.Visible;
            return;
        }
        
        _drawingSurface.Visibility = Visibility.Visible;
        _debugMessage.Visibility = Visibility.Collapsed;
        
        if (!_skipStaticRender)
        {
            _drawingSurface.Children.Clear();
            _cachedElements.Clear();
            _lastStyleZoom = double.NaN;
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
        var scale = baseScale;

        // Center offset
        var centerOffsetX = canvasWidth / 2 - (minX + mapWidth / 2) * scale;
        var centerOffsetY = canvasHeight / 2 - (minY + mapHeight / 2) * scale;

        // Transform helper with rotation and flip
        Point Transform(double x, double y)
        {
            var scaledX = x * scale;
            var scaledY = y * scale;

            return new Point(
                scaledX + centerOffsetX,
                scaledY + centerOffsetY
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
                            StrokeThickness = BaseLinkStrokeThickness,
                            Tag = LinkElementTag
                        };

                        _drawingSurface.Children.Add(line);
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

                        var nodeSize = _nodeSize;
                        var glowSize = nodeSize + 4;
                        var strokeWidth = BaseNodeStrokeThickness;

                        var outerGlow = new Ellipse
                        {
                            Width = glowSize,
                            Height = glowSize,
                            Fill = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
                            StrokeThickness = 0,
                            Tag = NodeGlowElementTag,
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new ScaleTransform(1.0, 1.0)
                        };

                        Canvas.SetLeft(outerGlow, pos.X - glowSize / 2);
                        Canvas.SetTop(outerGlow, pos.Y - glowSize / 2);
                        _drawingSurface.Children.Add(outerGlow);

                        var nodeEllipse = new Ellipse
                        {
                            Width = nodeSize,
                            Height = nodeSize,
                            Fill = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                            Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                            StrokeThickness = strokeWidth,
                            Tag = NodeElementTag,
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new ScaleTransform(1.0, 1.0)
                        };

                        Canvas.SetLeft(nodeEllipse, pos.X - nodeSize / 2);
                        Canvas.SetTop(nodeEllipse, pos.Y - nodeSize / 2);
                        _drawingSurface.Children.Add(nodeEllipse);
                    }
                }
            }
        }

        RenderVehicles(Transform, nodeLookup);

        UpdateStaticElementAppearance();

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

        UpdateViewTransform();
        QueueDynamicRefresh();
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
        for (int i = _drawingSurface.Children.Count - 1; i >= 0; i--)
        {
            if (_drawingSurface.Children[i] is FrameworkElement element && Equals(element.Tag, VehicleElementTag))
            {
                _drawingSurface.Children.RemoveAt(i);
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
            var effectiveZoom = Math.Max(_zoomLevel, MIN_ZOOM);
            var pathThickness = Math.Clamp(4.0 / Math.Pow(effectiveZoom, 0.8), 1.5, 6.0);

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

                _drawingSurface.Children.Add(pathLine);
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
            var effectiveZoom = Math.Max(_zoomLevel, MIN_ZOOM);
            var baseVehicleSize = _vehicleSize;
            var vehicleWidth = baseVehicleSize * 0.65 / effectiveZoom;
            var vehicleHeight = baseVehicleSize * 1.2 / effectiveZoom;
            var strokeWidth = 1.25 / effectiveZoom;

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
            _drawingSurface.Children.Add(vehicleRect);

            var vehicleLabel = new TextBlock
            {
                Text = $"{vehicle.Name}",
                Foreground = new SolidColorBrush(vehicleColor),
                FontSize = 12 / effectiveZoom,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(230, 0, 0, 0)),
                Padding = new Thickness(6 / effectiveZoom, 3 / effectiveZoom, 6 / effectiveZoom, 3 / effectiveZoom),
                Tag = VehicleElementTag
            };

            Canvas.SetLeft(vehicleLabel, pos.X + 12 / effectiveZoom);
            Canvas.SetTop(vehicleLabel, pos.Y - 10 / effectiveZoom);
            _drawingSurface.Children.Add(vehicleLabel);

            var stateLabel = new TextBlock
            {
                Text = vehicle.VehicleStateText,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 9 / effectiveZoom,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(4 / effectiveZoom, 1 / effectiveZoom, 4 / effectiveZoom, 1 / effectiveZoom),
                Tag = VehicleElementTag
            };

            Canvas.SetLeft(stateLabel, pos.X + 12 / effectiveZoom);
            Canvas.SetTop(stateLabel, pos.Y + 5 / effectiveZoom);
            _drawingSurface.Children.Add(stateLabel);
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
        if (e.Handled) return;
        _isDragging = true;
        _vehicleUpdateTimer?.Stop(); // Pause vehicle updates during drag
        _lastMousePosition = e.GetPosition(this);
        MapCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // This event handler is now superseded by the logic in the OnMouseMove override.
        // The override provides a more reliable event sequence.
        // We leave this handler attached (from XAML) but keep it empty.
    }

    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        MapCanvas.ReleaseMouseCapture();
        _vehicleUpdateTimer?.Start(); // Resume vehicle updates
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

    private void UpdateStaticElementAppearance()
    {
        if (_drawingSurface.Children.Count == 0)
        {
            return;
        }

        var effectiveZoom = Math.Max(_zoomLevel, MIN_ZOOM);

        if (!double.IsNaN(_lastStyleZoom) && Math.Abs(_lastStyleZoom - effectiveZoom) < 0.001)
        {
            return;
        }

        _lastStyleZoom = effectiveZoom;
        var linkThicknessFactor = Math.Clamp(1.0 / Math.Pow(effectiveZoom, 1.1), 0.35, 2.0);
        var nodeScaleFactor = Math.Clamp(1.0 / Math.Pow(effectiveZoom, 1.2), 0.35, 12.0);
        var glowScaleFactor = Math.Clamp(1.0 / Math.Pow(effectiveZoom, 1.1), 0.35, 12.0);
        var nodeStrokeFactor = Math.Clamp(Math.Pow(effectiveZoom, -1.1), 0.45, 1.5);

        foreach (var child in _drawingSurface.Children)
        {
            switch (child)
            {
                case Line line when Equals(line.Tag, LinkElementTag):
                    line.StrokeThickness = BaseLinkStrokeThickness * linkThicknessFactor;
                    break;
                case Ellipse ellipse when Equals(ellipse.Tag, NodeElementTag):
                    ApplyScaleTransform(ellipse, nodeScaleFactor);
                    ellipse.StrokeThickness = BaseNodeStrokeThickness * nodeStrokeFactor;
                    break;
                case Ellipse ellipse when Equals(ellipse.Tag, NodeGlowElementTag):
                    ApplyScaleTransform(ellipse, glowScaleFactor);
                    break;
            }
        }
    }

    private static void ApplyScaleTransform(UIElement element, double scale)
    {
        if (element.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1.0, 1.0);
            element.RenderTransform = scaleTransform;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        scaleTransform.ScaleX = scale;
        scaleTransform.ScaleY = scale;
    }

    private void ScheduleRender()
    {
        if (!_needsRender)
        {
            _needsRender = true;
            _renderTimer?.Start();
        }
    }

    private void QueueDynamicRefresh()
    {
        if (_needsRender)
        {
            return;
        }

        _skipStaticRender = true;
        _needsRender = true;
        _renderTimer?.Start();
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

        // Get the inverse of the total view transform
        var viewTransform = _drawingSurface.RenderTransform.Value;
        if (!viewTransform.HasInverse) return null;
        viewTransform.Invert();

        // Transform mouse position from screen space to the untransformed "drawing" space
        Point drawingPoint = viewTransform.Transform(position);

        // Now, we need to invert the base transformation (scaling and centering) 
        // that was applied in RenderMap to get back to original map coordinates.
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
        if (baseScale <= 0) return null;

        var centerOffsetX = canvasWidth / 2 - (minX + mapWidth / 2) * baseScale;
        var centerOffsetY = canvasHeight / 2 - (minY + mapHeight / 2) * baseScale;

        // Convert drawingPoint to original map coordinates
        var mapX = (drawingPoint.X - centerOffsetX) / baseScale;
        var mapY = (drawingPoint.Y - centerOffsetY) / baseScale;

        // Find the closest node in map coordinates, using a fixed pixel radius
        const double hitRadiusInPixels = 15.0;
        // Convert the pixel radius to map units by considering the total scale
        var totalScale = baseScale * _zoomLevel;
        var hitRadiusInMapUnits = hitRadiusInPixels / totalScale;

        MapNode? closestNode = null;
        var minDistanceSq = hitRadiusInMapUnits * hitRadiusInMapUnits;

        foreach (var node in allNodes)
        {
            var dx = node.X - mapX;
            var dy = node.Y - mapY;
            var distSq = dx * dx + dy * dy;
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                closestNode = node;
            }
        }

        return closestNode;
    }

    #endregion

    #region Mouse Event Overrides

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging)
        {
            // Panning logic
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _lastMousePosition;
            _lastMousePosition = currentPosition;

            _offsetX += delta.X;
            _offsetY += delta.Y;

            // Update the view transform directly
            UpdateViewTransform();
            
            e.Handled = true;
        }
        else
        {
            // Hover logic (existing logic)
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
