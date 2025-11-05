using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private double _vehicleAngleOffset = 0;
    private double _vehicleLabelScale = 1.0;
    private bool _areVehiclesFlipped = false;
    private DispatcherTimer? _vehicleUpdateTimer;
    private const string VehicleVisualTag = "__VehicleVisual";
    private const string VehiclePathElementTag = "__VehiclePath";
    private const string LinkElementTag = "__LinkLayer";
    private const string NodeElementTag = "__NodeMarker";
    private const string NodeGlowElementTag = "__NodeGlow";
    private const double BaseLinkStrokeThickness = 2.5;
    private const double BaseNodeStrokeThickness = 1.6;
    private readonly Dictionary<string, VehicleVisual> _vehicleVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SolidColorBrush> _vehiclePathBrushes = new();
    private readonly Color[] _vehiclePathPalette =
    {
        Color.FromRgb(255, 45, 85),   // Primary Red
        Color.FromRgb(255, 193, 7),   // Amber
        Color.FromRgb(52, 199, 89),   // Rich Green
        Color.FromRgb(255, 140, 0),   // Dark Orange
        Color.FromRgb(30, 144, 255),  // Dodger Blue
        Color.FromRgb(186, 85, 211),  // Medium Orchid
        Color.FromRgb(255, 214, 10),  // Bright Yellow
        Color.FromRgb(65, 105, 225),  // Royal Blue
        Color.FromRgb(255, 99, 71),   // Tomato
        Color.FromRgb(60, 179, 113),  // Medium Sea Green
        Color.FromRgb(70, 130, 180),   // Steel Blue
    };
    private readonly Dictionary<string, TargetHighlightElements> _targetHighlightElements = new(StringComparer.OrdinalIgnoreCase);
    private bool _skipStaticRender = false;
    private static readonly Regex ShapeNumberRegex = new(@"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private struct MapTransformContext
    {
        public double Scale;
        public double CenterOffsetX;
        public double CenterOffsetY;
        public double MapCenterXScaled;
        public double MapCenterYScaled;
    }

    private sealed class PathSegmentRenderInfo
    {
        public Point P1 { get; init; }
        public Point P2 { get; init; }
        public SolidColorBrush Brush { get; init; } = null!;
        public double BaseThickness { get; init; }
    }

    private sealed class VehicleShapeData
    {
        public VehicleShapeData(List<Point> normalizedPoints, double width, double height)
        {
            NormalizedPoints = normalizedPoints;
            Width = width;
            Height = height;
        }

        public IReadOnlyList<Point> NormalizedPoints { get; }
        public double Width { get; }
        public double Height { get; }
    }

    private sealed class VehicleVisual
    {
        public Shape BodyElement { get; init; } = null!;
        public Border LabelContainer { get; init; } = null!;
        public TextBlock NameText { get; init; } = null!;
        public TextBlock StateText { get; init; } = null!;
        public TextBlock AlarmText { get; init; } = null!;
        public VehicleShapeData? ShapeData { get; init; }
        public string ShapeSignature { get; init; } = string.Empty;
        public Ellipse? AlarmGlow { get; set; }
    }

    private sealed class TargetHighlightElements
    {
        public TargetHighlightElements(Ellipse glow, Ellipse core)
        {
            Glow = glow;
            Core = core;
        }

        public Ellipse Glow { get; }
        public Ellipse Core { get; }
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
    private double _originalVehicleAngleOffset = 0;
    private bool _originalVehicleFlipState = false;

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
            var newValue = Math.Clamp(value, 1, 20);
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
        get => _vehicleSize / 20.0;
        set
        {
            var newActualSize = Math.Clamp(value * 20.0, 20, 200);
            if (Math.Abs(_vehicleSize - newActualSize) > 0.001)
            {
                _vehicleSize = newActualSize;
                OnPropertyChanged();
                ScheduleRender();
            }
        }
    }

    public double VehicleAngleOffset
    {
        get => _vehicleAngleOffset;
        set
        {
            var newValue = Math.Clamp(value, -180, 180);
            if (Math.Abs(_vehicleAngleOffset - newValue) > 0.001)
            {
                _vehicleAngleOffset = newValue;
                OnPropertyChanged();
                QueueDynamicRefresh();
            }
        }
    }

    public bool AreVehiclesFlipped
    {
        get => _areVehiclesFlipped;
        set
        {
            if (_areVehiclesFlipped != value)
            {
                _areVehiclesFlipped = value;
                OnPropertyChanged();
                QueueDynamicRefresh();
            }
        }
    }

    public double VehicleLabelScale
    {
        get => _vehicleLabelScale;
        set
        {
            var newValue = Math.Clamp(value, 0.3, 2.5);
            if (Math.Abs(_vehicleLabelScale - newValue) > 0.001)
            {
                _vehicleLabelScale = newValue;
                OnPropertyChanged();
                QueueDynamicRefresh();
                RefreshVehicleLabelTransforms();
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
        RefreshVehicleLabelTransforms();
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
        UpdateViewTransform();
        ScheduleRender();
    }

    private void MapView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"MapView SizeChanged - Canvas Size: {MapCanvas.ActualWidth}x{MapCanvas.ActualHeight}");
        UpdateViewTransform();
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

    private HashSet<string> GetCurrentTargetNodes()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Vehicles == null)
        {
            return result;
        }

        foreach (var vehicle in Vehicles)
        {
            if (vehicle == null)
            {
                continue;
            }

            var targetName = !string.IsNullOrWhiteSpace(vehicle.TargetNode)
                ? vehicle.TargetNode.Trim()
                : (vehicle.Path != null && vehicle.Path.Count > 0
                    ? vehicle.Path[vehicle.Path.Count - 1]
                    : string.Empty)?.Trim();

            if (string.IsNullOrWhiteSpace(targetName) ||
                string.Equals(targetName, "없음", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(targetName);
        }

        return result;
    }

    private static DoubleAnimation CreateTargetNodeBlinkAnimation()
    {
        return new DoubleAnimation
        {
            From = 0.45,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromSeconds(1.6)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
    }

    private static DoubleAnimation CreateTargetNodeScaleAnimation(double from, double to)
    {
        return new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromSeconds(1.6)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
    }

    private void RemoveTargetHighlight(string key)
    {
        if (!_targetHighlightElements.TryGetValue(key, out var highlight))
        {
            return;
        }

        highlight.Glow.BeginAnimation(UIElement.OpacityProperty, null);
        highlight.Core.BeginAnimation(UIElement.OpacityProperty, null);

        if (highlight.Glow.RenderTransform is ScaleTransform glowScale)
        {
            glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        if (highlight.Core.RenderTransform is ScaleTransform coreScale)
        {
            coreScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            coreScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        _drawingSurface.Children.Remove(highlight.Glow);
        _drawingSurface.Children.Remove(highlight.Core);

        _targetHighlightElements.Remove(key);
    }

    private void ClearTargetHighlights()
    {
        if (_targetHighlightElements.Count == 0)
        {
            return;
        }

        foreach (var key in _targetHighlightElements.Keys.ToList())
        {
            RemoveTargetHighlight(key);
        }
    }

    private void UpdateTargetHighlights(HashSet<string> targetNodeNames, Dictionary<string, MapNode> nodeLookup, Func<double, double, Point> transform)
    {
        if (targetNodeNames.Count == 0 || nodeLookup.Count == 0)
        {
            ClearTargetHighlights();
            return;
        }

        var effectiveZoom = Math.Max(_zoomLevel, MIN_ZOOM);
        var nodeScaleFactor = Math.Clamp(1.0 / Math.Pow(effectiveZoom, 1.2), 0.35, 12.0);
        var baseVisualSize = Math.Max(_nodeSize * nodeScaleFactor, 1.5);
        var coreSize = Math.Max(baseVisualSize * 0.75, _nodeSize * 1.5);
        var glowSize = Math.Max(baseVisualSize * 1.0, _nodeSize * 2.5);

        // Remove any highlights that are no longer needed
        var keysToRemove = _targetHighlightElements.Keys
            .Where(key => !targetNodeNames.Contains(key))
            .ToList();

        foreach (var key in keysToRemove)
        {
            RemoveTargetHighlight(key);
        }

        foreach (var targetName in targetNodeNames)
        {
            if (!nodeLookup.TryGetValue(targetName, out var mapNode))
            {
                continue;
            }

            var position = transform(mapNode.X, mapNode.Y);
            var isNewHighlight = false;

            if (!_targetHighlightElements.TryGetValue(targetName, out var elements))
            {
                var glow = new Ellipse
                {
                    Width = glowSize,
                    Height = glowSize,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 255, 82, 82)),
                    StrokeThickness = 0,
                    Opacity = 0.75,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1.0, 1.0)
                };
                Panel.SetZIndex(glow, 2000);

                var core = new Ellipse
                {
                    Width = coreSize,
                    Height = coreSize,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 82, 82)),
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 234, 234)),
                    StrokeThickness = BaseNodeStrokeThickness,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1.0, 1.0)
                };
                Panel.SetZIndex(core, 2001);

                _drawingSurface.Children.Add(glow);
                _drawingSurface.Children.Add(core);

                elements = new TargetHighlightElements(glow, core);
                _targetHighlightElements[targetName] = elements;
                isNewHighlight = true;
            }

            elements.Glow.Width = glowSize;
            elements.Glow.Height = glowSize;
            elements.Core.Width = coreSize;
            elements.Core.Height = coreSize;

            Canvas.SetLeft(elements.Glow, position.X - glowSize / 2);
            Canvas.SetTop(elements.Glow, position.Y - glowSize / 2);
            Canvas.SetLeft(elements.Core, position.X - coreSize / 2);
            Canvas.SetTop(elements.Core, position.Y - coreSize / 2);

            if (isNewHighlight)
            {
                var glowBlink = CreateTargetNodeBlinkAnimation();
                elements.Glow.BeginAnimation(UIElement.OpacityProperty, glowBlink);
                if (elements.Glow.RenderTransform is ScaleTransform glowTransform)
                {
                    var glowScaleAnimation = CreateTargetNodeScaleAnimation(0.88, 1.32);
                    glowTransform.BeginAnimation(ScaleTransform.ScaleXProperty, glowScaleAnimation);
                    glowTransform.BeginAnimation(ScaleTransform.ScaleYProperty, glowScaleAnimation);
                }

                var coreBlink = CreateTargetNodeBlinkAnimation();
                elements.Core.BeginAnimation(UIElement.OpacityProperty, coreBlink);
                if (elements.Core.RenderTransform is ScaleTransform coreTransform)
                {
                    var coreScaleAnimation = CreateTargetNodeScaleAnimation(0.9, 1.18);
                    coreTransform.BeginAnimation(ScaleTransform.ScaleXProperty, coreScaleAnimation);
                    coreTransform.BeginAnimation(ScaleTransform.ScaleYProperty, coreScaleAnimation);
                }
            }
        }
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
            _vehicleVisuals.Clear();
            ClearTargetHighlights();
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

        var targetNodeNames = GetCurrentTargetNodes();
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
            .GroupBy(node => node.Name?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

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

        UpdateTargetHighlights(targetNodeNames, nodeLookup, Transform);

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
            if (_drawingSurface.Children[i] is Line element && Equals(element.Tag, VehiclePathElementTag))
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
        var segmentGroups = new Dictionary<string, List<PathSegmentRenderInfo>>(StringComparer.Ordinal);

        foreach (var vehicle in Vehicles)
        {
            if (vehicle.Path == null || vehicle.Path.Count < 2)
            {
                continue;
            }

            var vehicleKey = NormalizeVehicleKey(vehicle.Name);
            activeKeys.Add(vehicleKey);
            var pathBrush = GetVehiclePathBrush(vehicleKey);
            var effectiveZoom = Math.Max(_zoomLevel, MIN_ZOOM);
            var baseThickness = Math.Clamp(4.0 / Math.Pow(effectiveZoom, 0.8), 1.5, 6.0);

            for (int i = 0; i < vehicle.Path.Count - 1; i++)
            {
                var fromId = vehicle.Path[i];
                var toId = vehicle.Path[i + 1];

                if (!nodeLookup.TryGetValue(fromId, out var fromNode) ||
                    !nodeLookup.TryGetValue(toId, out var toNode))
                {
                    continue;
                }

                var p1 = transform(fromNode.X, fromNode.Y);
                var p2 = transform(toNode.X, toNode.Y);

                if (ArePointsClose(p1, p2))
                {
                    continue;
                }

                var segmentKey = BuildSegmentKey(fromId, toId);
                if (string.IsNullOrEmpty(segmentKey))
                {
                    continue;
                }

                if (!segmentGroups.TryGetValue(segmentKey, out var list))
                {
                    list = new List<PathSegmentRenderInfo>();
                    segmentGroups[segmentKey] = list;
                }

                list.Add(new PathSegmentRenderInfo
                {
                    P1 = p1,
                    P2 = p2,
                    Brush = pathBrush,
                    BaseThickness = baseThickness
                });
            }
        }

        foreach (var group in segmentGroups.Values)
        {
            DrawSegmentGroup(group);
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

    private static string BuildSegmentKey(string fromNodeName, string toNodeName)
    {
        if (string.IsNullOrWhiteSpace(fromNodeName) || string.IsNullOrWhiteSpace(toNodeName))
        {
            return string.Empty;
        }

        return string.CompareOrdinal(fromNodeName, toNodeName) <= 0
            ? $"{fromNodeName}|{toNodeName}"
            : $"{toNodeName}|{fromNodeName}";
    }

    private static bool ArePointsClose(Point a, Point b, double tolerance = 0.01)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy <= tolerance * tolerance;
    }

    private void DrawSegmentGroup(List<PathSegmentRenderInfo> group)
    {
        if (group.Count == 0)
        {
            return;
        }

        var reference = group[0];
        var dx = reference.P2.X - reference.P1.X;
        var dy = reference.P2.Y - reference.P1.Y;
        var baseVector = new Vector(dx, dy);

        if (baseVector.LengthSquared < 0.001)
        {
            return;
        }

        baseVector.Normalize();
        var normal = new Vector(-baseVector.Y, baseVector.X);

        if (normal.LengthSquared < 0.0001)
        {
            return;
        }

        var count = group.Count;
        var baseThickness = group.Max(info => info.BaseThickness);
        var perLineThickness = count > 1 ? Math.Max(baseThickness / count, 1.0) : baseThickness;

        for (var index = 0; index < count; index++)
        {
            var info = group[index];
            var offsetFactor = index - (count - 1) / 2.0;
            var offsetVector = normal * (offsetFactor * perLineThickness);

            var line = new Line
            {
                X1 = info.P1.X + offsetVector.X,
                Y1 = info.P1.Y + offsetVector.Y,
                X2 = info.P2.X + offsetVector.X,
                Y2 = info.P2.Y + offsetVector.Y,
                Stroke = info.Brush,
                StrokeThickness = perLineThickness,
                Tag = VehiclePathElementTag
            };

            _drawingSurface.Children.Add(line);
        }
    }

    private void RenderVehicles(Func<double, double, Point> transform, Dictionary<string, MapNode> nodeLookup)
    {
        var shouldAnimate = _skipStaticRender;
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);

        if (Vehicles == null || Vehicles.Count == 0)
        {
            RemoveInactiveVehicleVisuals(activeKeys);
            return;
        }

        foreach (var vehicle in Vehicles)
        {
            if (vehicle.VehicleState == "extracted" || vehicle.Coordinates == null || vehicle.Coordinates.Count < 2)
            {
                continue;
            }

            var vehicleKey = NormalizeVehicleKey(vehicle.Name);
            activeKeys.Add(vehicleKey);

            var pathBrush = GetVehiclePathBrush(vehicleKey);
            var pathColor = pathBrush.Color;
            var pos = transform(vehicle.Coordinates[0], vehicle.Coordinates[1]);
            var (vehicleColor, _, _) = GetVehicleColors(vehicle);
            var zoomScale = Math.Max(Math.Abs(_scaleTransform.ScaleX), MIN_ZOOM);
            var shapeSignature = BuildVehicleShapeSignature(vehicle);

            if (_vehicleVisuals.TryGetValue(vehicleKey, out var cachedVisual) &&
                !string.Equals(cachedVisual.ShapeSignature, shapeSignature, StringComparison.Ordinal))
            {
                RemoveVehicleVisual(cachedVisual);
                _vehicleVisuals.Remove(vehicleKey);
                cachedVisual = null;
            }

            var isNewVisual = cachedVisual == null;
            if (isNewVisual)
            {
                cachedVisual = CreateVehicleVisual(vehicle, vehicleColor, 1.25, pathColor, shapeSignature);
                _vehicleVisuals[vehicleKey] = cachedVisual;
                _drawingSurface.Children.Add(cachedVisual.BodyElement);
                _drawingSurface.Children.Add(cachedVisual.LabelContainer);
            }

            var visual = cachedVisual!;

            UpdateVehicleVisual(
                vehicle,
                visual,
                pos,
                transform,
                nodeLookup,
                pathColor,
                vehicleColor,
                zoomScale,
                shouldAnimate && !isNewVisual);
        }

        RemoveInactiveVehicleVisuals(activeKeys);
    }

    private VehicleVisual CreateVehicleVisual(
        Vehicle vehicle,
        Color vehicleColor,
        double strokeWidth,
        Color pathColor,
        string shapeSignature)
    {
        var shapeData = TryCreateVehicleShapeData(vehicle);
        Shape bodyElement;

        if (shapeData != null)
        {
            bodyElement = new Path
            {
                Fill = new SolidColorBrush(pathColor),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                StrokeThickness = strokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                Tag = VehicleVisualTag,
                SnapsToDevicePixels = true
            };
        }
        else
        {
            bodyElement = new Rectangle
            {
                Fill = new SolidColorBrush(pathColor),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                StrokeThickness = strokeWidth,
                RadiusX = 6,
                RadiusY = 6,
                Tag = VehicleVisualTag
            };
        }

        bodyElement.RenderTransformOrigin = new Point(0.5, 0.5);

        var nameText = new TextBlock
        {
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 9.5,
            Tag = VehicleVisualTag
        };

        var stateText = new TextBlock
        {
            Foreground = new SolidColorBrush(vehicleColor),
            FontSize = 8.5,
            Opacity = 0.9,
            Tag = VehicleVisualTag
        };

        var alarmText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 50)),
            FontSize = 7.5,
            FontWeight = FontWeights.Bold,
            Opacity = 0.95,
            Tag = VehicleVisualTag,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.NoWrap
        };

        var labelStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Tag = VehicleVisualTag
        };
        labelStack.Children.Add(nameText);
        stateText.Margin = new Thickness(0, 2, 0, 0);
        labelStack.Children.Add(stateText);
        alarmText.Margin = new Thickness(0, 2, 0, 0);
        labelStack.Children.Add(alarmText);

        var labelContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(160, 20, 20, 20)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            Child = labelStack,
            Tag = VehicleVisualTag,
            SnapsToDevicePixels = true
        };
        Canvas.SetZIndex(bodyElement, 900);
        Canvas.SetZIndex(labelContainer, 901);

        return new VehicleVisual
        {
            BodyElement = bodyElement,
            LabelContainer = labelContainer,
            NameText = nameText,
            StateText = stateText,
            AlarmText = alarmText,
            ShapeData = shapeData,
            ShapeSignature = shapeSignature
        };
    }

    private void UpdateVehicleVisual(
        Vehicle vehicle,
        VehicleVisual visual,
        Point bodyCenter,
        Func<double, double, Point> transform,
        Dictionary<string, MapNode> nodeLookup,
        Color pathColor,
        Color stateColor,
        double zoomScale,
        bool animate)
    {
        var desiredWidth = _vehicleSize * 0.68;
        var desiredHeight = _vehicleSize * 1.1;
        var bodyWidth = desiredWidth;
        var bodyHeight = desiredHeight;

        var bodyElement = visual.BodyElement;

        if (bodyElement.Fill is SolidColorBrush bodyFill)
        {
            bodyFill.Color = pathColor;
        }
        else
        {
            bodyElement.Fill = new SolidColorBrush(pathColor);
        }

        if (bodyElement.Stroke is SolidColorBrush strokeBrush)
        {
            strokeBrush.Color = Color.FromRgb(255, 255, 255);
        }
        else
        {
            bodyElement.Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        }

        if (visual.ShapeData != null && bodyElement is Path pathShape)
        {
            var actualSize = UpdateVehicleShapePath(pathShape, visual.ShapeData, desiredWidth, desiredHeight);
            bodyWidth = actualSize.Width;
            bodyHeight = actualSize.Height;
            bodyElement.Width = bodyWidth;
            bodyElement.Height = bodyHeight;
        }
        else if (bodyElement is Rectangle rectangle)
        {
            rectangle.Width = desiredWidth;
            rectangle.Height = desiredHeight;
            rectangle.RadiusX = Math.Max(2, desiredWidth * 0.12);
            rectangle.RadiusY = Math.Max(2, desiredHeight * 0.12);
            bodyWidth = desiredWidth;
            bodyHeight = desiredHeight;
        }
        else
        {
            bodyElement.Width = desiredWidth;
            bodyElement.Height = desiredHeight;
        }

        var strokeThickness = Math.Clamp(bodyWidth * 0.02, 0.3, 0.6);
        bodyElement.StrokeThickness = strokeThickness;

        var heading = ComputeVehicleAngle(vehicle, transform, nodeLookup);
        var rawAngle = heading.HasValue ? NormalizeAngle(heading.Value - 90) : 0;
        var adjustedAngle = rawAngle;
        if (_isFlippedHorizontally)
        {
            adjustedAngle = NormalizeAngle(-adjustedAngle);
        }
        var finalAngle = NormalizeAngle(adjustedAngle + _vehicleAngleOffset);

        TransformGroup transformGroup;
        ScaleTransform flipTransform;
        RotateTransform rotateTransform;

        if (bodyElement.RenderTransform is TransformGroup existingGroup &&
            existingGroup.Children.Count >= 2 &&
            existingGroup.Children[0] is ScaleTransform existingScale &&
            existingGroup.Children[1] is RotateTransform existingRotate)
        {
            transformGroup = existingGroup;
            flipTransform = existingScale;
            rotateTransform = existingRotate;
        }
        else
        {
            flipTransform = new ScaleTransform(1, 1);
            rotateTransform = new RotateTransform();
            transformGroup = new TransformGroup();
            transformGroup.Children.Add(flipTransform);
            transformGroup.Children.Add(rotateTransform);
            bodyElement.RenderTransform = transformGroup;
        }

        bodyElement.RenderTransformOrigin = new Point(0.5, 0.5);

        // 좌우반전만 적용 (위아래는 유지), 턴 방향도 올바르게 보이도록 각도 조정
        var shouldFlipForMapAndVehicle = _areVehiclesFlipped ^ _isFlippedHorizontally;
        flipTransform.ScaleX = shouldFlipForMapAndVehicle ? -1 : 1;
        flipTransform.ScaleY = 1;
        var targetAngle = shouldFlipForMapAndVehicle ? -finalAngle : finalAngle;
        SetVehicleRotation(rotateTransform, targetAngle, animate && heading.HasValue);

        visual.NameText.Text = vehicle.Name ?? string.Empty;
        visual.StateText.Text = vehicle.VehicleStateText;

        if (visual.StateText.Foreground is SolidColorBrush statusBrush)
        {
            statusBrush.Color = stateColor;
        }
        else
        {
            visual.StateText.Foreground = new SolidColorBrush(stateColor);
        }

        // 알람 텍스트 업데이트
        if (vehicle.HasAlarms && vehicle.Alarms.Count > 0)
        {
            // 첫 번째 알람 표시, 여러 개면 개수 표시
            var firstAlarm = vehicle.Alarms[0];
            var alarmMessage = vehicle.Alarms.Count > 1
                ? $"{firstAlarm} +{vehicle.Alarms.Count - 1}개"
                : firstAlarm;

            visual.AlarmText.Text = alarmMessage;
            visual.AlarmText.Visibility = Visibility.Visible;

            // 알람 타입에 따른 텍스트 색상 변경
            var isError = vehicle.Alarms.Any(a => a.StartsWith("vehicle.error", StringComparison.OrdinalIgnoreCase));
            var textColor = isError ? Color.FromRgb(239, 68, 68) : Color.FromRgb(245, 158, 11);

            if (visual.AlarmText.Foreground is SolidColorBrush alarmBrush)
            {
                alarmBrush.Color = textColor;
            }
            else
            {
                visual.AlarmText.Foreground = new SolidColorBrush(textColor);
            }
        }
        else
        {
            visual.AlarmText.Visibility = Visibility.Collapsed;
        }

        if (visual.LabelContainer.Background is SolidColorBrush bgBrush)
        {
            bgBrush.Color = Color.FromArgb(160, 20, 20, 20);
        }
        else
        {
            visual.LabelContainer.Background = new SolidColorBrush(Color.FromArgb(160, 20, 20, 20));
        }

        ApplyVehicleLabelTransform(visual.LabelContainer);

        visual.LabelContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desiredSize = visual.LabelContainer.DesiredSize;
        var labelWidth = desiredSize.Width > 0 ? desiredSize.Width : visual.LabelContainer.ActualWidth;
        if (labelWidth <= 0)
        {
            labelWidth = 40;
        }

        var labelHeight = desiredSize.Height > 0 ? desiredSize.Height : visual.LabelContainer.ActualHeight;
        if (labelHeight <= 0)
        {
            labelHeight = 16;
        }

        var clampedZoom = Math.Max(Math.Abs(zoomScale), MIN_ZOOM);
        var scaleFactor = clampedZoom >= 1.0
            ? Math.Clamp(1.0 / Math.Sqrt(clampedZoom), 0.65, 1.0)
            : 1.0;
        var baseScale = 0.9;
        var desiredLabelScale = baseScale * scaleFactor * _vehicleLabelScale;
        var labelScale = Math.Clamp(desiredLabelScale, 0.35, 2.0);
        var baseGap = bodyWidth * 0.05;
        var horizontalGap = Math.Max(0, baseGap * labelScale);
        var offsetSign = (_isFlippedHorizontally ^ _areVehiclesFlipped) ? -1.0 : 1.0;

        var scaledLabelHalfWidth = (labelWidth * labelScale) / 2.0;
        var offsetDistance = bodyWidth / 2 + horizontalGap + scaledLabelHalfWidth;
        var angleRad = -_rotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);
        var offsetX = offsetSign * offsetDistance * cos;
        var offsetY = offsetSign * offsetDistance * sin;

        var bodyLeft = bodyCenter.X - bodyWidth / 2;
        var bodyTop = bodyCenter.Y - bodyHeight / 2;
        var labelCenterX = bodyCenter.X + offsetX;
        var labelCenterY = bodyCenter.Y + offsetY;
        var labelLeft = labelCenterX - labelWidth / 2;
        var labelTop = labelCenterY - labelHeight / 2;

        SetElementPosition(bodyElement, bodyLeft, bodyTop, animate);
        SetElementPosition(visual.LabelContainer, labelLeft, labelTop, animate);

        // 알람 표시 처리
        if (vehicle.HasAlarms)
        {
            // 알람 타입에 따른 색상 결정
            var alarmColor = GetAlarmColor(vehicle.Alarms);

            if (visual.AlarmGlow == null)
            {
                // 알람 Glow 생성
                var alarmGlow = new Ellipse
                {
                    Fill = new SolidColorBrush(alarmColor),
                    StrokeThickness = 0,
                    Opacity = 0.6,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1.0, 1.0)
                };
                Panel.SetZIndex(alarmGlow, 899);
                _drawingSurface.Children.Add(alarmGlow);
                visual.AlarmGlow = alarmGlow;

                // 애니메이션 시작
                var blinkAnimation = CreateTargetNodeBlinkAnimation();
                alarmGlow.BeginAnimation(UIElement.OpacityProperty, blinkAnimation);
                if (alarmGlow.RenderTransform is ScaleTransform scaleTransform)
                {
                    var scaleAnimation = CreateTargetNodeScaleAnimation(0.8, 1.5);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                }
            }

            // 알람 색상 업데이트
            if (visual.AlarmGlow.Fill is SolidColorBrush brush)
            {
                brush.Color = alarmColor;
            }

            // 알람 Glow 위치 및 크기 업데이트
            var glowSize = Math.Max(bodyWidth, bodyHeight) * 1.0;
            visual.AlarmGlow.Width = glowSize;
            visual.AlarmGlow.Height = glowSize;
            Canvas.SetLeft(visual.AlarmGlow, bodyCenter.X - glowSize / 2);
            Canvas.SetTop(visual.AlarmGlow, bodyCenter.Y - glowSize / 2);
        }
        else if (visual.AlarmGlow != null)
        {
            // 알람이 없으면 Glow 제거
            _drawingSurface.Children.Remove(visual.AlarmGlow);
            visual.AlarmGlow = null;
        }
    }

    private static Color GetAlarmColor(List<string> alarms)
    {
        // vehicle.error로 시작하는 알람이 있으면 진한 붉은색 (우선순위 높음)
        foreach (var alarm in alarms)
        {
            if (alarm.StartsWith("vehicle.error", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(140, 220, 38, 38); // 진한 붉은색
            }
        }

        // vehicle.warning으로 시작하는 알람이 있으면 밝은 주황/노란색
        foreach (var alarm in alarms)
        {
            if (alarm.StartsWith("vehicle.warning", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(140, 245, 158, 11); // 밝은 주황색 (노란빛)
            }
        }

        // 기본값은 붉은색
        return Color.FromArgb(140, 220, 38, 38);
    }

    private string BuildVehicleShapeSignature(Vehicle vehicle)
    {
        if (vehicle == null)
        {
            return "__null__";
        }

        var hash = new HashCode();
        hash.Add(vehicle.VehicleType ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        if (vehicle.VehicleShape is { Count: > 0 })
        {
            foreach (var entry in vehicle.VehicleShape)
            {
                hash.Add(entry ?? string.Empty, StringComparer.Ordinal);
            }
            return $"shape:{hash.ToHashCode():X}";
        }

        if (vehicle.BodyShape is { Count: > 0 })
        {
            hash.Add("__body__", StringComparer.Ordinal);
            foreach (var entry in vehicle.BodyShape)
            {
                hash.Add(entry ?? string.Empty, StringComparer.Ordinal);
            }
            return $"body:{hash.ToHashCode():X}";
        }

        return $"fallback:{hash.ToHashCode():X}";
    }

    private VehicleShapeData? TryCreateVehicleShapeData(Vehicle vehicle)
    {
        if (vehicle == null)
        {
            return null;
        }

        var preferred = CreateVehicleShapeDataFromPoints(ParseVehicleShapePoints(vehicle.VehicleShape));
        if (preferred != null)
        {
            return preferred;
        }

        var bodyOnly = CreateVehicleShapeDataFromPoints(ParseVehicleShapePoints(vehicle.BodyShape));
        if (bodyOnly != null)
        {
            return bodyOnly;
        }

        if (IsForkliftVehicle(vehicle))
        {
            return CreateFallbackForkliftShapeData();
        }

        return null;
    }

    private static List<Point> ParseVehicleShapePoints(IReadOnlyList<string>? rawPoints)
    {
        var points = new List<Point>();

        if (rawPoints == null)
        {
            return points;
        }

        double? pendingX = null;

        foreach (var raw in rawPoints)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var matches = ShapeNumberRegex.Matches(raw);
            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                if (!double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                if (pendingX == null)
                {
                    pendingX = value;
                }
                else
                {
                    points.Add(new Point(pendingX.Value, value));
                    pendingX = null;
                }
            }
        }

        return points;
    }

    private static VehicleShapeData? CreateVehicleShapeDataFromPoints(List<Point> points)
    {
        if (points.Count < 3)
        {
            return null;
        }

        var filtered = new List<Point>(points.Count);
        foreach (var point in points)
        {
            if (filtered.Count == 0 || !ArePointsClose(filtered[^1], point, 1e-6))
            {
                filtered.Add(point);
            }
        }

        if (filtered.Count > 2 && ArePointsClose(filtered[0], filtered[^1], 1e-6))
        {
            filtered.RemoveAt(filtered.Count - 1);
        }

        if (filtered.Count < 3)
        {
            return null;
        }

        var minX = filtered.Min(p => p.X);
        var maxX = filtered.Max(p => p.X);
        var minY = filtered.Min(p => p.Y);
        var maxY = filtered.Max(p => p.Y);

        var width = maxX - minX;
        var height = maxY - minY;

        if (width <= double.Epsilon || height <= double.Epsilon)
        {
            return null;
        }

        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;

        var normalized = new List<Point>(filtered.Count);
        foreach (var point in filtered)
        {
            normalized.Add(new Point(point.X - centerX, point.Y - centerY));
        }

        return new VehicleShapeData(normalized, width, height);
    }

    private static VehicleShapeData CreateFallbackForkliftShapeData()
    {
        var fallbackPoints = new List<Point>
        {
            new(-0.6, -0.95),
            new(0.6, -0.95),
            new(0.6, 0.05),
            new(0.95, 0.05),
            new(0.95, 0.25),
            new(0.45, 0.25),
            new(0.45, 0.85),
            new(0.75, 0.85),
            new(0.75, 1.15),
            new(-0.75, 1.15),
            new(-0.75, 0.85),
            new(-0.45, 0.85),
            new(-0.45, 0.25),
            new(-0.95, 0.25),
            new(-0.95, 0.05),
            new(-0.6, 0.05)
        };

        var shapeData = CreateVehicleShapeDataFromPoints(fallbackPoints);
        if (shapeData != null)
        {
            return shapeData;
        }

        var minX = fallbackPoints.Min(p => p.X);
        var maxX = fallbackPoints.Max(p => p.X);
        var minY = fallbackPoints.Min(p => p.Y);
        var maxY = fallbackPoints.Max(p => p.Y);
        var width = Math.Max(maxX - minX, 0.01);
        var height = Math.Max(maxY - minY, 0.01);
        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;

        var normalized = fallbackPoints
            .Select(p => new Point(p.X - centerX, p.Y - centerY))
            .ToList();

        return new VehicleShapeData(normalized, width, height);
    }

    private static bool IsForkliftVehicle(Vehicle vehicle)
    {
        if (vehicle == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(vehicle.VehicleType) &&
            vehicle.VehicleType.Contains("fork", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(vehicle.Name) &&
               vehicle.Name.Contains("지게", StringComparison.OrdinalIgnoreCase);
    }

    private Size UpdateVehicleShapePath(Path path, VehicleShapeData shapeData, double targetWidth, double targetHeight)
    {
        var originalWidth = Math.Max(shapeData.Width, double.Epsilon);
        var originalHeight = Math.Max(shapeData.Height, double.Epsilon);

        var scaleX = targetWidth / originalWidth;
        var scaleY = targetHeight / originalHeight;
        var scale = Math.Max(Math.Min(scaleX, scaleY), 0.01);

        var actualWidth = originalWidth * scale;
        var actualHeight = originalHeight * scale;
        var halfWidth = actualWidth / 2.0;
        var halfHeight = actualHeight / 2.0;

        var geometry = new StreamGeometry
        {
            FillRule = FillRule.EvenOdd
        };

        using (var context = geometry.Open())
        {
            var isFirst = true;
            foreach (var point in shapeData.NormalizedPoints)
            {
                var x = point.X * scale + halfWidth;
                var y = point.Y * scale + halfHeight;
                if (isFirst)
                {
                    context.BeginFigure(new Point(x, y), true, true);
                    isFirst = false;
                }
                else
                {
                    context.LineTo(new Point(x, y), true, false);
                }
            }
        }

        geometry.Freeze();
        path.Data = geometry;
        path.Width = actualWidth;
        path.Height = actualHeight;
        path.StrokeLineJoin = PenLineJoin.Round;
        path.StrokeEndLineCap = PenLineCap.Round;
        path.StrokeStartLineCap = PenLineCap.Round;

        return new Size(actualWidth, actualHeight);
    }

    private void SetElementPosition(FrameworkElement element, double targetLeft, double targetTop, bool animate)
    {
        if (double.IsNaN(Canvas.GetLeft(element)))
        {
            Canvas.SetLeft(element, targetLeft);
        }

        if (double.IsNaN(Canvas.GetTop(element)))
        {
            Canvas.SetTop(element, targetTop);
        }

        if (!animate)
        {
            element.BeginAnimation(Canvas.LeftProperty, null);
            element.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(element, targetLeft);
            Canvas.SetTop(element, targetTop);
            return;
        }

        const double minimumDelta = 0.25;
        var currentLeft = Canvas.GetLeft(element);
        var currentTop = Canvas.GetTop(element);

        var requiresLeftAnimation = Math.Abs(currentLeft - targetLeft) > minimumDelta;
        var requiresTopAnimation = Math.Abs(currentTop - targetTop) > minimumDelta;

        var duration = TimeSpan.FromSeconds(1);

        if (requiresLeftAnimation)
        {
            var leftAnimation = new DoubleAnimation
            {
                From = currentLeft,
                To = targetLeft,
                Duration = duration,
                FillBehavior = FillBehavior.HoldEnd
            };
            element.BeginAnimation(Canvas.LeftProperty, leftAnimation, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            element.BeginAnimation(Canvas.LeftProperty, null);
            Canvas.SetLeft(element, targetLeft);
        }

        if (requiresTopAnimation)
        {
            var topAnimation = new DoubleAnimation
            {
                From = currentTop,
                To = targetTop,
                Duration = duration,
                FillBehavior = FillBehavior.HoldEnd
            };
            element.BeginAnimation(Canvas.TopProperty, topAnimation, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            element.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetTop(element, targetTop);
        }
    }

    private void SetVehicleRotation(RotateTransform rotateTransform, double targetAngle, bool animate)
    {
        if (rotateTransform == null)
        {
            return;
        }

        var normalizedTarget = NormalizeAngle(targetAngle);

        if (!animate)
        {
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            rotateTransform.Angle = normalizedTarget;
            return;
        }

        var currentAngle = NormalizeAngle(rotateTransform.Angle);
        var delta = ((normalizedTarget - currentAngle + 540.0) % 360.0) - 180.0;

        if (Math.Abs(delta) < 0.1)
        {
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            rotateTransform.Angle = normalizedTarget;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = currentAngle,
            To = currentAngle + delta,
            Duration = TimeSpan.FromMilliseconds(450),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            rotateTransform.Angle = normalizedTarget;
        };

        rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void RemoveInactiveVehicleVisuals(HashSet<string> activeKeys)
    {
        if (_vehicleVisuals.Count == 0)
        {
            return;
        }

        var staleKeys = _vehicleVisuals.Keys
            .Where(key => !activeKeys.Contains(key))
            .ToList();

        foreach (var key in staleKeys)
        {
            if (_vehicleVisuals.TryGetValue(key, out var visual))
            {
                RemoveVehicleVisual(visual);
                _vehicleVisuals.Remove(key);
            }
        }
    }

    private void RemoveVehicleVisual(VehicleVisual visual)
    {
        RemoveFrameworkElement(visual.BodyElement);
        RemoveFrameworkElement(visual.LabelContainer);
        if (visual.AlarmGlow != null)
        {
            RemoveFrameworkElement(visual.AlarmGlow);
        }
    }

    private void RemoveFrameworkElement(FrameworkElement element)
    {
        element.BeginAnimation(Canvas.LeftProperty, null);
        element.BeginAnimation(Canvas.TopProperty, null);
        _drawingSurface.Children.Remove(element);
    }

    private void RefreshVehicleLabelTransforms()
    {
        if (_vehicleVisuals.Count == 0)
        {
            return;
        }

        foreach (var visual in _vehicleVisuals.Values)
        {
            ApplyVehicleLabelTransform(visual.LabelContainer);
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

        var color = AssignVehiclePathColor(vehicleKey);
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        _vehiclePathBrushes[vehicleKey] = brush;
        return brush;
    }

    private Color AssignVehiclePathColor(string vehicleKey)
    {
        try
        {
            var usedColors = _vehiclePathBrushes.Values
                .Select(b => b.Color)
                .ToHashSet();

            foreach (var paletteColor in _vehiclePathPalette)
            {
                if (!usedColors.Contains(paletteColor))
                {
                    return paletteColor;
                }
            }

            var hash = Math.Abs(HashCode.Combine(vehicleKey));
            byte BaseComponent(int offset) => (byte)(64 + ((hash >> offset) & 0xFF) % 160);
            var fallback = Color.FromRgb(BaseComponent(0), BaseComponent(8), BaseComponent(16));

            var attempt = 0;
            while (usedColors.Contains(fallback) && attempt < 10)
            {
                fallback = Color.FromRgb(
                    (byte)((fallback.R + 85) % 256),
                    (byte)((fallback.G + 170) % 256),
                    (byte)((fallback.B + 43) % 256));
                attempt++;
            }

            return fallback;
        }
        catch
        {
            return _vehiclePathPalette[0];
        }
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
        _lastMousePosition = e.GetPosition(MapCanvas);
        MapCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var currentPosition = e.GetPosition(MapCanvas);
        var delta = currentPosition - _lastMousePosition;
        _lastMousePosition = currentPosition;

        _offsetX += delta.X;
        _offsetY += delta.Y;

        UpdateViewTransform();
        e.Handled = true;
    }

    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
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
            _originalVehicleAngleOffset = _vehicleAngleOffset;
            _originalVehicleFlipState = _areVehiclesFlipped;
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

    private void ResetVehicleAngle_Click(object sender, RoutedEventArgs e)
    {
        VehicleAngleOffset = 0;
        VehicleAngleSlider.Value = 0;
    }

    private async void CompleteSettings_Click(object sender, RoutedEventArgs e)
    {
        // Save the current settings
        await SaveSettingsAsync();

        // Treat the saved state as the new baseline for cancellation scenarios
        _originalRotationAngle = _rotationAngle;
        _originalFlipState = _isFlippedHorizontally;
        _originalVehicleAngleOffset = _vehicleAngleOffset;
        _originalVehicleFlipState = _areVehiclesFlipped;

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
        VehicleAngleOffset = _originalVehicleAngleOffset;
        VehicleAngleSlider.Value = _originalVehicleAngleOffset;
        AreVehiclesFlipped = _originalVehicleFlipState;
        VehicleFlipCheckBox.IsChecked = _originalVehicleFlipState;
        
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

    private void VehicleFlip_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            AreVehiclesFlipped = checkBox.IsChecked == true;
        }
    }

    private void ToggleFlip_Click(object sender, RoutedEventArgs e)
    {
        FlipCheckBox.IsChecked = !FlipCheckBox.IsChecked;
        IsFlippedHorizontally = FlipCheckBox.IsChecked == true;
    }

    private void RotateVehicle90_Click(object sender, RoutedEventArgs e)
    {
        VehicleAngleSlider.Value = (_vehicleAngleOffset + 90) % 360;
    }

    private void RotateVehicle180_Click(object sender, RoutedEventArgs e)
    {
        VehicleAngleSlider.Value = (_vehicleAngleOffset + 180) % 360;
    }

    private void RotateVehicle270_Click(object sender, RoutedEventArgs e)
    {
        VehicleAngleSlider.Value = (_vehicleAngleOffset + 270) % 360;
    }

    private void ToggleVehicleFlip_Click(object sender, RoutedEventArgs e)
    {
        VehicleFlipCheckBox.IsChecked = !VehicleFlipCheckBox.IsChecked;
        AreVehiclesFlipped = VehicleFlipCheckBox.IsChecked == true;
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

    private void ApplyVehicleLabelTransform(UIElement element)
    {
        var group = new TransformGroup();

        var zoomScale = Math.Max(Math.Abs(_scaleTransform.ScaleX), MIN_ZOOM);
        var scaleFactor = zoomScale >= 1.0
            ? Math.Clamp(1.0 / Math.Sqrt(zoomScale), 0.65, 1.0)
            : 1.0;
        var baseScale = 0.9;
        var desiredLabelScale = baseScale * scaleFactor * _vehicleLabelScale;
        var labelScale = Math.Clamp(desiredLabelScale, 0.35, 2.0);

        var rotate = new RotateTransform(-_rotationAngle);
        group.Children.Add(rotate);

        var scaleX = _isFlippedHorizontally ? -labelScale : labelScale;
        group.Children.Add(new ScaleTransform(scaleX, labelScale));

        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        element.RenderTransform = group;
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
            _vehicleAngleOffset = settings.VehicleAngleOffset;
            _areVehiclesFlipped = settings.AreVehiclesFlipped;
            _vehicleLabelScale = settings.VehicleLabelScale <= 0 ? 1.0 : settings.VehicleLabelScale;

            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(RotationAngle));
            OnPropertyChanged(nameof(ZoomLevel));
            OnPropertyChanged(nameof(IsFlippedHorizontally));
            OnPropertyChanged(nameof(NodeSize));
            OnPropertyChanged(nameof(VehicleSize));
            OnPropertyChanged(nameof(VehicleAngleOffset));
            OnPropertyChanged(nameof(AreVehiclesFlipped));
            OnPropertyChanged(nameof(VehicleLabelScale));

            UpdateViewTransform();
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
                VehicleSize = _vehicleSize,
                VehicleAngleOffset = _vehicleAngleOffset,
                AreVehiclesFlipped = _areVehiclesFlipped,
                VehicleLabelScale = _vehicleLabelScale
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
            e.Handled = true;
            return;
        }

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
            "inerror" => (Color.FromRgb(255, 45, 85), Color.FromRgb(255, 99, 71), "red (error)"),
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
