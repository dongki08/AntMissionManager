using System.Net.Http;
using System.Text;
using AntMissionManager.Models;
using Newtonsoft.Json;

namespace AntMissionManager.Services;

public class AntApiService
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:8081/wms/rest";
    private string _token = string.Empty;
    private string _apiVersion = string.Empty;

    public bool IsConnected { get; private set; }

    public AntApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> LoginAsync(string serverUrl, string username = "admin", string password = "123456")
    {
        try
        {
            _baseUrl = $"{serverUrl}/wms/rest";
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
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                
                if (loginResponse?.token != null)
                {
                    _token = loginResponse.token;
                    _apiVersion = $"/{loginResponse.apiVersion}";
                    IsConnected = true;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"ANT 서버 로그인 실패: {ex.Message}");
        }

        IsConnected = false;
        return false;
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
                            BatteryLevel = GetBatteryLevel(vehicleData.state?.batteryinfo),
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
                            CumulativeUptime = vehicleData.cumulativeuptime ?? 0
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

    private static int GetBatteryLevel(dynamic? batteryInfo)
    {
        if (batteryInfo is Newtonsoft.Json.Linq.JArray batteryArray && batteryArray.Count > 0)
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

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}