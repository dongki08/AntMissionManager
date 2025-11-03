using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AntMissionManager.Models;
using AntMissionManager.Services;
using AntMissionManager.Views;
using Microsoft.Win32;

namespace AntMissionManager.ViewModels;

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
    private readonly CollectionViewSource _missionViewSource;
    private ObservableCollection<MissionFilterOption> _missionFilterOptions = new();
    private MissionFilterOption? _selectedMissionFilter;
    private DateTime? _missionFilterStart;
    private DateTime? _missionFilterEnd;
    private const int CompletedMissionDefaultOffsetMinutes = 3;
    private string _missionSortProperty = nameof(MissionInfo.MissionIdSortValue);
    private ListSortDirection _missionSortDirection = ListSortDirection.Descending;

    // Mission Router Properties
    private ObservableCollection<MissionRoute> _routes = new();
    private ObservableCollection<RouteNode> _selectedNodes = new();
    private string _routeName = string.Empty;
    private string _selectedMissionType = "Transport";
    private string _selectedNode = string.Empty;

    // Vehicle Management Properties
    private ObservableCollection<Vehicle> _vehicles = new();
    private readonly CollectionViewSource _vehicleViewSource;
    private string _selectedVehicle = string.Empty;
    private string _selectedInsertNode = string.Empty;
    private ObservableCollection<NodeInfo> _availableNodes = new();
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

        _vehicleViewSource = new CollectionViewSource { Source = _vehicles };
        ApplyVehicleSort(_vehicleSortProperty, _vehicleSortDirection, updateState: false);

        MissionFilterOptions = new ObservableCollection<MissionFilterOption>(new[]
        {
            new MissionFilterOption("기본 (대기/진행/완료)", new[] { 1, 3, 4 }, isDefault: true),
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
        LoadInitialData();

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
            await RefreshMissionsAsync(isAutoRefresh: true);
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
            }
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

    // Mission Router
    public ObservableCollection<MissionRoute> Routes
    {
        get => _routes;
        set => SetProperty(ref _routes, value);
    }

    public ObservableCollection<RouteNode> SelectedNodes
    {
        get => _selectedNodes;
        set => SetProperty(ref _selectedNodes, value);
    }

    public string RouteName
    {
        get => _routeName;
        set => SetProperty(ref _routeName, value);
    }

    public string SelectedMissionType
    {
        get => _selectedMissionType;
        set => SetProperty(ref _selectedMissionType, value);
    }

    public string SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
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
        set => SetProperty(ref _availableNodes, value);
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
    
    public ICommand AddNodeCommand { get; private set; } = null!;
    public ICommand RemoveNodeCommand { get; private set; } = null!;
    public ICommand SaveRouteCommand { get; private set; } = null!;
    public ICommand ClearRouteCommand { get; private set; } = null!;
    public ICommand ImportRoutesCommand { get; private set; } = null!;
    public ICommand ExportRoutesCommand { get; private set; } = null!;
    public ICommand EditRouteCommand { get; private set; } = null!;
    public ICommand DeleteRouteCommand { get; private set; } = null!;
    
    public ICommand InsertVehicleCommand { get; private set; } = null!;
    public ICommand ExtractVehicleCommand { get; private set; } = null!;
    public ICommand RefreshVehiclesCommand { get; private set; } = null!;
    public ICommand RefreshNodesCommand { get; private set; } = null!;
    public ICommand ConnectToServerCommand { get; private set; } = null!;

    public ICommand RefreshAlarmsCommand { get; private set; } = null!;
    public ICommand ClearAlarmSearchCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        LogoutCommand = new RelayCommand(ExecuteLogout);
        RefreshMissionsCommand = new AsyncRelayCommand(ExecuteRefreshMissions);
        CancelMissionCommand = new RelayCommand(ExecuteCancelMission);
        
        AddNodeCommand = new RelayCommand(ExecuteAddNode, () => !string.IsNullOrEmpty(SelectedNode));
        RemoveNodeCommand = new RelayCommand(ExecuteRemoveNode);
        SaveRouteCommand = new RelayCommand(ExecuteSaveRoute, CanExecuteSaveRoute);
        ClearRouteCommand = new RelayCommand(ExecuteClearRoute);
        ImportRoutesCommand = new RelayCommand(ExecuteImportRoutes);
        ExportRoutesCommand = new RelayCommand(ExecuteExportRoutes);
        EditRouteCommand = new RelayCommand(ExecuteEditRoute);
        DeleteRouteCommand = new RelayCommand(ExecuteDeleteRoute);
        
        InsertVehicleCommand = new AsyncRelayCommand(ExecuteInsertVehicle, CanExecuteVehicleCommand);
        ExtractVehicleCommand = new AsyncRelayCommand(ExecuteExtractVehicle, CanExecuteVehicleCommand);
        RefreshVehiclesCommand = new AsyncRelayCommand(ExecuteRefreshVehicles);
    RefreshNodesCommand = new AsyncRelayCommand(ExecuteRefreshNodes);
    ConnectToServerCommand = new AsyncRelayCommand(ExecuteConnectToServer);

    RefreshAlarmsCommand = new AsyncRelayCommand(ExecuteRefreshAlarms);
    ClearAlarmSearchCommand = new RelayCommand(_ => ClearAlarmSearch(), _ => CanClearAlarmSearch());
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

    private Task ExecuteRefreshMissions() => RefreshMissionsAsync(isAutoRefresh: false);

    private async Task RefreshMissionsAsync(bool isAutoRefresh)
    {
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
    }

    private void ExecuteCancelMission(object? parameter)
    {
        if (parameter is string missionId)
        {
            // TODO: 미션 취소 로직 구현
            StatusText = $"미션 {missionId} 취소 요청됨";
        }
    }

    private void ExecuteAddNode()
    {
        if (!string.IsNullOrEmpty(SelectedNode))
        {
            var node = new RouteNode
            {
                Index = SelectedNodes.Count + 1,
                NodeName = SelectedNode
            };
            
            SelectedNodes.Add(node);
            SelectedNode = string.Empty;
        }
    }

    private void ExecuteRemoveNode(object? parameter)
    {
        if (parameter is RouteNode node)
        {
            SelectedNodes.Remove(node);
            
            // 인덱스 재정렬
            for (int i = 0; i < SelectedNodes.Count; i++)
            {
                SelectedNodes[i].Index = i + 1;
            }
        }
    }

    private bool CanExecuteSaveRoute()
    {
        return !string.IsNullOrWhiteSpace(RouteName) && 
               SelectedNodes.Count >= 2 && 
               !string.IsNullOrWhiteSpace(SelectedMissionType);
    }

    private void ExecuteSaveRoute()
    {
        var route = new MissionRoute
        {
            Id = Guid.NewGuid().ToString(),
            Name = RouteName,
            Nodes = SelectedNodes.Select(n => n.NodeName).ToList(),
            MissionType = SelectedMissionType,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        Routes.Add(route);
        
        try
        {
            _fileService.SaveRoutes(Routes.ToList());
            StatusText = "라우터가 저장되었습니다.";
            
            ExecuteClearRoute();
        }
        catch (Exception ex)
        {
            StatusText = $"라우터 저장 실패: {ex.Message}";
        }
    }

    private void ExecuteClearRoute()
    {
        RouteName = string.Empty;
        SelectedMissionType = "Transport";
        SelectedNodes.Clear();
    }

    private void ExecuteImportRoutes()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
            Title = "라우터 파일 가져오기"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var importedRoutes = _fileService.ImportRoutes(openFileDialog.FileName);
                
                foreach (var route in importedRoutes)
                {
                    Routes.Add(route);
                }
                
                _fileService.SaveRoutes(Routes.ToList());
                StatusText = $"{importedRoutes.Count}개의 라우터를 가져왔습니다.";
            }
            catch (Exception ex)
            {
                StatusText = $"파일 가져오기 실패: {ex.Message}";
            }
        }
    }

    private void ExecuteExportRoutes()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "CSV 파일 (*.csv)|*.csv",
            Title = "라우터 파일 내보내기",
            FileName = $"mission_routes_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                _fileService.ExportRoutes(Routes.ToList(), saveFileDialog.FileName);
                StatusText = "라우터를 내보냈습니다.";
            }
            catch (Exception ex)
            {
                StatusText = $"파일 내보내기 실패: {ex.Message}";
            }
        }
    }

    private void ExecuteEditRoute(object? parameter)
    {
        if (parameter is MissionRoute route)
        {
            RouteName = route.Name;
            SelectedMissionType = route.MissionType;
            
            SelectedNodes.Clear();
            for (int i = 0; i < route.Nodes.Count; i++)
            {
                SelectedNodes.Add(new RouteNode
                {
                    Index = i + 1,
                    NodeName = route.Nodes[i]
                });
            }
            
            Routes.Remove(route);
        }
    }

    private void ExecuteDeleteRoute(object? parameter)
    {
        if (parameter is MissionRoute route)
        {
            Routes.Remove(route);
            
            try
            {
                _fileService.SaveRoutes(Routes.ToList());
                StatusText = "라우터가 삭제되었습니다.";
            }
            catch (Exception ex)
            {
                StatusText = $"라우터 삭제 실패: {ex.Message}";
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
                foreach (var vehicleName in vehiclesToRemove)
                {
                    Vehicles.Remove(existingVehicles[vehicleName]);
                }

                // Add new vehicles and update existing ones
                foreach (var newVehicle in newVehicles)
                {
                    if (existingVehicles.TryGetValue(newVehicle.Name, out var existingVehicle))
                    {
                        // Update properties of the existing vehicle instance
                        UpdateVehicleProperties(existingVehicle, newVehicle);
                    }
                    else
                    {
                        // Add new vehicle
                        Vehicles.Add(newVehicle);
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

    private void LoadInitialData()
    {
        try
        {
            var routes = _fileService.LoadRoutes();
            Routes = new ObservableCollection<MissionRoute>(routes);
        }
        catch (Exception ex)
        {
            StatusText = $"초기 데이터 로드 실패: {ex.Message}";
        }
    }

    private void RefreshMissionFilter()
    {
        MissionView?.Refresh();
        ApplyMissionSort();
        UpdateMissionStatistics();
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

        var timestamp = GetMissionTimestamp(mission);

        if (!(filter?.IsDefault ?? false))
        {
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
        else
        {
            var cutoff = DateTime.Now.AddMinutes(-CompletedMissionDefaultOffsetMinutes);
            if (mission.NavigationState == 4 && timestamp < cutoff)
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

    private static DateTime GetMissionTimestamp(MissionInfo mission)
    {
        return mission.QueueTimestamp;
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

    private void RefreshAlarmFilter()
    {
        AlarmView?.Refresh();
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

    public bool ContainsState(int state)
    {
        if (IsAll)
        {
            return true;
        }

        return _navigationStates.Contains(state);
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
