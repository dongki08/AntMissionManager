using System.IO;
using System.Text.Json;

namespace AntMissionManager.Services;

public class MapSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AntMissionManager",
        "mapsettings.json"
    );

    public class MapSettings
    {
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double RotationAngle { get; set; }
        public double ZoomLevel { get; set; } = 1.0;
        public bool IsFlippedHorizontally { get; set; } = false;
        public bool ShowNodeLabels { get; set; } = false;
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }

    public async Task<MapSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new MapSettings();
            }

            var json = await File.ReadAllTextAsync(SettingsPath);
            return JsonSerializer.Deserialize<MapSettings>(json) ?? new MapSettings();
        }
        catch
        {
            return new MapSettings();
        }
    }

    public async Task SaveSettingsAsync(MapSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            settings.LastSaved = DateTime.Now;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save map settings: {ex.Message}");
        }
    }
}