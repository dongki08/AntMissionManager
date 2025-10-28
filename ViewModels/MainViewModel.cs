using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AntMissionManager.Models;
using AntMissionManager.Services;
using AntMissionManager.Views;
using Microsoft.Win32;
using System.Linq;

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

    // Mission Router Properties
    private ObservableCollection<MissionRoute> _routes = new();
    private ObservableCollection<RouteNode> _selectedNodes = new();
    private string _routeName = string.Empty;
    private string _selectedMissionType = "Transport";
    private string _selectedNode = string.Empty;

    // Vehicle Management Properties
    private ObservableCollection<Vehicle> _vehicles = new();
    private string _selectedVehicle = string.Empty;
    private string _selectedInsertNode = string.Empty;
    private ObservableCollection<NodeInfo> _availableNodes = new();

    public MainViewModel()
    {
        _antApiService = new AntApiService();
        _fileService = new FileService();
        
        InitializeCommands();
        LoadInitialData();
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
        set => SetProperty(ref _vehicles, value);
    }

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
    }

    #endregion

    #region Command Implementations

    private void ExecuteLogout()
    {
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

    private async Task ExecuteRefreshMissions()
    {
        StatusText = "미션 정보 새로고침 중...";
        
        try
        {
            var missions = await _antApiService.GetAllMissionsAsync();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                Missions.Clear();
                foreach (var mission in missions)
                {
                    Missions.Add(mission);
                }
                
                UpdateMissionStatistics();
            });
            
            StatusText = "미션 정보 업데이트 완료";
        }
        catch (Exception ex)
        {
            StatusText = $"미션 새로고침 실패: {ex.Message}";
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

    private async Task ExecuteRefreshVehicles()
    {
        StatusText = "차량 정보 새로고침 중...";
        
        try
        {
            var vehicles = await _antApiService.GetAllVehiclesAsync();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                Vehicles.Clear();
                foreach (var vehicle in vehicles)
                {
                    Vehicles.Add(vehicle);
                }
            });
            
            StatusText = "차량 정보 업데이트 완료";
        }
        catch (Exception ex)
        {
            StatusText = $"차량 새로고침 실패: {ex.Message}";
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
        
        try
        {
            var success = await _antApiService.LoginAsync(ServerUrl, Username, Password);
            
            if (success)
            {
                IsConnected = true;
                ConnectionStatus = $"연결 상태: 연결됨 ({ServerUrl})";
                StatusText = "ANT 서버 연결 성공";
                
                // 연결 성공 시 기본 데이터 로드
                await ExecuteRefreshNodes();
                await ExecuteRefreshVehicles();
            }
            else
            {
                IsConnected = false;
                ConnectionStatus = "연결 상태: 연결 실패";
                StatusText = "ANT 서버 연결 실패";
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatus = "연결 상태: 오류";
            StatusText = $"서버 연결 오류: {ex.Message}";
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

    private void UpdateMissionStatistics()
    {
        TotalMissions = Missions.Count;
        RunningMissions = Missions.Count(m => m.NavigationState == 3); // Started
        PendingMissions = Missions.Count(m => m.NavigationState == 0 || m.NavigationState == 1); // Received or Accepted
        CompletedMissions = Missions.Count(m => m.NavigationState == 4); // Completed
    }

    #endregion
}