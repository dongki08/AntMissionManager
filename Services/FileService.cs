using System.Globalization;
using System.IO;
using System.Text;
using AntMissionManager.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace AntMissionManager.Services;

public class FileService
{
    private readonly string _dataDirectory;
    private readonly string _routesFileName = "mission_routes.csv";

    public FileService()
    {
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AntMissionManager");
        Directory.CreateDirectory(_dataDirectory);
    }

    public List<MissionRoute> LoadRoutes()
    {
        var filePath = Path.Combine(_dataDirectory, _routesFileName);
        
        if (!File.Exists(filePath))
            return new List<MissionRoute>();

        try
        {
            using var reader = new StringReader(File.ReadAllText(filePath, Encoding.UTF8));
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            });

            var routes = new List<MissionRoute>();
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var route = new MissionRoute
                {
                    Id = csv.GetField<string>("Id") ?? Guid.NewGuid().ToString(),
                    Name = csv.GetField<string>("Name") ?? "",
                    Nodes = csv.GetField<string>("Nodes")?.Split(',').ToList() ?? new List<string>(),
                    MissionType = csv.GetField<string>("MissionType") ?? "",
                    CreatedAt = DateTime.TryParse(csv.GetField<string>("CreatedAt"), out var createdAt) ? createdAt : DateTime.Now,
                    IsActive = bool.TryParse(csv.GetField<string>("IsActive"), out var isActive) && isActive
                };
                routes.Add(route);
            }

            return routes;
        }
        catch (Exception ex)
        {
            throw new Exception($"라우터 파일 로드 실패: {ex.Message}");
        }
    }

    public void SaveRoutes(List<MissionRoute> routes)
    {
        var filePath = Path.Combine(_dataDirectory, _routesFileName);

        try
        {
            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            csv.WriteHeader<RouteRecord>();
            csv.NextRecord();

            foreach (var route in routes)
            {
                csv.WriteRecord(new RouteRecord
                {
                    Id = route.Id,
                    Name = route.Name,
                    Nodes = string.Join(",", route.Nodes),
                    MissionType = route.MissionType,
                    CreatedAt = route.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsActive = route.IsActive.ToString()
                });
                csv.NextRecord();
            }

            File.WriteAllText(filePath, writer.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new Exception($"라우터 파일 저장 실패: {ex.Message}");
        }
    }

    public List<MissionRoute> ImportRoutes(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("파일을 찾을 수 없습니다.");

        try
        {
            using var reader = new StringReader(File.ReadAllText(filePath, Encoding.UTF8));
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            });

            var routes = new List<MissionRoute>();
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var route = new MissionRoute
                {
                    Id = Guid.NewGuid().ToString(), // 새로운 ID 생성
                    Name = csv.GetField<string>("Name") ?? "",
                    Nodes = csv.GetField<string>("Nodes")?.Split(',').ToList() ?? new List<string>(),
                    MissionType = csv.GetField<string>("MissionType") ?? "",
                    CreatedAt = DateTime.Now, // 현재 시간으로 설정
                    IsActive = bool.TryParse(csv.GetField<string>("IsActive"), out var isActive) && isActive
                };
                routes.Add(route);
            }

            return routes;
        }
        catch (Exception ex)
        {
            throw new Exception($"파일 가져오기 실패: {ex.Message}");
        }
    }

    public void ExportRoutes(List<MissionRoute> routes, string filePath)
    {
        try
        {
            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            csv.WriteHeader<RouteRecord>();
            csv.NextRecord();

            foreach (var route in routes)
            {
                csv.WriteRecord(new RouteRecord
                {
                    Id = route.Id,
                    Name = route.Name,
                    Nodes = string.Join(",", route.Nodes),
                    MissionType = route.MissionType,
                    CreatedAt = route.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsActive = route.IsActive.ToString()
                });
                csv.NextRecord();
            }

            File.WriteAllText(filePath, writer.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new Exception($"파일 내보내기 실패: {ex.Message}");
        }
    }

    private class RouteRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nodes { get; set; } = string.Empty;
        public string MissionType { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string IsActive { get; set; } = string.Empty;
    }
}