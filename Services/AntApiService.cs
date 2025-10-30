using System.Net.Http;
using System.Text;
using AntMissionManager.Models;
using Newtonsoft.Json;

namespace AntMissionManager.Services;

public class AntApiService
{
    private static AntApiService? _instance;
    private static readonly object _lock = new object();

    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:8081/wms/rest";
    private string _token = string.Empty;
    private string _apiVersion = string.Empty;

    public bool IsConnected { get; private set; }
    public string CurrentServerUrl { get; private set; } = "localhost:8081";

    public static AntApiService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new AntApiService();
                    }
                }
            }
            return _instance;
        }
    }

    private AntApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? Token { get; set; }
        public string? ApiVersion { get; set; }
        public string? DisplayName { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public async Task<LoginResponse> LoginAsync(string serverUrl, string username = "admin", string password = "123456")
    {
        try
        {
            _baseUrl = $"http://{serverUrl}/wms/rest";
            CurrentServerUrl = serverUrl;
            var loginUrl = $"{_baseUrl}/login";

            var loginRequest = new
            {
                username = username,
                password = password,
                isLdap = false,
                apiVersion = new { major = 0, minor = 1 }
            };

            var json = JsonConvert.SerializeObject(loginRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(loginUrl, content);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var loginData = JsonConvert.DeserializeObject<dynamic>(responseContent);

                if (loginData?.token != null)
                {
                    _token = loginData.token;
                    _apiVersion = $"/{loginData.apiVersion}";
                    IsConnected = true;

                    return new LoginResponse
                    {
                        Success = true,
                        StatusCode = statusCode,
                        Token = _token,
                        ApiVersion = loginData.apiVersion,
                        DisplayName = loginData.displayName ?? username
                    };
                }
            }
            else if (statusCode == 401)
            {
                IsConnected = false;
                return new LoginResponse
                {
                    Success = false,
                    StatusCode = 401,
                    ErrorMessage = "아이디 또는 비밀번호가 올바르지 않습니다."
                };
            }
            else
            {
                IsConnected = false;
                return new LoginResponse
                {
                    Success = false,
                    StatusCode = statusCode,
                    ErrorMessage = $"서버 응답 오류: {statusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            IsConnected = false;
            return new LoginResponse
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = $"서버 연결 실패: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            IsConnected = false;
            return new LoginResponse
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = $"로그인 오류: {ex.Message}"
            };
        }

        IsConnected = false;
        return new LoginResponse
        {
            Success = false,
            StatusCode = 0,
            ErrorMessage = "알 수 없는 오류가 발생했습니다."
        };
    }

    public async Task<List<Vehicle>> GetAllVehiclesAsync()
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/vehicles";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<dynamic>(content);
                
                var vehicles = new List<Vehicle>();
                
                if (apiResponse?.payload?.vehicles != null)
                {
                    foreach (var vehicleData in apiResponse.payload.vehicles)
                    {
                        var vehicle = new Vehicle
                        {
                            Name = vehicleData.name ?? "",
                            OperatingState = vehicleData.operatingstate ?? 0,
                            Location = GetCurrentLocation(vehicleData.location),
                            MissionId = vehicleData.missionid ?? "",
                            BatteryLevel = GetBatteryLevel(vehicleData.state),
                            Alarms = GetAlarms(vehicleData.alarms),
                            LastUpdate = DateTime.TryParse(vehicleData.timestamp?.ToString(), out DateTime timestamp) ? timestamp : DateTime.Now,
                            IpAddress = vehicleData.ipaddress ?? "",
                            IsSimulated = vehicleData.issimulated ?? false,
                            IsLoaded = vehicleData.isloaded ?? false,
                            Payload = vehicleData.payload ?? "",
                            Coordinates = GetCoordinates(vehicleData.location),
                            Course = GetCourse(vehicleData.location),
                            CurrentNodeName = GetCurrentNodeName(vehicleData.location),
                            TraveledDistance = vehicleData.traveleddistance ?? 0,
                            CumulativeUptime = vehicleData.cumulativeuptime ?? 0,
                            Path = GetPath(vehicleData.path),
                            VehicleState = GetVehicleState(vehicleData.state),
                            // 추가 필드들
                            Coverage = vehicleData.coverage ?? false,
                            Port = vehicleData.port ?? 0,
                            IsOmni = vehicleData.isOmni ?? false,
                            ForceCharge = vehicleData.forceCharge ?? false,
                            ActionName = GetActionName(vehicleData.action),
                            ActionSourceId = GetActionSourceId(vehicleData.action),
                            ArrivalDate = GetArrivalDate(vehicleData.action),
                            AbsArrivalDate = GetAbsArrivalDate(vehicleData.action),
                            ActionNodeId = GetActionNodeId(vehicleData.action),
                            CurrentNodeId = vehicleData.location?.currentnodeid ?? -1,
                            MapName = vehicleData.location?.map ?? "",
                            GroupName = vehicleData.location?.group ?? "",
                            Uncertainty = GetUncertainty(vehicleData.location),
                            ConnectionOk = GetStateValue(vehicleData.state, "connection.ok"),
                            BatteryMaxTemp = GetStateValue(vehicleData.state, "battery.info.maxtemperature"),
                            BatteryVoltage = GetBatteryVoltage(vehicleData.state),
                            VehicleType = GetStateValue(vehicleData.state, "vehicle.type"),
                            LockUuid = GetStateValue(vehicleData.state, "lock.UUID"),
                            LockOwnerApp = GetLockOwner(vehicleData.state, 0),
                            LockOwnerPc = GetLockOwner(vehicleData.state, 1),
                            LockOwnerUser = GetLockOwner(vehicleData.state, 2),
                            MissionFrom = GetMissionInfo(vehicleData.state, 0),
                            MissionTo = GetMissionInfo(vehicleData.state, 1),
                            MissionFinal = GetMissionInfo(vehicleData.state, 2),
                            Errors = GetErrors(vehicleData.state),
                            MissionBlocked = GetMissionBlocked(vehicleData.state),
                            // state 객체의 추가 필드들
                            ActionSourceType = GetActionSourceType(vehicleData.action),
                            BodyShape = GetStateArrayValue(vehicleData.state, "body.shape"),
                            TrafficInfo = GetStateArrayValue(vehicleData.state, "traffic.info"),
                            MissionProgress = GetStateArrayValue(vehicleData.state, "mission.progress"),
                            ErrorBits = GetStateArrayValue(vehicleData.state, "error.bits"),
                            SharedMemoryOut = GetStateArrayValue(vehicleData.state, "sharedMemory.out"),
                            SharedMemoryIn = GetStateArrayValue(vehicleData.state, "sharedMemory.in"),
                            VehicleShape = GetStateArrayValue(vehicleData.state, "vehicle.shape"),
                            ErrorDetailsLabel = GetStateArrayValue(vehicleData.state, "errorDetailsLabel"),
                            Messages = GetStateArrayValue(vehicleData.state, "messages"),
                            ErrorDetails = GetStateArrayValue(vehicleData.state, "errorDetails")
                        };
                        vehicles.Add(vehicle);
                    }
                }
                
                return vehicles;
            }
            else
            {
                throw new Exception($"차량 정보 조회 실패: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"차량 정보 조회 오류: {ex.Message}");
        }
    }

    public async Task<List<MissionInfo>> GetAllMissionsAsync()
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/missions";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<dynamic>(content);
                
                var missions = new List<MissionInfo>();
                
                if (apiResponse?.payload?.missions != null)
                {
                    foreach (var missionData in apiResponse.payload.missions)
                    {
                        var mission = new MissionInfo
                        {
                            MissionId = missionData.missionid ?? "",
                            MissionType = GetMissionTypeText((int)(missionData.missiontype ?? 0)),
                            FromNode = missionData.fromnode ?? "",
                            ToNode = missionData.tonode ?? "",
                            AssignedVehicle = missionData.assignedto ?? "",
                            NavigationState = missionData.navigationstate ?? 0,
                            TransportState = missionData.transportstate ?? 0,
                            Priority = missionData.priority ?? 0,
                            CreatedAt = DateTime.TryParse(missionData.createdat?.ToString(), out DateTime createdAt) ? createdAt : DateTime.Now,
                            ArrivingTime = DateTime.TryParse(missionData.arrivingtime?.ToString(), out DateTime arrivingTime) ? arrivingTime : null
                        };
                        missions.Add(mission);
                    }
                }
                
                return missions;
            }
            else
            {
                throw new Exception($"미션 정보 조회 실패: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"미션 정보 조회 오류: {ex.Message}");
        }
    }

    public async Task<bool> CreateMissionAsync(string missionType, string fromNode, string toNode, string? vehicle = null)
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/missions";
            
            var missionRequest = new
            {
                missionrequest = new
                {
                    requestor = "admin",
                    missiontype = missionType,
                    fromnode = fromNode,
                    tonode = toNode,
                    cardinality = "1",
                    priority = "2",
                    deadline = "",
                    parameters = new
                    {
                        desc = "Mission extension",
                        type = "org.json.JSONObject",
                        name = "parameters",
                        value = new
                        {
                            payload = "Default Payload",
                            vehicle = vehicle ?? ""
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(missionRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            throw new Exception($"미션 생성 오류: {ex.Message}");
        }
    }

    public async Task<bool> CancelMissionAsync(string missionId)
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/missions/{missionId}";
            
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            throw new Exception($"미션 취소 오류: {ex.Message}");
        }
    }

    public async Task<bool> InsertVehicleAsync(string vehicleName, string nodeId, bool forceInsertion = false)
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/vehicles/{Uri.EscapeDataString(vehicleName)}/command";
            
            var command = new
            {
                command = new
                {
                    name = "insert",
                    args = new
                    {
                        nodeId = nodeId,
                        forceInsertion = forceInsertion
                    }
                }
            };

            var json = JsonConvert.SerializeObject(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            throw new Exception($"차량 삽입 오류: {ex.Message}");
        }
    }

    public async Task<bool> ExtractVehicleAsync(string vehicleName)
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/vehicles/{Uri.EscapeDataString(vehicleName)}/command";
            
            var command = new
            {
                command = new
                {
                    name = "extract",
                    args = new { }
                }
            };

            var json = JsonConvert.SerializeObject(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            throw new Exception($"차량 추출 오류: {ex.Message}");
        }
    }

    private static int GetBatteryLevel(dynamic? stateData)
    {
        if (stateData?["battery.info"] is Newtonsoft.Json.Linq.JArray batteryArray && batteryArray.Count > 0)
        {
            if (double.TryParse(batteryArray[0]?.ToString(), out var battery))
            {
                return (int)battery;
            }
        }
        return 0;
    }

    private static List<string> GetAlarms(dynamic? alarms)
    {
        var alarmList = new List<string>();
        
        if (alarms is Newtonsoft.Json.Linq.JArray alarmArray)
        {
            foreach (var alarm in alarmArray)
            {
                if (alarm?.ToString() is string alarmText && !string.IsNullOrEmpty(alarmText))
                {
                    alarmList.Add(alarmText);
                }
            }
        }
        
        return alarmList;
    }

    private static string GetCurrentLocation(dynamic? locationData)
    {
        if (locationData?.currentnode?.name != null)
        {
            return locationData.currentnode.name;
        }
        return "Unknown";
    }

    private static List<double> GetCoordinates(dynamic? locationData)
    {
        if (locationData?.coord is Newtonsoft.Json.Linq.JArray coordArray && coordArray.Count >= 2)
        {
            return new List<double> 
            { 
                (double)(coordArray[0] ?? 0.0), 
                (double)(coordArray[1] ?? 0.0) 
            };
        }
        return new List<double>();
    }

    private static double GetCourse(dynamic? locationData)
    {
        if (locationData?.course != null)
        {
            return (double)(locationData.course ?? 0.0);
        }
        return 0.0;
    }

    private static string GetCurrentNodeName(dynamic? locationData)
    {
        if (locationData?.currentnode?.name != null)
        {
            return locationData.currentnode.name;
        }
        return "";
    }

    private static List<string> GetPath(dynamic? pathData)
    {
        var path = new List<string>();

        if (pathData is Newtonsoft.Json.Linq.JArray pathArray)
        {
            foreach (var node in pathArray)
            {
                if (node?.ToString() is string nodeText && !string.IsNullOrEmpty(nodeText))
                {
                    path.Add(nodeText);
                }
            }
        }

        return path;
    }

    private static string GetVehicleState(dynamic? stateData)
    {
        if (stateData?["vehicle.state"] is Newtonsoft.Json.Linq.JArray vehicleStateArray && vehicleStateArray.Count > 0)
        {
            return vehicleStateArray[0]?.ToString() ?? "";
        }
        return "";
    }

    private static string GetActionName(dynamic? actionData)
    {
        return actionData?.name?.ToString() ?? "";
    }

    private static string GetActionSourceId(dynamic? actionData)
    {
        return actionData?.sourceid?.ToString() ?? "";
    }

    private static string GetArrivalDate(dynamic? actionData)
    {
        return actionData?.args?.arrivaldate?.ToString() ?? "";
    }

    private static string GetAbsArrivalDate(dynamic? actionData)
    {
        return actionData?.args?.absarrivaldate?.ToString() ?? "";
    }

    private static string GetActionNodeId(dynamic? actionData)
    {
        return actionData?.args?.nodeid?.ToString() ?? "";
    }

    private static List<double> GetUncertainty(dynamic? locationData)
    {
        var uncertainty = new List<double>();
        if (locationData?.uncertainty is Newtonsoft.Json.Linq.JArray uncertaintyArray && uncertaintyArray.Count >= 2)
        {
            uncertainty.Add((double)(uncertaintyArray[0] ?? 0.0));
            uncertainty.Add((double)(uncertaintyArray[1] ?? 0.0));
        }
        return uncertainty;
    }

    private static string GetStateValue(dynamic? stateData, string key)
    {
        if (stateData?[key] is Newtonsoft.Json.Linq.JArray valueArray && valueArray.Count > 0)
        {
            return valueArray[0]?.ToString() ?? "";
        }
        return "";
    }

    private static string GetBatteryVoltage(dynamic? stateData)
    {
        if (stateData?["battery.info"] is Newtonsoft.Json.Linq.JArray batteryArray && batteryArray.Count > 1)
        {
            return batteryArray[1]?.ToString() ?? "";
        }
        return "";
    }

    private static string GetLockOwner(dynamic? stateData, int index)
    {
        if (stateData?["lock.owner"] is Newtonsoft.Json.Linq.JArray ownerArray && ownerArray.Count > index)
        {
            return ownerArray[index]?.ToString() ?? "";
        }
        return "";
    }

    private static string GetMissionInfo(dynamic? stateData, int index)
    {
        if (stateData?["mission.info"] is Newtonsoft.Json.Linq.JArray missionArray && missionArray.Count > index)
        {
            return missionArray[index]?.ToString() ?? "";
        }
        return "";
    }

    private static List<string> GetErrors(dynamic? stateData)
    {
        var errors = new List<string>();
        if (stateData?["errors"] is Newtonsoft.Json.Linq.JArray errorArray)
        {
            foreach (var error in errorArray)
            {
                if (error?.ToString() is string errorText && !string.IsNullOrEmpty(errorText))
                {
                    errors.Add(errorText);
                }
            }
        }
        return errors;
    }

    private static bool GetMissionBlocked(dynamic? stateData)
    {
        if (stateData?["vehicle.state"] is Newtonsoft.Json.Linq.JArray vehicleStateArray && vehicleStateArray.Count > 1)
        {
            var blockedText = vehicleStateArray[1]?.ToString() ?? "false";
            return bool.TryParse(blockedText, out bool blocked) && blocked;
        }
        return false;
    }

    private static string GetActionSourceType(dynamic? actionData)
    {
        return actionData?.sourcetype?.ToString() ?? "";
    }

    private static List<string> GetStateArrayValue(dynamic? stateData, string key)
    {
        var result = new List<string>();

        if (stateData?[key] is Newtonsoft.Json.Linq.JArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    result.Add(item.ToString() ?? "");
                }
            }
        }

        return result;
    }

    private static string GetMissionTypeText(int missionType)
    {
        return missionType switch
        {
            0 => "Transport to Station",
            1 => "Move to Station", 
            2 => "Waiting Lane",
            7 => "Transport to Node",
            8 => "Move to Node",
            9 => "Station to Station",
            10 => "Move Vehicle to Node",
            12 => "Move to Loop",
            _ => $"Unknown ({missionType})"
        };
    }

    public async Task<List<NodeInfo>> GetAllNodesAsync()
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/mapdata";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<dynamic>(content);
                
                var nodes = new List<NodeInfo>();
                
                if (apiResponse?.payload?.data != null)
                {
                    foreach (var dataItem in apiResponse.payload.data)
                    {
                        if (dataItem?.data?.layers != null)
                        {
                            foreach (var layer in dataItem.data.layers)
                            {
                                if (layer?.name == "navigation" && layer?.symbols != null)
                                {
                                    foreach (var symbol in layer.symbols)
                                    {
                                        if (symbol?.coord != null && symbol.coord.Count >= 2)
                                        {
                                            var node = new NodeInfo
                                            {
                                                Name = symbol.name ?? "",
                                                Id = symbol.id ?? symbol.symbolid ?? "",
                                                X = (double)(symbol.coord[0] ?? 0.0),
                                                Y = (double)(symbol.coord[1] ?? 0.0),
                                                IsAvailable = true
                                            };
                                            nodes.Add(node);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                return nodes;
            }
            else
            {
                throw new Exception($"노드 정보 조회 실패: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"노드 정보 조회 오류: {ex.Message}");
        }
    }

    public async Task<List<MissionInfo>> GetFilteredMissionsAsync(Dictionary<string, object>? filters = null, string composition = "AND")
    {
        if (!IsConnected) 
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var url = $"{_baseUrl}{_apiVersion}/missions";
            var queryParams = new List<string>();

            // 기본 정렬 및 페이징
            queryParams.Add("datarange=" + Uri.EscapeDataString("[0,50]"));
            queryParams.Add("dataorderby=" + Uri.EscapeDataString("[[\"createdat\",\"desc\"]]"));

            // 필터 조건 추가
            if (filters != null && filters.Count > 0)
            {
                var criteria = new List<string>();
                
                foreach (var filter in filters)
                {
                    var key = filter.Key;
                    var value = filter.Value;
                    
                    // 연산자 추출
                    string fieldName = key;
                    string operatorType = "IN";
                    
                    if (key.Contains(" EQ"))
                    {
                        operatorType = "EQ";
                        fieldName = key.Replace(" EQ", "");
                    }
                    else if (key.Contains(" GT"))
                    {
                        operatorType = "GT";
                        fieldName = key.Replace(" GT", "");
                    }
                    else if (key.Contains(" LT"))
                    {
                        operatorType = "LT";
                        fieldName = key.Replace(" LT", "");
                    }
                    else if (key.Contains(" !EQ"))
                    {
                        operatorType = "!EQ";
                        fieldName = key.Replace(" !EQ", "");
                    }

                    string valueStr;
                    if (value is IEnumerable<object> enumerable && !(value is string))
                    {
                        valueStr = string.Join("|", enumerable);
                    }
                    else
                    {
                        valueStr = value.ToString() ?? "";
                    }

                    criteria.Add($"\"{fieldName} {operatorType}:{valueStr}\"");
                }

                var criteriaJson = $"{{\"criteria\":[{string.Join(",", criteria)}],\"composition\":\"{composition}\"}}";
                queryParams.Add("dataselection=" + Uri.EscapeDataString(criteriaJson));
            }

            if (queryParams.Count > 0)
            {
                url += "?" + string.Join("&", queryParams);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<dynamic>(content);
                
                var missions = new List<MissionInfo>();
                
                if (apiResponse?.payload?.missions != null)
                {
                    foreach (var missionData in apiResponse.payload.missions)
                    {
                        var mission = new MissionInfo
                        {
                            MissionId = missionData.missionid ?? "",
                            MissionType = GetMissionTypeText((int)(missionData.missiontype ?? 0)),
                            FromNode = missionData.fromnode ?? "",
                            ToNode = missionData.tonode ?? "",
                            AssignedVehicle = missionData.assignedto ?? "",
                            NavigationState = missionData.navigationstate ?? 0,
                            TransportState = missionData.transportstate ?? 0,
                            Priority = missionData.priority ?? 0,
                            CreatedAt = DateTime.TryParse(missionData.createdat?.ToString(), out DateTime createdAt) ? createdAt : DateTime.Now,
                            ArrivingTime = DateTime.TryParse(missionData.arrivingtime?.ToString(), out DateTime arrivingTime) ? arrivingTime : null
                        };
                        missions.Add(mission);
                    }
                }
                
                return missions;
            }
            else
            {
                throw new Exception($"필터된 미션 정보 조회 실패: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"필터된 미션 정보 조회 오류: {ex.Message}");
        }
    }

    public async Task<List<AlarmInfo>> GetAlarmsAsync(int limit = 50, bool ascending = false)
    {
        if (!IsConnected)
            throw new InvalidOperationException("ANT 서버에 연결되지 않았습니다.");

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var dataRange = $"[0,{limit}]";
            var sortOrder = ascending ? "asc" : "desc";
            var dataOrderBy = $"[[\"createdat\",\"{sortOrder}\"]]";

            var url = $"{_baseUrl}{_apiVersion}/alarms?_={timestamp}&datarange={Uri.EscapeDataString(dataRange)}&dataorderby={Uri.EscapeDataString(dataOrderBy)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<dynamic>(content);

                var alarms = new List<AlarmInfo>();

                if (apiResponse?.payload?.alarms != null)
                {
                    foreach (var alarmData in apiResponse.payload.alarms)
                    {
                        DateTime? closedAt = null;
                        if (alarmData.closedat != null)
                        {
                            if (DateTime.TryParse(alarmData.closedat.ToString(), out DateTime parsedClosedAt))
                            {
                                closedAt = parsedClosedAt;
                            }
                        }

                        DateTime? clearedAt = null;
                        if (alarmData.clearedat != null)
                        {
                            if (DateTime.TryParse(alarmData.clearedat.ToString(), out DateTime parsedClearedAt))
                            {
                                clearedAt = parsedClearedAt;
                            }
                        }

                        var alarm = new AlarmInfo
                        {
                            Uuid = alarmData.uuid?.ToString() ?? "",
                            SourceId = alarmData.sourceid?.ToString() ?? "",
                            SourceType = alarmData.sourcetype?.ToString() ?? "",
                            EventName = alarmData.eventname?.ToString() ?? "",
                            AlarmMessage = alarmData.alarmmessage?.ToString() ?? "",
                            EventCount = alarmData.eventcount ?? 0,
                            FirstEventAt = DateTime.TryParse(alarmData.firsteventat?.ToString(), out DateTime firstEventAt) ? firstEventAt : DateTime.Now,
                            LastEventAt = DateTime.TryParse(alarmData.lasteventat?.ToString(), out DateTime lastEventAt) ? lastEventAt : DateTime.Now,
                            Timestamp = DateTime.TryParse(alarmData.timestamp?.ToString(), out DateTime alarmTimestamp) ? alarmTimestamp : DateTime.Now,
                            State = alarmData.state ?? 0,
                            ClosedAt = closedAt,
                            ClearedAt = clearedAt
                        };
                        alarms.Add(alarm);
                    }
                }

                return alarms;
            }
            else
            {
                throw new Exception($"알람 정보 조회 실패: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"알람 정보 조회 오류: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}