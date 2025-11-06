using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AntManager.Models;
using AntManager.Services;
using AntManager.Views;
using Microsoft.Win32;

namespace AntManager.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AntApiService _antApiService;
    private readonly FileService _fileService;
    
    private string _statusText = "준비";
    private string _connectionStatus = "연결 상태: 미연결";
    private bool _isConnected;
    private string _serverUrl = "localhost:8081";
    private string _username = "admin";
    private string _password = "123456";

    // Mission Dashboard Properties
    private ObservableCollection<MissionInfo> _missions = new();
    private int _totalMissions;
    private int _runningMissions;
    private int _pendingMissions;
    private int _completedMissions;
    private int _cancelledMissions;
    private readonly CollectionViewSource _missionViewSource;
    private ObservableCollection<MissionFilterOption> _missionFilterOptions = new();
    private MissionFilterOption? _selectedMissionFilter;
    private DateTime? _missionFilterStart;
    private DateTime? _missionFilterEnd;
    private string _missionSearchTerm = string.Empty;
    private const int CompletedMissionDefaultOffsetMinutes = 3;
    private string _missionSortProperty = nameof(MissionInfo.MissionIdSortValue);
    private ListSortDirection _missionSortDirection = ListSortDirection.Descending;
    private bool _isFullMissionHistoryLoaded;
    private DateTime _lastFullMissionHistoryRefresh = DateTime.MinValue;
    private readonly TimeSpan _fullMissionHistoryRefreshInterval = TimeSpan.FromSeconds(10);
    private Task? _fullMissionHistoryLoadTask;

    // Snackbar state
    private string _snackbarMessage = string.Empty;
    private bool _isSnackbarVisible;
    private bool _isSnackbarPersistent;
    private CancellationTokenSource? _snackbarCts;
    private bool _isVehicleSnackbarActive;
    private string _vehicleSnackbarMessage = string.Empty;
    private readonly Dictionary<string, string> _activeVehicleAlarms = new(StringComparer.OrdinalIgnoreCase);

    // Mission Router Properties
    private ObservableCollection<MissionTemplate> _missionTemplates = new();
    private CollectionViewSource? _templateViewSource;
    private MissionTemplate? _selectedTemplate;
    private string _templateName = string.Empty;
    private string? _selectedTemplateFilter;
    private ObservableCollection<string> _templateTitles = new();
    private MissionTemplateType _selectedTemplateType = MissionTemplateType.Moving;
    private string _fromNode = string.Empty;
    private string _toNode = string.Empty;
    private string _templateVehicle = string.Empty;
    private int _templatePriority = 2;
    private string _templatePriorityDescription = string.Empty;

    // Dynamic Mission Vars
    private ObservableCollection<DynamicNodeVar> _fromNodeVars = new();
    private ObservableCollection<DynamicNodeVar> _toNodeVars = new();
    private string _fromNodeType = "Dynamic_Lift";
    private string _toNodeType = "Dynamic_Lift";

    // Vehicle Management Properties
    private ObservableCollection<Vehicle> _vehicles = new();
    private readonly CollectionViewSource _vehicleViewSource;
    private string _selectedVehicle = string.Empty;
    private string _selectedInsertNode = string.Empty;
    private ObservableCollection<NodeInfo> _availableNodes = new();
    private ObservableCollection<NodeInfo> _filteredFromNodes = new();
    private ObservableCollection<NodeInfo> _filteredToNodes = new();
    private ObservableCollection<NodeInfo> _filteredInsertNodes = new();
    private string _fromNodeSearchText = string.Empty;
    private string _toNodeSearchText = string.Empty;
    private string _insertNodeSearchText = string.Empty;
    private NodeInfo? _selectedFromNodeItem;
    private NodeInfo? _selectedToNodeItem;
    private NodeInfo? _selectedInsertNodeItem;
    private bool _isFromNodeDropDownOpen;
    private bool _isToNodeDropDownOpen;
    private bool _isInsertNodeDropDownOpen;
    private string _vehicleSortProperty = nameof(Vehicle.Name);
    private ListSortDirection _vehicleSortDirection = ListSortDirection.Descending;

    // Map View Properties
    private List<MapData> _mapData = new();
    private bool _mapDataLoaded = false;

    private const string TimestampPropertyName = nameof(AlarmInfo.Timestamp);

    // Alarm Log Properties
    private ObservableCollection<AlarmInfo> _alarms = new();
    private readonly CollectionViewSource _alarmViewSource;
    private readonly List<SortDescription> _alarmSortDescriptions = new();
    private int _alarmLimit = 50;
    private bool _alarmSortAscending = false;
    private string _alarmSearchTerm = string.Empty;
    private string _selectedAlarmSearchColumn = string.Empty;
    private System.Timers.Timer? _realtimeRefreshTimer;
    private bool _isRealtimeRefreshInProgress;

    public MainViewModel()
    {
        _antApiService = AntApiService.Instance;
        _fileService = new FileService();

        _missionViewSource = new CollectionViewSource { Source = _missions };
        _missionViewSource.Filter += OnMissionFilter;
        _missionViewSource.SortDescriptions.Add(new SortDescription(nameof(MissionInfo.MissionIdSortValue), ListSortDirection.Descending));
        ApplyMissionSort();

        _missionTemplates.CollectionChanged += OnMissionTemplatesCollectionChanged;
        _templateViewSource = new CollectionViewSource { Source = _missionTemplates };
        _templateViewSource.Filter += OnTemplateFilter;

        _vehicleViewSource = new CollectionViewSource { Source = _vehicles };
        ApplyVehicleSort(_vehicleSortProperty, _vehicleSortDirection, updateState: false);

        MissionFilterOptions = new ObservableCollection<MissionFilterOption>(new[]
        {
            new MissionFilterOption("기본 (전체)", null, isDefault: true),
            new MissionFilterOption("전체", null),
            new MissionFilterOption("수신 (0)", new[] { 0 }),
            new MissionFilterOption("대기 (1)", new[] { 1 }),
            new MissionFilterOption("진행 (3)", new[] { 3 }),
            new MissionFilterOption("완료 (4)", new[] { 4 }),
            new MissionFilterOption("취소 (5)", new[] { 5 })
        });
        SelectedMissionFilter = MissionFilterOptions.FirstOrDefault();

        _alarmViewSource = new CollectionViewSource { Source = _alarms };
        _alarmViewSource.Filter += OnAlarmFilter;

        AlarmSearchColumns = new ObservableCollection<AlarmSearchOption>(new[]
        {
            new AlarmSearchOption("전체", "All"),
            new AlarmSearchOption("상태", nameof(AlarmInfo.StateText)),
            new AlarmSearchOption("타입", nameof(AlarmInfo.SourceTypeText)),
            new AlarmSearchOption("소스 ID", nameof(AlarmInfo.SourceId)),
            new AlarmSearchOption("이벤트 이름", nameof(AlarmInfo.EventDisplayName)),
            new AlarmSearchOption("메시지", nameof(AlarmInfo.AlarmMessage)),
            new AlarmSearchOption("횟수", nameof(AlarmInfo.EventCount)),
            new AlarmSearchOption("최초 발생", nameof(AlarmInfo.FirstEventAtText)),
            new AlarmSearchOption("최근 발생", nameof(AlarmInfo.LastEventAtText)),
            new AlarmSearchOption("발생 시간", nameof(AlarmInfo.TimestampText))
        });

        _selectedAlarmSearchColumn = AlarmSearchColumns.First().Key;
        RefreshAlarmFilter();
        ApplyDefaultAlarmSort(force: true);

        InitializeCommands();
        _ = LoadInitialDataAsync();

        // LoginViewModel에서 이미 로그인했으므로 자동 연결 불필요
        // 연결 상태 확인만 수행
        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        if (_antApiService.IsConnected)
        {
            IsConnected = true;
            ConnectionStatus = $"연결 상태: 연결됨 ({_antApiService.CurrentServerUrl})";
            StatusText = "ANT 서버 연결됨";

            // 데이터 로드
            _ = LoadInitialServerData();

            // 알람 자동 갱신 타이머 시작
            StartRealtimeRefreshTimer();
        }
        else
        {
            IsConnected = false;
            ConnectionStatus = "연결 상태: 미연결";
            StatusText = "서버에 연결되지 않았습니다";

            // 타이머 중지
            StopRealtimeRefreshTimer();
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task LoadInitialServerData()
    {
        try
        {
            // 맵 데이터는 한 번만 로드 (정적 데이터)
            if (!_mapDataLoaded)
            {
                await ExecuteRefreshMap();
                _mapDataLoaded = true;
            }
            
            await ExecuteRefreshNodes();
            await ExecuteRefreshVehicles();
            await ExecuteRefreshMissions();
            await ExecuteRefreshAlarms();
        }
        catch (Exception ex)
        {
            StatusText = $"초기 데이터 로드 실패: {ex.Message}";
        }
    }

    private void StartRealtimeRefreshTimer()
    {
        StopRealtimeRefreshTimer();

        _realtimeRefreshTimer = new System.Timers.Timer(1000); // 1초
        _realtimeRefreshTimer.Elapsed += async (_, _) => await RefreshRealtimeDataAsync();
        _realtimeRefreshTimer.AutoReset = true;
        _realtimeRefreshTimer.Start();
    }

    private void StopRealtimeRefreshTimer()
    {
        if (_realtimeRefreshTimer != null)
        {
            _realtimeRefreshTimer.Stop();
            _realtimeRefreshTimer.Dispose();
            _realtimeRefreshTimer = null;
        }
    }

    private async Task RefreshRealtimeDataAsync()
    {
        if (_isRealtimeRefreshInProgress)
        {
            return;
        }

        _isRealtimeRefreshInProgress = true;

        try
        {
            if (SelectedMissionFilter?.RequiresFullHistory ?? false)
            {
                await EnsureFullMissionHistoryAsync(isAutoRefresh: true);
            }
            else
            {
                await RefreshMissionsAsync(isAutoRefresh: true);
            }
            await RefreshVehiclesAsync(isAutoRefresh: true);
            await RefreshAlarmsAsync(isAutoRefresh: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"실시간 데이터 갱신 오류: {ex.Message}");
        }
        finally
        {
            _isRealtimeRefreshInProgress = false;
        }
    }

    #region Properties

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    // Mission Dashboard
    public ObservableCollection<MissionInfo> Missions
    {
        get => _missions;
        set => SetProperty(ref _missions, value);
    }

    public ICollectionView MissionView => _missionViewSource.View;

    public string MissionSortProperty => _missionSortProperty;

    public ListSortDirection MissionSortDirection => _missionSortDirection;

    public ObservableCollection<MissionFilterOption> MissionFilterOptions
    {
        get => _missionFilterOptions;
        private set => SetProperty(ref _missionFilterOptions, value);
    }

    public MissionFilterOption? SelectedMissionFilter
    {
        get => _selectedMissionFilter;
        set
        {
            if (!SetProperty(ref _selectedMissionFilter, value))
            {
                return;
            }

            if (value?.IsDefault ?? false)
            {
                var anyChanged = false;

                if (MissionFilterStart.HasValue)
                {
                    MissionFilterStart = null;
                    anyChanged = true;
                }

                if (MissionFilterEnd.HasValue)
                {
                    MissionFilterEnd = null;
                    anyChanged = true;
                }

                if (!anyChanged)
                {
                    RefreshMissionFilter();
                }
            }
            else
            {
                RefreshMissionFilter();

                if (value?.RequiresFullHistory ?? false)
                {
                    _ = EnsureFullMissionHistoryAsync();
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }
    }

    public DateTime? MissionFilterStart
    {
        get => _missionFilterStart;
        set
        {
            if (SetProperty(ref _missionFilterStart, value))
            {
                RefreshMissionFilter();
            }
        }
    }

    public DateTime? MissionFilterEnd
    {
        get => _missionFilterEnd;
        set
        {
            if (SetProperty(ref _missionFilterEnd, value))
            {
                RefreshMissionFilter();
            }
        }
    }

    public string MissionSearchTerm
    {
        get => _missionSearchTerm;
        set
        {
            if (SetProperty(ref _missionSearchTerm, value))
            {
                RefreshMissionFilter();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TotalMissions
    {
        get => _totalMissions;
        set => SetProperty(ref _totalMissions, value);
    }

    public int RunningMissions
    {
        get => _runningMissions;
        set => SetProperty(ref _runningMissions, value);
    }

    public int PendingMissions
    {
        get => _pendingMissions;
        set => SetProperty(ref _pendingMissions, value);
    }

    public int CompletedMissions
    {
        get => _completedMissions;
        set => SetProperty(ref _completedMissions, value);
    }

    public int CancelledMissions
    {
        get => _cancelledMissions;
        set => SetProperty(ref _cancelledMissions, value);
    }

    public string SnackbarMessage
    {
        get => _snackbarMessage;
        private set => SetProperty(ref _snackbarMessage, value);
    }

    public bool IsSnackbarVisible
    {
        get => _isSnackbarVisible;
        private set => SetProperty(ref _isSnackbarVisible, value);
    }

    // Mission Router
    public ObservableCollection<MissionTemplate> MissionTemplates
    {
        get => _missionTemplates;
        set
        {
            if (_missionTemplates != null)
            {
                _missionTemplates.CollectionChanged -= OnMissionTemplatesCollectionChanged;
            }

            if (SetProperty(ref _missionTemplates, value))
            {
                if (_missionTemplates != null)
                {
                    _missionTemplates.CollectionChanged += OnMissionTemplatesCollectionChanged;
                }

                if (_templateViewSource != null)
                {
                    _templateViewSource.Source = _missionTemplates;
                    _templateViewSource.View?.Refresh();
                }

                RefreshTemplateTitles();
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(TemplateView));
            }
        }
    }

    public ICollectionView? TemplateView => _templateViewSource?.View;

    public ObservableCollection<string> TemplateTitles
    {
        get => _templateTitles;
        set => SetProperty(ref _templateTitles, value);
    }

    public string? SelectedTemplateFilter
    {
        get => _selectedTemplateFilter;
        set
        {
            if (SetProperty(ref _selectedTemplateFilter, value))
            {
                _templateViewSource?.View?.Refresh();
                OnPropertyChanged(nameof(TemplateView));
            }
        }
    }

    public MissionTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string TemplateTitle
    {
        get => _templateName;
        set
        {
            if (SetProperty(ref _templateName, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public MissionTemplateType SelectedTemplateType
    {
        get => _selectedTemplateType;
        set
        {
            if (SetProperty(ref _selectedTemplateType, value))
            {
                OnPropertyChanged(nameof(IsMovingMission));
                OnPropertyChanged(nameof(IsNotMovingMission));
                OnPropertyChanged(nameof(IsPickAndDropMission));
                OnPropertyChanged(nameof(IsDynamicMission));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsMovingMission => _selectedTemplateType == MissionTemplateType.Moving;
    public bool IsPickAndDropMission => _selectedTemplateType == MissionTemplateType.PickAndDrop;
    public bool IsDynamicMission => _selectedTemplateType == MissionTemplateType.Dynamic;
    public bool IsNotMovingMission => _selectedTemplateType != MissionTemplateType.Moving;

    public string FromNode
    {
        get => _fromNode;
        set
        {
            if (SetProperty(ref _fromNode, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ToNode
    {
        get => _toNode;
        set
        {
            if (SetProperty(ref _toNode, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string TemplateVehicle
    {
        get => _templateVehicle;
        set
        {
            if (SetProperty(ref _templateVehicle, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TemplatePriority
    {
        get => _templatePriority;
        set
        {
            if (SetProperty(ref _templatePriority, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string TemplatePriorityDescription
    {
        get => _templatePriorityDescription;
        set
        {
            if (SetProperty(ref _templatePriorityDescription, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ObservableCollection<DynamicNodeVar> FromNodeVars
    {
        get => _fromNodeVars;
        set => SetProperty(ref _fromNodeVars, value);
    }

    public ObservableCollection<DynamicNodeVar> ToNodeVars
    {
        get => _toNodeVars;
        set => SetProperty(ref _toNodeVars, value);
    }

    public string FromNodeType
    {
        get => _fromNodeType;
        set => SetProperty(ref _fromNodeType, value);
    }

    public string ToNodeType
    {
        get => _toNodeType;
        set => SetProperty(ref _toNodeType, value);
    }

    // Vehicle Management
    public ObservableCollection<Vehicle> Vehicles
    {
        get => _vehicles;
        set
        {
            if (SetProperty(ref _vehicles, value))
            {
                _vehicleViewSource.Source = _vehicles;
                ApplyVehicleSort(_vehicleSortProperty, _vehicleSortDirection, updateState: false);
            }
        }
    }

    public ICollectionView VehiclesView => _vehicleViewSource.View;

    public string VehicleSortProperty => _vehicleSortProperty;

    public ListSortDirection VehicleSortDirection => _vehicleSortDirection;

    public string SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public string SelectedInsertNode
    {
        get => _selectedInsertNode;
        set => SetProperty(ref _selectedInsertNode, value);
    }

    public ObservableCollection<NodeInfo> AvailableNodes
    {
        get => _availableNodes;
        set
        {
            if (SetProperty(ref _availableNodes, value))
            {
                UpdateFilteredFromNodes();
                UpdateFilteredToNodes();
                UpdateFilteredInsertNodes();
            }
        }
    }

    public ObservableCollection<NodeInfo> FilteredFromNodes
    {
        get => _filteredFromNodes;
        set => SetProperty(ref _filteredFromNodes, value);
    }

    public ObservableCollection<NodeInfo> FilteredToNodes
    {
        get => _filteredToNodes;
        set => SetProperty(ref _filteredToNodes, value);
    }

    public ObservableCollection<NodeInfo> FilteredInsertNodes
    {
        get => _filteredInsertNodes;
        set => SetProperty(ref _filteredInsertNodes, value);
    }

    public string FromNodeSearchText
    {
        get => _fromNodeSearchText;
        set
        {
            if (SetProperty(ref _fromNodeSearchText, value))
            {
                UpdateFilteredFromNodes();
                // 텍스트 입력 시 드롭다운 자동 열기
                if (!string.IsNullOrEmpty(value))
                {
                    IsFromNodeDropDownOpen = true;
                }
            }
        }
    }

    public string ToNodeSearchText
    {
        get => _toNodeSearchText;
        set
        {
            if (SetProperty(ref _toNodeSearchText, value))
            {
                UpdateFilteredToNodes();
                // 텍스트 입력 시 드롭다운 자동 열기
                if (!string.IsNullOrEmpty(value))
                {
                    IsToNodeDropDownOpen = true;
                }
            }
        }
    }

    public bool IsFromNodeDropDownOpen
    {
        get => _isFromNodeDropDownOpen;
        set
        {
            if (SetProperty(ref _isFromNodeDropDownOpen, value))
            {
                // 드롭다운이 열릴 때 (토글 버튼 클릭)
                if (value && string.IsNullOrEmpty(_fromNodeSearchText))
                {
                    // 검색 텍스트가 없으면 전체 리스트 표시
                    UpdateFilteredFromNodes();
                }
            }
        }
    }

    public bool IsToNodeDropDownOpen
    {
        get => _isToNodeDropDownOpen;
        set
        {
            if (SetProperty(ref _isToNodeDropDownOpen, value))
            {
                // 드롭다운이 열릴 때 (토글 버튼 클릭)
                if (value && string.IsNullOrEmpty(_toNodeSearchText))
                {
                    // 검색 텍스트가 없으면 전체 리스트 표시
                    UpdateFilteredToNodes();
                }
            }
        }
    }

    public string InsertNodeSearchText
    {
        get => _insertNodeSearchText;
        set
        {
            if (SetProperty(ref _insertNodeSearchText, value))
            {
                UpdateFilteredInsertNodes();
                // 텍스트 입력 시 드롭다운 자동 열기
                if (!string.IsNullOrEmpty(value))
                {
                    IsInsertNodeDropDownOpen = true;
                }
            }
        }
    }

    public bool IsInsertNodeDropDownOpen
    {
        get => _isInsertNodeDropDownOpen;
        set
        {
            if (SetProperty(ref _isInsertNodeDropDownOpen, value))
            {
                // 드롭다운이 열릴 때 (토글 버튼 클릭)
                if (value && string.IsNullOrEmpty(_insertNodeSearchText))
                {
                    // 검색 텍스트가 없으면 전체 리스트 표시
                    UpdateFilteredInsertNodes();
                }
            }
        }
    }

    public NodeInfo? SelectedFromNodeItem
    {
        get => _selectedFromNodeItem;
        set
        {
            if (SetProperty(ref _selectedFromNodeItem, value))
            {
                if (value != null)
                {
                    FromNode = value.Name;
                    FromNodeSearchText = value.Name;
                }
            }
        }
    }

    public NodeInfo? SelectedToNodeItem
    {
        get => _selectedToNodeItem;
        set
        {
            if (SetProperty(ref _selectedToNodeItem, value))
            {
                if (value != null)
                {
                    ToNode = value.Name;
                    ToNodeSearchText = value.Name;
                }
            }
        }
    }

    public NodeInfo? SelectedInsertNodeItem
    {
        get => _selectedInsertNodeItem;
        set
        {
            if (SetProperty(ref _selectedInsertNodeItem, value))
            {
                if (value != null)
                {
                    SelectedInsertNode = value.Name;
                    InsertNodeSearchText = value.Name;
                }
            }
        }
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    // Alarm Log
    public ObservableCollection<AlarmInfo> Alarms
    {
        get => _alarms;
        set
        {
            if (SetProperty(ref _alarms, value))
            {
                _alarmViewSource.Source = _alarms;
                RefreshAlarmFilter();
                UpdateAlarmSort(force: true);
            }
        }
    }

    public ICollectionView AlarmView => _alarmViewSource.View;

    public ObservableCollection<AlarmSearchOption> AlarmSearchColumns { get; }

    public int AlarmLimit
    {
        get => _alarmLimit;
        set => SetProperty(ref _alarmLimit, value);
    }

    public bool AlarmSortAscending
    {
        get => _alarmSortAscending;
        set
        {
            if (SetProperty(ref _alarmSortAscending, value))
            {
                ApplyDefaultAlarmSort(force: true);
            }
        }
    }

    public string SelectedAlarmSearchColumn
    {
        get => _selectedAlarmSearchColumn;
        set
        {
            if (SetProperty(ref _selectedAlarmSearchColumn, value))
            {
                RefreshAlarmFilter();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string AlarmSearchTerm
    {
        get => _alarmSearchTerm;
        set
        {
            if (SetProperty(ref _alarmSearchTerm, value))
            {
                RefreshAlarmFilter();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    // Map View
    public List<MapData> MapData
    {
        get => _mapData;
        set => SetProperty(ref _mapData, value);
    }

    #endregion

    #region Commands

    public ICommand LogoutCommand { get; private set; } = null!;
    public ICommand RefreshMissionsCommand { get; private set; } = null!;
    public ICommand CancelMissionCommand { get; private set; } = null!;
    
    public ICommand SaveTemplateCommand { get; private set; } = null!;
    public ICommand ClearTemplateCommand { get; private set; } = null!;
    public ICommand ExecuteTemplateCommand { get; private set; } = null!;
    public ICommand EditTemplateCommand { get; private set; } = null!;
    public ICommand DeleteTemplateCommand { get; private set; } = null!;
    public ICommand AddFromVarCommand { get; private set; } = null!;
    public ICommand AddToVarCommand { get; private set; } = null!;
    public ICommand RemoveFromVarCommand { get; private set; } = null!;
    public ICommand RemoveToVarCommand { get; private set; } = null!;
    public ICommand ImportTemplatesCommand { get; private set; } = null!;
    public ICommand ExportTemplatesCommand { get; private set; } = null!;
    
    public ICommand InsertVehicleCommand { get; private set; } = null!;
    public ICommand ExtractVehicleCommand { get; private set; } = null!;
    public ICommand RefreshVehiclesCommand { get; private set; } = null!;
    public ICommand RefreshNodesCommand { get; private set; } = null!;
    public ICommand ConnectToServerCommand { get; private set; } = null!;

    public ICommand RefreshAlarmsCommand { get; private set; } = null!;
    public ICommand ClearAlarmSearchCommand { get; private set; } = null!;
    public ICommand ClearMissionSearchCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        LogoutCommand = new RelayCommand(ExecuteLogout);
        RefreshMissionsCommand = new AsyncRelayCommand(ExecuteRefreshMissions);
        CancelMissionCommand = new AsyncRelayCommand(ExecuteCancelMissionAsync, CanExecuteCancelMission);
        
        SaveTemplateCommand = new AsyncRelayCommand(ExecuteSaveTemplate, CanExecuteSaveTemplate);
        ClearTemplateCommand = new RelayCommand(ExecuteClearTemplate);
        ExecuteTemplateCommand = new AsyncRelayCommand(ExecuteTemplate);
        EditTemplateCommand = new RelayCommand(ExecuteEditTemplate);
        DeleteTemplateCommand = new AsyncRelayCommand(ExecuteDeleteTemplate);
        AddFromVarCommand = new RelayCommand(ExecuteAddFromVar);
        AddToVarCommand = new RelayCommand(ExecuteAddToVar);
        RemoveFromVarCommand = new RelayCommand(ExecuteRemoveFromVar);
        RemoveToVarCommand = new RelayCommand(ExecuteRemoveToVar);
        ImportTemplatesCommand = new AsyncRelayCommand(ExecuteImportTemplates);
        ExportTemplatesCommand = new AsyncRelayCommand(ExecuteExportTemplates, _ => MissionTemplates.Any());
        
        InsertVehicleCommand = new AsyncRelayCommand(ExecuteInsertVehicle, CanExecuteVehicleCommand);
        ExtractVehicleCommand = new AsyncRelayCommand(ExecuteExtractVehicle, CanExecuteVehicleCommand);
        RefreshVehiclesCommand = new AsyncRelayCommand(ExecuteRefreshVehicles);
    RefreshNodesCommand = new AsyncRelayCommand(ExecuteRefreshNodes);
    ConnectToServerCommand = new AsyncRelayCommand(ExecuteConnectToServer);

    RefreshAlarmsCommand = new AsyncRelayCommand(ExecuteRefreshAlarms);
    ClearAlarmSearchCommand = new RelayCommand(_ => ClearAlarmSearch(), _ => CanClearAlarmSearch());
    ClearMissionSearchCommand = new RelayCommand(_ => ClearMissionSearch(), _ => CanClearMissionSearch());
}

    #endregion

    #region Command Implementations

    private void ExecuteLogout()
    {
        // Show confirmation dialog
        bool result = CommonDialogWindow.ShowDialog(
            "Are you sure you want to logout?",
            "Logout Confirmation",
            CommonDialogWindow.DialogType.Question);

        if (result)
        {
            // 타이머 중지
            StopRealtimeRefreshTimer();

            var loginWindow = new LoginWindow();
            loginWindow.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow)
                {
                    window.Close();
                    break;
                }
            }
        }
    }

    private Task ExecuteRefreshMissions()
    {
        return SelectedMissionFilter?.RequiresFullHistory ?? false
            ? EnsureFullMissionHistoryAsync(force: true)
            : RefreshMissionsAsync(isAutoRefresh: false);
    }

    private async Task RefreshMissionsAsync(bool isAutoRefresh, bool allowWhenFullHistory = false)
    {
        if (!allowWhenFullHistory && (SelectedMissionFilter?.RequiresFullHistory ?? false))
        {
            return;
        }

        _isFullMissionHistoryLoaded = false;
        _lastFullMissionHistoryRefresh = DateTime.MinValue;

        if (!isAutoRefresh)
        {
            StatusText = "미션 정보 새로고침 중...";
        }

        try
        {
            var newMissions = await _antApiService.GetAllMissionsAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var existingMissions = Missions.ToDictionary(m => m.MissionId);
                var newMissionsDict = newMissions.ToDictionary(m => m.MissionId);

                // Remove missions that no longer exist
                var missionsToRemove = existingMissions.Keys.Except(newMissionsDict.Keys).ToList();
                foreach (var missionId in missionsToRemove)
                {
                    Missions.Remove(existingMissions[missionId]);
                }

                // Add new missions and update existing ones
                foreach (var newMission in newMissions)
                {
                    if (existingMissions.TryGetValue(newMission.MissionId, out var existingMission))
                    {
                        // Update properties
                        UpdateMissionProperties(existingMission, newMission);
                    }
                    else
                    {
                        Missions.Add(newMission);
                    }
                }

                RefreshMissionFilter();
                CommandManager.InvalidateRequerySuggested();
            });

            if (!isAutoRefresh)
            {
                StatusText = "미션 정보 업데이트 완료";
            }
        }
        catch (Exception ex)
        {
            if (!isAutoRefresh)
            {
                StatusText = $"미션 새로고침 실패: {ex.Message}";
            }
            else
            {
                Debug.WriteLine($"미션 자동 갱신 오류: {ex.Message}");
            }
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool CanExecuteCancelMission(object? parameter)
    {
        if (parameter is not string missionId)
        {
            return false;
        }

        var mission = Missions.FirstOrDefault(m => m.MissionId == missionId);
        return mission?.CanCancel == true && _antApiService.IsConnected;
    }

    private async Task ExecuteCancelMissionAsync(object? parameter)
    {
        if (parameter is not string missionId)
        {
            return;
        }

        var mission = Missions.FirstOrDefault(m => m.MissionId == missionId);
        if (mission?.CanCancel != true)
        {
            return;
        }

        var confirm = CommonDialogWindow.ShowDialog(
            message: $"미션 {missionId}을(를) 취소하시겠습니까?",
            title: "미션 취소 확인",
            type: CommonDialogWindow.DialogType.Question);

        if (!confirm)
        {
            return;
        }

        StatusText = $"미션 {missionId} 취소 중...";

        try
        {
            var cancelled = await _antApiService.CancelMissionAsync(missionId);

            if (cancelled)
            {
                StatusText = $"미션 {missionId} 취소 완료";
                ShowGeneralSnackbar($"미션 {missionId} 취소 완료");
                await RefreshMissionsAsync(isAutoRefresh: true);
            }
            else
            {
                StatusText = $"미션 {missionId} 취소 실패";
                ShowGeneralSnackbar($"미션 {missionId} 취소 실패");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"미션 {missionId} 취소 오류: {ex.Message}";
            ShowGeneralSnackbar($"미션 {missionId} 취소 오류: {ex.Message}");
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }


    // Mission Template Methods
    private bool CanExecuteSaveTemplate()
    {
        if (string.IsNullOrWhiteSpace(TemplateTitle))
            return false;

        if (IsMovingMission)
            return !string.IsNullOrWhiteSpace(ToNode);

        return !string.IsNullOrWhiteSpace(FromNode) && !string.IsNullOrWhiteSpace(ToNode);
    }

    private async Task ExecuteSaveTemplate()
    {
        var template = new MissionTemplate
        {
            Title = TemplateTitle,
            TemplateType = SelectedTemplateType,
            FromNode = FromNode,
            ToNode = ToNode,
            Vehicle = TemplateVehicle,
            Priority = TemplatePriority,
            PriorityDescription = TemplatePriorityDescription,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        if (IsDynamicMission)
        {
            if (FromNodeVars.Count > 0)
            {
                template.FromNodeConfig = new DynamicNodeConfig
                {
                    NodeType = FromNodeType,
                    Vars = new ObservableCollection<DynamicNodeVar>(FromNodeVars)
                };
            }

            if (ToNodeVars.Count > 0)
            {
                template.ToNodeConfig = new DynamicNodeConfig
                {
                    NodeType = ToNodeType,
                    Vars = new ObservableCollection<DynamicNodeVar>(ToNodeVars)
                };
            }
        }

        MissionTemplates.Add(template);
        OnPropertyChanged(nameof(TemplateView));

        try
        {
            await _fileService.SaveTemplatesAsync(MissionTemplates.ToList());
            StatusText = "템플릿이 저장되었습니다.";
            ExecuteClearTemplate();
        }
        catch (Exception ex)
        {
            StatusText = $"템플릿 저장 실패: {ex.Message}";
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ExecuteClearTemplate()
    {
        TemplateTitle = string.Empty;
        FromNode = string.Empty;
        ToNode = string.Empty;
        TemplateVehicle = string.Empty;
        TemplatePriority = 2;
        TemplatePriorityDescription = string.Empty;
        FromNodeVars.Clear();
        ToNodeVars.Clear();
        FromNodeType = "Dynamic_Lift";
        ToNodeType = "Dynamic_Lift";
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task ExecuteTemplate(object? parameter)
    {
        if (parameter is not MissionTemplate template)
            return;

        StatusText = $"템플릿 '{template.Title}' 실행 중...";

        try
        {
            var success = await _antApiService.CreateMissionFromTemplateAsync(template);

            if (success)
            {
                StatusText = $"미션이 생성되었습니다: {template.Title}";
                ShowGeneralSnackbar($"미션 '{template.Title}' 실행 완료");
                await RefreshMissionsAsync(isAutoRefresh: false);
            }
            else
            {
                StatusText = $"미션 생성 실패: {template.Title}";
                ShowGeneralSnackbar($"미션 생성 실패: {template.Title}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"미션 생성 오류: {ex.Message}";
            ShowGeneralSnackbar($"미션 생성 오류: {ex.Message}");
        }
    }

    private void ExecuteEditTemplate(object? parameter)
    {
        if (parameter is not MissionTemplate template)
        {
            return;
        }

        var working = CloneTemplate(template);
        var fromNodeType = working.FromNodeConfig?.NodeType ?? "Dynamic_Lift";
        var toNodeType = working.ToNodeConfig?.NodeType ?? "Dynamic_Lift";
        var fromVars = CloneVars(working.FromNodeConfig?.Vars);
        var toVars = CloneVars(working.ToNodeConfig?.Vars);

        var dialog = new CommonDialogWindow(string.Empty, "템플릿 수정", CommonDialogWindow.DialogType.Info)
        {
            Width = 560,
            Height = 640
        };

        var owner = Application.Current?
            .Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);
        if (owner != null && !ReferenceEquals(owner, dialog))
        {
            dialog.Owner = owner;
        }

        dialog.MessageBlock.Visibility = Visibility.Collapsed;
        dialog.PrimaryButton.Content = "저장";
        dialog.SecondaryButton.Content = "취소";

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var formPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        scroll.Content = formPanel;

        dialog.ContentHost.Children.Clear();
        dialog.ContentHost.Children.Add(scroll);

        Brush secondaryText = (Brush)(Application.Current?.TryFindResource("SecondaryText") ?? Brushes.Gray);
        Brush primaryText = (Brush)(Application.Current?.TryFindResource("PrimaryText") ?? Brushes.White);

        FrameworkElement CreateField(string labelText, Control control, Thickness? margin = null)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = margin ?? new Thickness(0, 0, 0, 16)
            };

            var label = new TextBlock
            {
                Text = labelText,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = secondaryText
            };

            panel.Children.Add(label);
            panel.Children.Add(control);
            return panel;
        }

        void StyleControl(Control control)
        {
            control.Padding = new Thickness(12, 8, 12, 8);
            control.Foreground = primaryText;
            control.SetResourceReference(Control.BackgroundProperty, "SurfaceBackground");
            control.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            control.BorderThickness = new Thickness(1);
        }

        var titleBox = new TextBox { Text = working.Title };
        StyleControl(titleBox);
        titleBox.TextChanged += (_, __) => working.Title = titleBox.Text;
        formPanel.Children.Add(CreateField("제목", titleBox));

        var typeCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(MissionTemplateType)),
            SelectedItem = working.TemplateType
        };
        StyleControl(typeCombo);
        formPanel.Children.Add(CreateField("미션 타입", typeCombo));

        var vehicleBox = new TextBox { Text = working.Vehicle };
        StyleControl(vehicleBox);
        vehicleBox.TextChanged += (_, __) => working.Vehicle = vehicleBox.Text;
        formPanel.Children.Add(CreateField("지게차", vehicleBox));

        var fromNodeBox = new TextBox { Text = working.FromNode };
        StyleControl(fromNodeBox);
        fromNodeBox.TextChanged += (_, __) => working.FromNode = fromNodeBox.Text;
        var fromSection = CreateField("From 노드", fromNodeBox);
        formPanel.Children.Add(fromSection);

        var toNodeBox = new TextBox { Text = working.ToNode };
        StyleControl(toNodeBox);
        toNodeBox.TextChanged += (_, __) => working.ToNode = toNodeBox.Text;
        formPanel.Children.Add(CreateField("To 노드", toNodeBox));

        var priorityBox = new TextBox { Text = working.Priority.ToString() };
        StyleControl(priorityBox);
        priorityBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        priorityBox.TextChanged += (_, __) =>
        {
            if (int.TryParse(priorityBox.Text, out var value))
            {
                working.Priority = value;
            }
        };
        formPanel.Children.Add(CreateField("우선순위", priorityBox));

        var descriptionBox = new TextBox
        {
            Text = working.PriorityDescription,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };
        StyleControl(descriptionBox);
        descriptionBox.TextChanged += (_, __) => working.PriorityDescription = descriptionBox.Text;
        formPanel.Children.Add(CreateField("설명", descriptionBox));

        var dynamicPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var dynamicLabel = new TextBlock
        {
            Text = "다이나믹 설정",
            FontSize = 12,
            Foreground = secondaryText,
            Margin = new Thickness(0, 0, 0, 8)
        };
        dynamicPanel.Children.Add(dynamicLabel);

        TextBox CreateNodeTypeBox(string label, string initialValue, Action<string> setter)
        {
            var box = new TextBox { Text = initialValue };
            StyleControl(box);
            box.TextChanged += (_, __) => setter(box.Text);
            dynamicPanel.Children.Add(CreateField(label, box, new Thickness(0, 0, 0, 8)));
            return box;
        }

        CreateNodeTypeBox("From 노드 타입", fromNodeType, value => fromNodeType = value);
        dynamicPanel.Children.Add(CreateField("From 노드 변수", CreateVarGrid(fromVars), new Thickness(0, 0, 0, 16)));

        CreateNodeTypeBox("To 노드 타입", toNodeType, value => toNodeType = value);
        dynamicPanel.Children.Add(CreateField("To 노드 변수", CreateVarGrid(toVars), new Thickness(0, 0, 0, 0)));

        formPanel.Children.Add(dynamicPanel);

        void UpdateVisibility()
        {
            fromSection.Visibility = working.TemplateType == MissionTemplateType.Moving
                ? Visibility.Collapsed
                : Visibility.Visible;
            dynamicPanel.Visibility = working.TemplateType == MissionTemplateType.Dynamic
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        typeCombo.SelectionChanged += (_, __) =>
        {
            if (typeCombo.SelectedItem is MissionTemplateType selected)
            {
                working.TemplateType = selected;
                UpdateVisibility();
            }
        };

        UpdateVisibility();

        DataGrid CreateVarGrid(ObservableCollection<DynamicNodeVar> source)
        {
            var grid = new DataGrid
            {
                ItemsSource = source,
                AutoGenerateColumns = false,
                CanUserAddRows = true,
                CanUserDeleteRows = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0,
                Height = 140,
                Margin = new Thickness(0, 4, 0, 0)
            };

            grid.SetResourceReference(Control.BackgroundProperty, "SurfaceBackground");
            grid.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            grid.BorderThickness = new Thickness(1);
            grid.Foreground = primaryText;

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Key",
                Binding = new Binding("Key") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Value",
                Binding = new Binding("Value") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            return grid;
        }

        var result = dialog.ShowDialog();
        if (result != true)
        {
            return;
        }

        ApplyTemplateChanges(template, working, fromNodeType, toNodeType, fromVars, toVars);
        RefreshTemplateTitles();
        _templateViewSource?.View?.Refresh();
        OnPropertyChanged(nameof(MissionTemplates));
        OnPropertyChanged(nameof(TemplateView));

        _ = _fileService.SaveTemplatesAsync(MissionTemplates.ToList());
        StatusText = $"템플릿 '{template.Title}' 이(가) 수정되었습니다.";
        CommandManager.InvalidateRequerySuggested();
    }

    private static MissionTemplate CloneTemplate(MissionTemplate source)
    {
        return new MissionTemplate
        {
            Title = source.Title,
            TemplateType = source.TemplateType,
            FromNode = source.FromNode,
            ToNode = source.ToNode,
            Vehicle = source.Vehicle,
            Priority = source.Priority,
            PriorityDescription = source.PriorityDescription,
            CreatedAt = source.CreatedAt,
            FromNodeConfig = source.FromNodeConfig != null ? CloneConfig(source.FromNodeConfig) : null,
            ToNodeConfig = source.ToNodeConfig != null ? CloneConfig(source.ToNodeConfig) : null
        };
    }

    private static DynamicNodeConfig CloneConfig(DynamicNodeConfig config)
    {
        return new DynamicNodeConfig
        {
            NodeType = config.NodeType,
            Vars = CloneVars(config.Vars)
        };
    }

    private static ObservableCollection<DynamicNodeVar> CloneVars(ObservableCollection<DynamicNodeVar>? source)
    {
        if (source == null)
        {
            return new ObservableCollection<DynamicNodeVar>();
        }

        return new ObservableCollection<DynamicNodeVar>(source.Select(v => new DynamicNodeVar
        {
            Key = v.Key,
            Value = v.Value
        }));
    }

    private static void ApplyTemplateChanges(
        MissionTemplate target,
        MissionTemplate working,
        string fromNodeType,
        string toNodeType,
        ObservableCollection<DynamicNodeVar> fromVars,
        ObservableCollection<DynamicNodeVar> toVars)
    {
        target.Title = working.Title;
        target.TemplateType = working.TemplateType;
        target.FromNode = working.FromNode;
        target.ToNode = working.ToNode;
        target.Vehicle = working.Vehicle;
        target.Priority = working.Priority;
        target.PriorityDescription = working.PriorityDescription;

        if (working.TemplateType == MissionTemplateType.Dynamic)
        {
            target.FromNodeConfig = new DynamicNodeConfig
            {
                NodeType = string.IsNullOrWhiteSpace(fromNodeType) ? "Dynamic_Lift" : fromNodeType,
                Vars = new ObservableCollection<DynamicNodeVar>(fromVars.Select(v => new DynamicNodeVar { Key = v.Key, Value = v.Value }))
            };

            target.ToNodeConfig = new DynamicNodeConfig
            {
                NodeType = string.IsNullOrWhiteSpace(toNodeType) ? "Dynamic_Lift" : toNodeType,
                Vars = new ObservableCollection<DynamicNodeVar>(toVars.Select(v => new DynamicNodeVar { Key = v.Key, Value = v.Value }))
            };
        }
        else
        {
            target.FromNodeConfig = null;
            target.ToNodeConfig = null;
        }
    }

    private void ExecuteAddFromVar()
    {
        FromNodeVars.Add(new DynamicNodeVar { Key = "", Value = "" });
    }

    private void ExecuteAddToVar()
    {
        ToNodeVars.Add(new DynamicNodeVar { Key = "", Value = "" });
    }

    private void ExecuteRemoveFromVar(object? parameter)
    {
        if (parameter is DynamicNodeVar var)
        {
            FromNodeVars.Remove(var);
        }
    }

    private void ExecuteRemoveToVar(object? parameter)
    {
        if (parameter is DynamicNodeVar var)
        {
            ToNodeVars.Remove(var);
        }
    }

    private async Task ExecuteDeleteTemplate(object? parameter)
    {
        if (parameter is not MissionTemplate template)
        {
            return;
        }

        var confirmed = CommonDialogWindow.ShowDialog(
            message: $"템플릿 \"{template.Title}\"을(를) 삭제할까요?",
            title: "템플릿 삭제 확인",
            type: CommonDialogWindow.DialogType.Warning);

        if (!confirmed)
        {
            return;
        }

        MissionTemplates.Remove(template);
        OnPropertyChanged(nameof(TemplateView));

        try
        {
            await _fileService.SaveTemplatesAsync(MissionTemplates.ToList());
            StatusText = "템플릿이 삭제되었습니다.";
        }
        catch (Exception ex)
        {
            StatusText = $"템플릿 삭제 실패: {ex.Message}";
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ExecuteImportTemplates(object? parameter)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
            Title = "미션 경로 파일 가져오기"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var importedTemplates = await _fileService.ImportTemplatesAsync(openFileDialog.FileName);

                foreach (var template in importedTemplates)
                {
                    MissionTemplates.Add(template);
                }

                OnPropertyChanged(nameof(TemplateView));
                await _fileService.SaveTemplatesAsync(MissionTemplates.ToList());
                StatusText = $"{importedTemplates.Count}개의 미션 경로를 가져왔습니다.";
            }
            catch (Exception ex)
            {
                StatusText = $"미션 경로 가져오기 실패: {ex.Message}";
            }
            finally
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private async Task ExecuteExportTemplates(object? parameter)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON 파일 (*.json)|*.json",
            Title = "미션 경로 파일 내보내기",
            FileName = $"mission_channel_{DateTime.Now:yyyyMMdd}.json"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                await _fileService.ExportTemplatesAsync(MissionTemplates.ToList(), saveFileDialog.FileName);
                StatusText = "미션 경로를 내보냈습니다.";
            }
            catch (Exception ex)
            {
                StatusText = $"미션 경로 내보내기 실패: {ex.Message}";
            }
            finally
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool CanExecuteVehicleCommand()
    {
        return !string.IsNullOrEmpty(SelectedVehicle);
    }

    private async Task ExecuteInsertVehicle()
    {
        if (string.IsNullOrEmpty(SelectedVehicle) || string.IsNullOrEmpty(SelectedInsertNode))
            return;

        StatusText = $"차량 {SelectedVehicle} 삽입 중...";
        
        try
        {
            await _antApiService.InsertVehicleAsync(SelectedVehicle, SelectedInsertNode);
            StatusText = $"차량 {SelectedVehicle}이(가) {SelectedInsertNode}에 삽입되었습니다.";
            
            await ExecuteRefreshVehicles();
        }
        catch (Exception ex)
        {
            StatusText = $"차량 삽입 실패: {ex.Message}";
        }
    }

    private async Task ExecuteExtractVehicle()
    {
        if (string.IsNullOrEmpty(SelectedVehicle))
            return;

        StatusText = $"차량 {SelectedVehicle} 추출 중...";
        
        try
        {
            await _antApiService.ExtractVehicleAsync(SelectedVehicle);
            StatusText = $"차량 {SelectedVehicle}이(가) 추출되었습니다.";
            
            await ExecuteRefreshVehicles();
        }
        catch (Exception ex)
        {
            StatusText = $"차량 추출 실패: {ex.Message}";
        }
    }

    private Task ExecuteRefreshVehicles() => RefreshVehiclesAsync(isAutoRefresh: false);

    private async Task RefreshVehiclesAsync(bool isAutoRefresh)
    {
        if (!isAutoRefresh)
        {
            StatusText = "차량 정보 새로고침 중...";
        }

        try
        {
            var newVehicles = await _antApiService.GetAllVehiclesAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var existingVehicles = Vehicles.ToDictionary(v => v.Name);
                var newVehiclesDict = newVehicles.ToDictionary(v => v.Name);

                // Remove vehicles that no longer exist
                var vehiclesToRemove = existingVehicles.Keys.Except(newVehiclesDict.Keys).ToList();
                var removedAlarmInfo = false;
                foreach (var vehicleName in vehiclesToRemove)
                {
                    Vehicles.Remove(existingVehicles[vehicleName]);
                    removedAlarmInfo |= _activeVehicleAlarms.Remove(vehicleName);
                }
                if (removedAlarmInfo)
                {
                    RefreshVehicleAlarmSnackbar();
                }

                // Add new vehicles and update existing ones
                foreach (var newVehicle in newVehicles)
                {
                    if (existingVehicles.TryGetValue(newVehicle.Name, out var existingVehicle))
                    {
                        // Update properties of the existing vehicle instance
                        UpdateVehicleProperties(existingVehicle, newVehicle);
                        UpdateVehicleAlarmToast(existingVehicle);
                    }
                    else
                    {
                        // Add new vehicle
                        Vehicles.Add(newVehicle);
                        UpdateVehicleAlarmToast(newVehicle);
                    }
                }

                ApplyVehicleSort(_vehicleSortProperty, _vehicleSortDirection, updateState: false);
            });

            if (!isAutoRefresh)
            {
                StatusText = "차량 정보 업데이트 완료";
            }
        }
        catch (Exception ex)
        {
            if (!isAutoRefresh)
            {
                StatusText = $"차량 새로고침 실패: {ex.Message}";
            }
            else
            {
                Debug.WriteLine($"차량 자동 갱신 오류: {ex.Message}");
            }
        }
    }

    private async Task ExecuteRefreshNodes()
    {
        StatusText = "노드 정보 새로고침 중...";
        
        try
        {
            var nodes = await _antApiService.GetAllNodesAsync();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableNodes.Clear();
                foreach (var node in nodes)
                {
                    AvailableNodes.Add(node);
                }
            });
            
            StatusText = "노드 정보 업데이트 완료";
        }
        catch (Exception ex)
        {
            StatusText = $"노드 새로고침 실패: {ex.Message}";
        }
    }

    private async Task ExecuteConnectToServer()
    {
        StatusText = "ANT 서버에 연결 중...";
        ConnectionStatus = "연결 상태: 연결 중...";

        var loginResponse = await _antApiService.LoginAsync(ServerUrl, Username, Password);

        if (loginResponse.Success)
        {
            IsConnected = true;
            ConnectionStatus = $"연결 상태: 연결됨 ({_antApiService.CurrentServerUrl})";
            StatusText = $"ANT 서버 연결 성공 - {loginResponse.DisplayName}";

            // 연결 성공 시 기본 데이터 로드
            await LoadInitialServerData();

            // 실시간 자동 갱신 타이머 시작
            StartRealtimeRefreshTimer();
        }
        else
        {
            IsConnected = false;
            ConnectionStatus = "연결 상태: 연결 실패";
            StatusText = loginResponse.ErrorMessage ?? "ANT 서버 연결 실패";
        }
    }

    private Task ExecuteRefreshAlarms() => RefreshAlarmsAsync(isAutoRefresh: false);

    private async Task RefreshAlarmsAsync(bool isAutoRefresh)
    {
        try
        {
            var newAlarms = await _antApiService.GetAlarmsAsync(AlarmLimit, AlarmSortAscending);

            Application.Current.Dispatcher.Invoke(() =>
            {
                var existingAlarms = Alarms.ToDictionary(a => a.Uuid);
                var newAlarmsDict = newAlarms.ToDictionary(a => a.Uuid);

                // Remove alarms that are no longer in the list
                var alarmsToRemove = existingAlarms.Keys.Except(newAlarmsDict.Keys).ToList();
                foreach (var uuid in alarmsToRemove)
                {
                    Alarms.Remove(existingAlarms[uuid]);
                }

                // Add new alarms and update existing ones
                foreach (var newAlarm in newAlarms)
                {
                    if (existingAlarms.TryGetValue(newAlarm.Uuid, out var existingAlarm))
                    {
                        // Update properties of the existing alarm instance
                        UpdateAlarmProperties(existingAlarm, newAlarm);
                    }
                    else
                    {
                        // Add new alarm
                        Alarms.Add(newAlarm);
                    }
                }

                // Refreshing the view is still necessary for filtering/sorting to apply correctly
                RefreshAlarmFilter();
            });
        }
        catch (Exception ex)
        {
            if (!isAutoRefresh)
            {
                StatusText = $"알람 새로고침 실패: {ex.Message}";
            }
            else
            {
                Debug.WriteLine($"알람 자동 갱신 오류: {ex.Message}");
            }
        }
    }

    private async Task ExecuteRefreshMap()
    {
        StatusText = "맵 정보 로드 중...";

        try
        {
            var mapDataList = await _antApiService.GetMapDataAsync();

            if (mapDataList == null)
            {
                Services.MapLogger.LogError("mapDataList is NULL!");
                StatusText = "맵 정보가 null입니다";
                return;
            }

            if (mapDataList.Count == 0)
            {
                Services.MapLogger.LogError("mapDataList is EMPTY!");
                StatusText = "맵 정보가 비어있습니다";
                return;
            }

            Services.MapLogger.LogSection("ViewModel - Map Data Summary");
            foreach (var map in mapDataList)
            {
                Services.MapLogger.Log($"Map: {map.Alias} (ID: {map.Id}) - {map.Layers.Count} layers");
                foreach (var layer in map.Layers)
                {
                    Services.MapLogger.Log($"  Layer '{layer.Name}': {layer.Nodes.Count} nodes, {layer.Links.Count} links");
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Services.MapLogger.Log($"Setting MapData property with {mapDataList.Count} maps");
                MapData = mapDataList;
                Services.MapLogger.Log($"MapData property set successfully");
            });

            var logPath = Services.MapLogger.GetLogFilePath();
            StatusText = $"맵 로드 완료 ({mapDataList.Count}개) - 로그: {logPath}";
            Services.MapLogger.Log($"Status: {StatusText}");
        }
        catch (Exception ex)
        {
            StatusText = $"맵 정보 로드 실패: {ex.Message}";
            Services.MapLogger.LogError("Map load failed", ex);
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadInitialDataAsync()
    {
        try
        {
            var templates = await _fileService.LoadTemplatesAsync();
            MissionTemplates = new ObservableCollection<MissionTemplate>(templates);
        }
        catch (Exception ex)
        {
            StatusText = $"초기 데이터 로드 실패: {ex.Message}";
        }
    }

    private async Task EnsureFullMissionHistoryAsync(bool force = false, bool isAutoRefresh = false)
    {
        if (_fullMissionHistoryLoadTask != null)
        {
            if (!_fullMissionHistoryLoadTask.IsCompleted)
            {
                await _fullMissionHistoryLoadTask;
                return;
            }

            _fullMissionHistoryLoadTask = null;
        }

        if (!force && _lastFullMissionHistoryRefresh != DateTime.MinValue)
        {
            var elapsed = DateTime.Now - _lastFullMissionHistoryRefresh;
            if (elapsed < _fullMissionHistoryRefreshInterval)
            {
                return;
            }
        }

        var loadTask = LoadFullMissionHistoryAsync(isAutoRefresh);
        _fullMissionHistoryLoadTask = loadTask;

        try
        {
            await loadTask;
        }
        finally
        {
            if (ReferenceEquals(_fullMissionHistoryLoadTask, loadTask))
            {
                _fullMissionHistoryLoadTask = null;
            }
        }
    }

    private async Task LoadFullMissionHistoryAsync(bool isAutoRefresh = false)
    {
        if (!isAutoRefresh)
        {
            StatusText = "전체 미션 조회 중...";
        }

        try
        {
            int maxMissionId = Missions
                .Select(m => m.MissionIdSortValue)
                .Where(id => id > int.MinValue)
                .DefaultIfEmpty(int.MinValue)
                .Max();

            var latestBatch = await _antApiService.GetAllMissionsUnfilteredAsync();

            if (latestBatch.Any())
            {
                var latestMax = latestBatch
                    .Select(m => m.MissionIdSortValue)
                    .Where(id => id > int.MinValue)
                    .DefaultIfEmpty(int.MinValue)
                    .Max();

                if (latestMax > maxMissionId)
                {
                    maxMissionId = latestMax;
                }
            }

            if (maxMissionId == int.MinValue)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Missions.Clear();
                    if (latestBatch.Any())
                    {
                        foreach (var mission in latestBatch.OrderByDescending(m => m.MissionIdSortValue))
                        {
                            Missions.Add(mission);
                        }
                    }
                    RefreshMissionFilter();
                });

                _isFullMissionHistoryLoaded = false;

                if (!isAutoRefresh)
                {
                    StatusText = latestBatch.Count > 0
                        ? $"전체 범위 미션 ID를 계산할 수 없어 최근 {latestBatch.Count}건만 표시합니다"
                        : "전체 미션이 없습니다";
                }
                else
                {
                    Debug.WriteLine("전체 미션 ID 계산 실패 - 최신 100건만 표시");
                }

                return;
            }

            var fullMissions = await _antApiService.GetAllMissionsWithRangeAsync(maxMissionId);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Missions.Clear();
                foreach (var mission in fullMissions.OrderByDescending(m => m.MissionIdSortValue))
                {
                    Missions.Add(mission);
                }
                RefreshMissionFilter();
            });

            _isFullMissionHistoryLoaded = true;

            if (!isAutoRefresh)
            {
                StatusText = $"전체 미션 {fullMissions.Count}건 조회 완료";
            }
        }
        catch (Exception ex)
        {
            _isFullMissionHistoryLoaded = false;

            if (!isAutoRefresh)
            {
                StatusText = $"전체 미션 조회 실패: {ex.Message}";
            }
            else
            {
                Debug.WriteLine($"전체 미션 자동 갱신 오류: {ex.Message}");
            }
        }
        finally
        {
            _lastFullMissionHistoryRefresh = DateTime.Now;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RefreshMissionFilter()
    {
        MissionView?.Refresh();
        ApplyMissionSort();
        UpdateMissionStatistics();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplyMissionSort()
    {
        ApplyMissionSort(_missionSortProperty, _missionSortDirection, updateState: false);
    }

    public void ApplyMissionColumnSort(string propertyName, ListSortDirection direction)
    {
        ApplyMissionSort(propertyName, direction, updateState: true);
    }

    private void ApplyMissionSort(string propertyName, ListSortDirection direction, bool updateState)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        if (updateState)
        {
            _missionSortProperty = propertyName;
            _missionSortDirection = direction;
        }

        var sortDescriptions = _missionViewSource.SortDescriptions;
        sortDescriptions.Clear();
        sortDescriptions.Add(new SortDescription(propertyName, direction));
    }

    public void ApplyVehicleColumnSort(string propertyName, ListSortDirection direction)
    {
        ApplyVehicleSort(propertyName, direction, updateState: true);
    }

    private void ApplyVehicleSort(string propertyName, ListSortDirection direction, bool updateState)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        if (updateState)
        {
            _vehicleSortProperty = propertyName;
            _vehicleSortDirection = direction;
        }

        var sortDescriptions = _vehicleViewSource.SortDescriptions;
        sortDescriptions.Clear();
        sortDescriptions.Add(new SortDescription(propertyName, direction));
        _vehicleViewSource.View?.Refresh();
    }

    private void OnMissionFilter(object? sender, FilterEventArgs e)
    {
        if (e.Item is not MissionInfo mission)
        {
            e.Accepted = false;
            return;
        }

        var filter = SelectedMissionFilter ?? MissionFilterOptions.FirstOrDefault();
        if (filter != null && !filter.ContainsState(mission.NavigationState))
        {
            e.Accepted = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(MissionSearchTerm))
        {
            var term = MissionSearchTerm.Trim();
            var comparison = StringComparison.OrdinalIgnoreCase;

            bool Matches(string? source) => !string.IsNullOrEmpty(source) && source.IndexOf(term, comparison) >= 0;

            if (!Matches(mission.MissionId)
                && !Matches(mission.MissionType)
                && !Matches(mission.FromNode)
                && !Matches(mission.ToNode)
                && !Matches(mission.AssignedVehicle)
                && !Matches(mission.NavigationStateText)
                && !Matches(mission.TransportStateText)
                && !Matches(mission.CreatedAtDisplay)
                && !Matches(mission.Priority.ToString())
                && !Matches(mission.MissionIdSortValue.ToString()))
            {
                e.Accepted = false;
                return;
            }
        }

        if (!(filter?.IsDefault ?? false))
        {
            var timestamp = mission.QueueTimestamp;
            var startBoundary = GetMissionFilterStartBoundary();
            var endBoundary = GetMissionFilterEndBoundary();

            if (endBoundary < startBoundary)
            {
                (startBoundary, endBoundary) = (endBoundary, startBoundary);
            }

            if (timestamp < startBoundary || timestamp > endBoundary)
            {
                e.Accepted = false;
                return;
            }
        }

        e.Accepted = true;
    }

    private DateTime GetMissionFilterStartBoundary()
    {
        if (!MissionFilterStart.HasValue)
        {
            return DateTime.MinValue;
        }

        var value = MissionFilterStart.Value;
        return value.TimeOfDay == TimeSpan.Zero ? value.Date : value;
    }

    private DateTime GetMissionFilterEndBoundary()
    {
        if (!MissionFilterEnd.HasValue)
        {
            return DateTime.MaxValue;
        }

        var value = MissionFilterEnd.Value;
        if (value.TimeOfDay == TimeSpan.Zero)
        {
            return value.Date.AddDays(1).AddTicks(-1);
        }

        return value;
    }
    private void UpdateMissionStatistics()
    {
        IEnumerable<MissionInfo> missionSource =
            MissionView != null
                ? MissionView.Cast<MissionInfo>()
                : Missions;

        var missionList = missionSource.ToList();

        TotalMissions = missionList.Count;
        RunningMissions = missionList.Count(m => m.NavigationState == 3); // Started
        PendingMissions = missionList.Count(m => m.NavigationState == 0 || m.NavigationState == 1); // Received or Accepted
        CompletedMissions = missionList.Count(m => m.NavigationState == 4); // Completed
        CancelledMissions = missionList.Count(m => m.NavigationState == 5); // Cancelled
    }

    private void ClearAlarmSearch()
    {
        var defaultOption = AlarmSearchColumns.FirstOrDefault();
        if (defaultOption != null)
        {
            SelectedAlarmSearchColumn = defaultOption.Key;
        }

        AlarmSearchTerm = string.Empty;
    }

    private bool CanClearAlarmSearch()
    {
        var defaultOption = AlarmSearchColumns.FirstOrDefault();
        var isDefaultColumn = defaultOption == null || string.Equals(SelectedAlarmSearchColumn, defaultOption.Key, StringComparison.Ordinal);
        return !string.IsNullOrWhiteSpace(AlarmSearchTerm) || !isDefaultColumn;
    }

    private void ClearMissionSearch()
    {
        MissionSearchTerm = string.Empty;
    }

    private void ShowGeneralSnackbar(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ShowSnackbarInternal(message, TimeSpan.FromSeconds(3), persistent: false);
    }

    private void ShowVehicleSnackbar(string message)
    {
        _vehicleSnackbarMessage = message;
        _isVehicleSnackbarActive = true;

        // If a general snackbar is currently visible, wait for it to finish before showing the vehicle warning again.
        if (!_isSnackbarPersistent && IsSnackbarVisible)
        {
            return;
        }

        ShowSnackbarInternal(message, duration: null, persistent: true);
    }

    private void HideVehicleSnackbar()
    {
        _isVehicleSnackbarActive = false;
        _vehicleSnackbarMessage = string.Empty;

        if (_isSnackbarPersistent)
        {
            HideSnackbarInternal();
        }
    }

    private void ShowSnackbarInternal(string message, TimeSpan? duration, bool persistent)
    {
        if (_snackbarCts != null)
        {
            _snackbarCts.Cancel();
            _snackbarCts.Dispose();
            _snackbarCts = null;
        }

        SnackbarMessage = message;
        IsSnackbarVisible = true;
        _isSnackbarPersistent = persistent;

        if (!persistent)
        {
            var cts = new CancellationTokenSource();
            _snackbarCts = cts;
            _ = DismissSnackbarAfterAsync(duration ?? TimeSpan.FromSeconds(3), cts);
        }
    }

    private void HideSnackbarInternal()
    {
        if (_snackbarCts != null)
        {
            _snackbarCts.Cancel();
            _snackbarCts.Dispose();
            _snackbarCts = null;
        }

        _isSnackbarPersistent = false;
        IsSnackbarVisible = false;
        SnackbarMessage = string.Empty;
    }

    private async Task DismissSnackbarAfterAsync(TimeSpan delay, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delay, cts.Token);
        }
        catch (TaskCanceledException)
        {
            cts.Dispose();
            return;
        }

        if (_snackbarCts != cts)
        {
            cts.Dispose();
            return;
        }

        HideSnackbarInternal();
        cts.Dispose();

        if (_isVehicleSnackbarActive && !string.IsNullOrEmpty(_vehicleSnackbarMessage))
        {
            ShowVehicleSnackbar(_vehicleSnackbarMessage);
        }
    }

    private void UpdateVehicleAlarmToast(Vehicle vehicle)
    {
        var firstAlarm = vehicle.Alarms?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));

        if (string.IsNullOrWhiteSpace(firstAlarm))
        {
            if (_activeVehicleAlarms.Remove(vehicle.Name))
            {
                RefreshVehicleAlarmSnackbar();
            }
            return;
        }

        _activeVehicleAlarms[vehicle.Name] = firstAlarm;
        RefreshVehicleAlarmSnackbar();
    }

    private void RefreshVehicleAlarmSnackbar()
    {
        if (_activeVehicleAlarms.Count == 0)
        {
            HideVehicleSnackbar();
            return;
        }

        var firstPair = _activeVehicleAlarms.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase).First();
        var message = BuildVehicleAlarmMessage(firstPair.Key, firstPair.Value);

        if (_isSnackbarPersistent && string.Equals(SnackbarMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        if (!_isSnackbarPersistent && IsSnackbarVisible)
        {
            // Let the current general snackbar finish; it will restore the vehicle warning afterwards.
            _vehicleSnackbarMessage = message;
            _isVehicleSnackbarActive = true;
            return;
        }

        ShowVehicleSnackbar(message);
    }

    private string BuildVehicleAlarmMessage(string vehicleName, string alarmText)
    {
        return $"차량 {vehicleName} 알람: {alarmText}";
    }

    private bool CanClearMissionSearch()
    {
        return !string.IsNullOrWhiteSpace(MissionSearchTerm);
    }

    private void RefreshAlarmFilter()
    {
        AlarmView?.Refresh();
    }

    private void OnMissionTemplatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshTemplateTitles();
        CommandManager.InvalidateRequerySuggested();
        OnPropertyChanged(nameof(TemplateView));
    }

    private void OnTemplateFilter(object sender, FilterEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedTemplateFilter) || _selectedTemplateFilter == "전체")
        {
            e.Accepted = true;
            return;
        }

        if (e.Item is MissionTemplate template)
        {
            e.Accepted = template.Title == _selectedTemplateFilter;
        }
    }

    private void RefreshTemplateTitles()
    {
        var titles = new HashSet<string> { "전체" };
        foreach (var template in _missionTemplates)
        {
            if (!string.IsNullOrWhiteSpace(template.Title))
            {
                titles.Add(template.Title);
            }
        }

        TemplateTitles = new ObservableCollection<string>(titles.OrderBy(t => t == "전체" ? "" : t));

        if (string.IsNullOrWhiteSpace(_selectedTemplateFilter) || !titles.Contains(_selectedTemplateFilter))
        {
            SelectedTemplateFilter = "전체";
        }
    }

    public void ApplyAlarmSort(IReadOnlyList<SortDescription> sortDescriptions)
    {
        if (sortDescriptions == null || sortDescriptions.Count == 0)
        {
            ApplyDefaultAlarmSort(force: true);
            return;
        }

        var sorts = new List<SortDescription>(sortDescriptions.Count + 1);
        foreach (var description in sortDescriptions)
        {
            if (string.IsNullOrWhiteSpace(description.PropertyName))
            {
                continue;
            }

            sorts.Add(new SortDescription(description.PropertyName, description.Direction));
        }

        if (!sorts.Any(sd => string.Equals(sd.PropertyName, TimestampPropertyName, StringComparison.Ordinal)))
        {
            var timestampDirection = _alarmSortAscending
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;

            sorts.Add(new SortDescription(TimestampPropertyName, timestampDirection));
        }

        if (sorts.Count > 0 && string.Equals(sorts[0].PropertyName, TimestampPropertyName, StringComparison.Ordinal))
        {
            var newAscending = sorts[0].Direction == ListSortDirection.Ascending;
            if (_alarmSortAscending != newAscending)
            {
                _alarmSortAscending = newAscending;
                OnPropertyChanged(nameof(AlarmSortAscending));
            }
        }

        SetAlarmSortDescriptions(sorts, force: true);
    }

    public void ApplyAlarmColumnSort(string propertyName, ListSortDirection direction)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        ApplyAlarmSort(new List<SortDescription>
        {
            new SortDescription(propertyName, direction)
        });
    }

    private void ApplyDefaultAlarmSort(bool force)
    {
        var direction = _alarmSortAscending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        SetAlarmSortDescriptions(new[]
        {
            new SortDescription(TimestampPropertyName, direction)
        }, force);
    }

    private void SetAlarmSortDescriptions(IEnumerable<SortDescription> sortDescriptions, bool force)
    {
        _alarmSortDescriptions.Clear();

        foreach (var sortDescription in sortDescriptions)
        {
            if (string.IsNullOrWhiteSpace(sortDescription.PropertyName))
            {
                continue;
            }

            if (_alarmSortDescriptions.Any(existing =>
                    string.Equals(existing.PropertyName, sortDescription.PropertyName, StringComparison.Ordinal)))
            {
                continue;
            }

            _alarmSortDescriptions.Add(new SortDescription(sortDescription.PropertyName, sortDescription.Direction));
        }

        if (_alarmSortDescriptions.Count == 0)
        {
            ApplyDefaultAlarmSort(force: true);
            return;
        }

        UpdateAlarmSort(force);
    }

    private void UpdateAlarmSort(bool force = false)
    {
        var view = AlarmView;
        if (view == null)
        {
            return;
        }

        using (view.DeferRefresh())
        {
            if (force)
            {
                view.SortDescriptions.Clear();
            }

            for (var index = 0; index < _alarmSortDescriptions.Count; index++)
            {
                var desired = _alarmSortDescriptions[index];
                var existingIndex = -1;

                for (int i = 0; i < view.SortDescriptions.Count; i++)
                {
                    if (string.Equals(view.SortDescriptions[i].PropertyName, desired.PropertyName, StringComparison.Ordinal))
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    var needsDirectionUpdate = view.SortDescriptions[existingIndex].Direction != desired.Direction;
                    var needsReorder = existingIndex != index;

                    if (needsDirectionUpdate || needsReorder)
                    {
                        view.SortDescriptions.RemoveAt(existingIndex);
                        view.SortDescriptions.Insert(Math.Min(index, view.SortDescriptions.Count), desired);
                    }
                }
                else
                {
                    view.SortDescriptions.Insert(Math.Min(index, view.SortDescriptions.Count), desired);
                }
            }

            if (force)
            {
                for (int i = view.SortDescriptions.Count - 1; i >= 0; i--)
                {
                    if (!_alarmSortDescriptions.Any(sd =>
                            string.Equals(sd.PropertyName, view.SortDescriptions[i].PropertyName, StringComparison.Ordinal)))
                    {
                        view.SortDescriptions.RemoveAt(i);
                    }
                }
            }
        }
    }

    private void OnAlarmFilter(object? sender, FilterEventArgs e)
    {
        if (e.Item is not AlarmInfo alarm)
        {
            e.Accepted = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(AlarmSearchTerm))
        {
            e.Accepted = true;
            return;
        }

        var term = AlarmSearchTerm.Trim();
        var comparison = StringComparison.OrdinalIgnoreCase;

        if (string.Equals(SelectedAlarmSearchColumn, "All", StringComparison.OrdinalIgnoreCase))
        {
            e.Accepted = ContainsTerm(alarm.StateText, term, comparison)
                         || ContainsTerm(alarm.SourceTypeText, term, comparison)
                         || ContainsTerm(alarm.SourceId, term, comparison)
                         || ContainsTerm(alarm.EventDisplayName, term, comparison)
                         || ContainsTerm(alarm.AlarmMessage, term, comparison)
                         || ContainsTerm(alarm.EventCount.ToString(), term, comparison)
                         || ContainsTerm(alarm.FirstEventAtText, term, comparison)
                         || ContainsTerm(alarm.LastEventAtText, term, comparison)
                         || ContainsTerm(alarm.TimestampText, term, comparison);
            return;
        }

        var value = GetAlarmValueByKey(alarm, SelectedAlarmSearchColumn);
        e.Accepted = ContainsTerm(value, term, comparison);
    }

    private static bool ContainsTerm(string? source, string term, StringComparison comparison)
    {
        return !string.IsNullOrEmpty(source) && source.IndexOf(term, comparison) >= 0;
    }

    private static string? GetAlarmValueByKey(AlarmInfo alarm, string key)
    {
        return key switch
        {
            nameof(AlarmInfo.StateText) => alarm.StateText,
            nameof(AlarmInfo.SourceTypeText) => alarm.SourceTypeText,
            nameof(AlarmInfo.SourceId) => alarm.SourceId,
            nameof(AlarmInfo.EventDisplayName) => alarm.EventDisplayName,
            nameof(AlarmInfo.AlarmMessage) => alarm.AlarmMessage,
            nameof(AlarmInfo.EventCount) => alarm.EventCount.ToString(),
            nameof(AlarmInfo.FirstEventAtText) => alarm.FirstEventAtText,
            nameof(AlarmInfo.LastEventAtText) => alarm.LastEventAtText,
            nameof(AlarmInfo.TimestampText) => alarm.TimestampText,
            nameof(AlarmInfo.Uuid) => alarm.Uuid,
            _ => null
        };
    }

    private void UpdateVehicleProperties(Vehicle existing, Vehicle newVehicle)
    {
        existing.OperatingState = newVehicle.OperatingState;
        existing.Location = newVehicle.Location;
        existing.MissionId = newVehicle.MissionId;
        existing.BatteryLevel = newVehicle.BatteryLevel;
        existing.Alarms = newVehicle.Alarms;
        existing.LastUpdate = newVehicle.LastUpdate;
        existing.IpAddress = newVehicle.IpAddress;
        existing.IsSimulated = newVehicle.IsSimulated;
        existing.IsLoaded = newVehicle.IsLoaded;
        existing.Payload = newVehicle.Payload;
        existing.Coordinates = newVehicle.Coordinates;
        existing.Course = newVehicle.Course;
        existing.CurrentNodeName = newVehicle.CurrentNodeName;
        existing.TraveledDistance = newVehicle.TraveledDistance;
        existing.CumulativeUptime = newVehicle.CumulativeUptime;
        existing.Path = newVehicle.Path;
        existing.VehicleState = newVehicle.VehicleState;
        existing.Coverage = newVehicle.Coverage;
        existing.Port = newVehicle.Port;
        existing.IsOmni = newVehicle.IsOmni;
        existing.ForceCharge = newVehicle.ForceCharge;
        existing.ActionName = newVehicle.ActionName;
        existing.ActionSourceId = newVehicle.ActionSourceId;
        existing.ArrivalDate = newVehicle.ArrivalDate;
        existing.AbsArrivalDate = newVehicle.AbsArrivalDate;
        existing.ActionNodeId = newVehicle.ActionNodeId;
        existing.CurrentNodeId = newVehicle.CurrentNodeId;
        existing.MapName = newVehicle.MapName;
        existing.GroupName = newVehicle.GroupName;
        existing.Uncertainty = newVehicle.Uncertainty;
        existing.ConnectionOk = newVehicle.ConnectionOk;
        existing.BatteryMaxTemp = newVehicle.BatteryMaxTemp;
        existing.BatteryVoltage = newVehicle.BatteryVoltage;
        existing.VehicleType = newVehicle.VehicleType;
        existing.LockUuid = newVehicle.LockUuid;
        existing.LockOwnerApp = newVehicle.LockOwnerApp;
        existing.LockOwnerPc = newVehicle.LockOwnerPc;
        existing.LockOwnerUser = newVehicle.LockOwnerUser;
        existing.MissionFrom = newVehicle.MissionFrom;
        existing.MissionTo = newVehicle.MissionTo;
        existing.MissionFinal = newVehicle.MissionFinal;
        existing.Errors = newVehicle.Errors;
        existing.MissionBlocked = newVehicle.MissionBlocked;
        existing.ActionSourceType = newVehicle.ActionSourceType;
        existing.BodyShape = newVehicle.BodyShape;
        existing.TrafficInfo = newVehicle.TrafficInfo;
        existing.MissionProgress = newVehicle.MissionProgress;
        existing.ErrorBits = newVehicle.ErrorBits;
        existing.SharedMemoryOut = newVehicle.SharedMemoryOut;
        existing.SharedMemoryIn = newVehicle.SharedMemoryIn;
        existing.VehicleShape = newVehicle.VehicleShape;
        existing.ErrorDetailsLabel = newVehicle.ErrorDetailsLabel;
        existing.Messages = newVehicle.Messages;
        existing.ErrorDetails = newVehicle.ErrorDetails;
    }

    private void UpdateAlarmProperties(AlarmInfo existing, AlarmInfo newAlarm)
    {
        existing.SourceId = newAlarm.SourceId;
        existing.SourceType = newAlarm.SourceType;
        existing.EventName = newAlarm.EventName;
        existing.AlarmMessage = newAlarm.AlarmMessage;
        existing.EventCount = newAlarm.EventCount;
        existing.FirstEventAt = newAlarm.FirstEventAt;
        existing.LastEventAt = newAlarm.LastEventAt;
        existing.Timestamp = newAlarm.Timestamp;
        existing.State = newAlarm.State;
        existing.ClosedAt = newAlarm.ClosedAt;
        existing.ClearedAt = newAlarm.ClearedAt;
    }

    private void UpdateMissionProperties(MissionInfo existing, MissionInfo newMission)
    {
        existing.MissionType = newMission.MissionType;
        existing.FromNode = newMission.FromNode;
        existing.ToNode = newMission.ToNode;
        existing.AssignedVehicle = newMission.AssignedVehicle;
        existing.NavigationState = newMission.NavigationState;
        existing.TransportState = newMission.TransportState;
        existing.Priority = newMission.Priority;

        if (!newMission.ArrivingTime.HasValue && newMission.CreatedAt != DateTime.MinValue)
        {
            newMission.ArrivingTime = newMission.CreatedAt;
        }

        if (newMission.CreatedAt != DateTime.MinValue)
        {
            existing.CreatedAt = newMission.CreatedAt;
        }

        if (newMission.ArrivingTime.HasValue)
        {
            existing.ArrivingTime = newMission.ArrivingTime;
        }

        existing.CreatedAtDisplay = newMission.CreatedAtDisplay;
    }

    private void UpdateFilteredFromNodes()
    {
        if (string.IsNullOrWhiteSpace(_fromNodeSearchText))
        {
            FilteredFromNodes = new ObservableCollection<NodeInfo>(_availableNodes);
        }
        else
        {
            var filtered = _availableNodes
                .Where(n => n.Name.StartsWith(_fromNodeSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            FilteredFromNodes = new ObservableCollection<NodeInfo>(filtered);
        }
    }

    private void UpdateFilteredToNodes()
    {
        if (string.IsNullOrWhiteSpace(_toNodeSearchText))
        {
            FilteredToNodes = new ObservableCollection<NodeInfo>(_availableNodes);
        }
        else
        {
            var filtered = _availableNodes
                .Where(n => n.Name.StartsWith(_toNodeSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            FilteredToNodes = new ObservableCollection<NodeInfo>(filtered);
        }
    }

    private void UpdateFilteredInsertNodes()
    {
        if (string.IsNullOrWhiteSpace(_insertNodeSearchText))
        {
            FilteredInsertNodes = new ObservableCollection<NodeInfo>(_availableNodes);
        }
        else
        {
            var filtered = _availableNodes
                .Where(n => n.Name.StartsWith(_insertNodeSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            FilteredInsertNodes = new ObservableCollection<NodeInfo>(filtered);
        }
    }

    #endregion
}

public sealed class MissionFilterOption
{
    private readonly IReadOnlyList<int>? _navigationStates;

    public MissionFilterOption(string displayName, IReadOnlyList<int>? navigationStates, bool isDefault = false)
    {
        DisplayName = displayName;
        _navigationStates = navigationStates;
        IsDefault = isDefault;
    }

    public string DisplayName { get; }

    public bool IsDefault { get; }

    public bool IsAll => _navigationStates == null;

    public bool RequiresFullHistory => !IsDefault;

    public bool ContainsState(int state)
    {
        if (IsAll)
        {
            return true;
        }

        return _navigationStates?.Contains(state) ?? false;
    }
}

public sealed class AlarmSearchOption
{
    public AlarmSearchOption(string displayName, string key)
    {
        DisplayName = displayName;
        Key = key;
    }

    public string DisplayName { get; }
    public string Key { get; }
}
