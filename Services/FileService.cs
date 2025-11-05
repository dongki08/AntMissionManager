using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AntManager.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace AntManager.Services;

public class FileService
{
    private readonly string _dataDirectory;
    private readonly string _routesFileName = "mission_routes.csv";
    private readonly string _templatesFileName = "mission_templates.json";

    public FileService()
    {
        // Store data in 'data' folder next to exe file
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _dataDirectory = Path.Combine(exeDirectory, "data");

        // Create directory if it doesn't exist
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
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
            System.Diagnostics.Debug.WriteLine($"Failed to load routes: {ex.Message}");
            return new List<MissionRoute>();
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
            System.Diagnostics.Debug.WriteLine($"Failed to save routes: {ex.Message}");
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

    // Mission Template Methods
    private static readonly JsonSerializerOptions TemplateJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<MissionTemplate>> LoadTemplatesAsync()
    {
        var filePath = Path.Combine(_dataDirectory, _templatesFileName);

        if (!File.Exists(filePath))
            return new List<MissionTemplate>();

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return DeserializeTemplates(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load templates: {ex.Message}");
            return new List<MissionTemplate>();
        }
    }

    public async Task SaveTemplatesAsync(List<MissionTemplate> templates)
    {
        var filePath = Path.Combine(_dataDirectory, _templatesFileName);

        try
        {
            var json = SerializeTemplates(templates);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save templates: {ex.Message}");
        }
    }

    public async Task<List<MissionTemplate>> ImportTemplatesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("파일을 찾을 수 없습니다.");

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return DeserializeTemplates(json);
        }
        catch (Exception ex)
        {
            throw new Exception($"템플릿 가져오기 실패: {ex.Message}");
        }
    }

    public async Task ExportTemplatesAsync(List<MissionTemplate> templates, string filePath)
    {
        try
        {
            var json = SerializeTemplates(templates);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"템플릿 내보내기 실패: {ex.Message}");
        }
    }

    private static string SerializeTemplates(IEnumerable<MissionTemplate> templates)
    {
        var dtoList = templates.Select(MissionTemplateMapper.ToDto).ToList();
        return JsonSerializer.Serialize(dtoList, TemplateJsonOptions);
    }

    private static List<MissionTemplate> DeserializeTemplates(string json)
    {
        try
        {
            var dtoList = JsonSerializer.Deserialize<List<MissionTemplateDto>>(json, TemplateJsonOptions);
            if (dtoList != null && dtoList.Any())
            {
                return dtoList.Select(MissionTemplateMapper.FromDto).ToList();
            }
        }
        catch
        {
            // Try legacy format below
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<List<MissionTemplate>>(json, TemplateJsonOptions);
            if (legacy != null)
            {
                return legacy;
            }
        }
        catch
        {
            // ignored
        }

        return new List<MissionTemplate>();
    }
    private static class MissionTemplateMapper
    {
        public static MissionTemplateDto ToDto(MissionTemplate template)
        {
            return new MissionTemplateDto
            {
                Title = template.Title,
                MissionType = template.TemplateType switch
                {
                    MissionTemplateType.Moving => "Moving",
                    MissionTemplateType.PickAndDrop => "PickDrop",
                    MissionTemplateType.Dynamic => "Dynamic",
                    _ => "PickDrop"
                },
                Vehicle = template.Vehicle,
                FromNode = template.FromNode,
                ToNode = template.ToNode,
                Priority = template.Priority,
                Description = template.PriorityDescription,
                CreatedAt = template.CreatedAt,
                FromNodeConfig = template.FromNodeConfig,
                ToNodeConfig = template.ToNodeConfig
            };
        }

        public static MissionTemplate FromDto(MissionTemplateDto dto)
        {
            var template = new MissionTemplate
            {
                Title = dto.Title ?? string.Empty,
                Vehicle = dto.Vehicle ?? string.Empty,
                FromNode = dto.FromNode ?? string.Empty,
                ToNode = dto.ToNode ?? string.Empty,
                Priority = dto.Priority ?? 2,
                PriorityDescription = dto.Description ?? string.Empty,
                CreatedAt = dto.CreatedAt ?? DateTime.Now,
                FromNodeConfig = dto.FromNodeConfig,
                ToNodeConfig = dto.ToNodeConfig
            };

            template.TemplateType = ResolveTemplateType(dto);

            return template;
        }

        private static MissionTemplateType ResolveTemplateType(MissionTemplateDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.MissionType))
            {
                switch (dto.MissionType.Trim().ToLowerInvariant())
                {
                    case "moving":
                        return MissionTemplateType.Moving;
                    case "pickdrop":
                    case "pickanddrop":
                    case "pick_drop":
                        return MissionTemplateType.PickAndDrop;
                    case "dynamic":
                        return MissionTemplateType.Dynamic;
                }
            }

            if (dto.IsMoving == true)
            {
                return MissionTemplateType.Moving;
            }

            if (dto.IsDynamic == true)
            {
                return MissionTemplateType.Dynamic;
            }

            if (dto.IsPickAndDrop == true)
            {
                return MissionTemplateType.PickAndDrop;
            }

            return MissionTemplateType.PickAndDrop;
        }
    }

    private class MissionTemplateDto
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("missionType")] public string? MissionType { get; set; }
        [JsonPropertyName("vehicle")] public string? Vehicle { get; set; }
        [JsonPropertyName("fromNode")] public string? FromNode { get; set; }
        [JsonPropertyName("toNode")] public string? ToNode { get; set; }
        [JsonPropertyName("priority")] public int? Priority { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("createdAt")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("fromNodeConfig")] public DynamicNodeConfig? FromNodeConfig { get; set; }
        [JsonPropertyName("toNodeConfig")] public DynamicNodeConfig? ToNodeConfig { get; set; }

        // Legacy fields for compatibility
        [JsonPropertyName("isMoving")] public bool? IsMoving { get; set; }
        [JsonPropertyName("isPickAndDrop")] public bool? IsPickAndDrop { get; set; }
        [JsonPropertyName("isDynamic")] public bool? IsDynamic { get; set; }
    }
}
