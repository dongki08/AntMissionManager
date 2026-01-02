using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AntManager.Models;
using Newtonsoft.Json;

namespace AntManager.Services;

public class ApiTemplateStorageService
{
    private readonly string _storageFilePath;

    public ApiTemplateStorageService()
    {
        // Store data in 'data' folder next to exe file
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var folder = Path.Combine(exeDirectory, "data");
        Directory.CreateDirectory(folder);
        _storageFilePath = Path.Combine(folder, "api_templates.json");
    }

    public IList<ApiRequestTemplate> LoadTemplates()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return new List<ApiRequestTemplate>();
            }

            var json = File.ReadAllText(_storageFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ApiRequestTemplate>();
            }

            var templates = JsonConvert.DeserializeObject<List<ApiRequestTemplate>>(json) ?? new List<ApiRequestTemplate>();
            EnsureTemplateIntegrity(templates);
            return templates;
        }
        catch
        {
            return new List<ApiRequestTemplate>();
        }
    }

    public void SaveTemplates(IEnumerable<ApiRequestTemplate> templates)
    {
        var list = templates.Select(Clone).ToList();
        EnsureTemplateIntegrity(list);
        var json = JsonConvert.SerializeObject(list, Formatting.Indented);
        File.WriteAllText(_storageFilePath, json);
    }

    public IList<ApiRequestTemplate> ImportFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var templates = JsonConvert.DeserializeObject<List<ApiRequestTemplate>>(json) ?? new List<ApiRequestTemplate>();
        EnsureTemplateIntegrity(templates);
        return templates;
    }

    public void ExportToFile(IEnumerable<ApiRequestTemplate> templates, string filePath)
    {
        var list = templates.Select(Clone).ToList();
        EnsureTemplateIntegrity(list);
        var json = JsonConvert.SerializeObject(list, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    private static ApiRequestTemplate Clone(ApiRequestTemplate template)
    {
        return new ApiRequestTemplate
        {
            Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id,
            Title = template.Title ?? string.Empty,
            Description = template.Description ?? string.Empty,
            Url = template.Url ?? string.Empty,
            Method = string.IsNullOrWhiteSpace(template.Method) ? "GET" : template.Method,
            Body = template.Body ?? string.Empty,
            Headers = template.Headers?.Select(h => new ApiHeaderEntry
            {
                Key = h.Key ?? string.Empty,
                Value = h.Value ?? string.Empty
            }).ToList() ?? new List<ApiHeaderEntry>()
        };
    }

    private static void EnsureTemplateIntegrity(IEnumerable<ApiRequestTemplate> templates)
    {
        foreach (var template in templates)
        {
            if (template.Id == Guid.Empty)
            {
                template.Id = Guid.NewGuid();
            }

            template.Title ??= string.Empty;
            template.Description ??= string.Empty;
            template.Method = string.IsNullOrWhiteSpace(template.Method) ? "GET" : template.Method;
            template.Url ??= string.Empty;
            template.Body ??= string.Empty;
            template.Headers ??= new List<ApiHeaderEntry>();

            foreach (var header in template.Headers)
            {
                header.Key ??= string.Empty;
                header.Value ??= string.Empty;
            }
        }
    }
}
