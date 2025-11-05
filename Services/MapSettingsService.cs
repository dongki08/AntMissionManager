using System.IO;
using System.Text.Json;

namespace AntManager.Services;

public class MapSettingsService
{
    private readonly string _settingsPath;

    public class MapSettings
    {
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double RotationAngle { get; set; }
        public double ZoomLevel { get; set; } = 1.0;
        public bool IsFlippedHorizontally { get; set; } = false;
        public bool ShowNodeLabels { get; set; } = false;
        public double NodeSize { get; set; } = 5;
        public double VehicleSize { get; set; } = 16;
        public double VehicleAngleOffset { get; set; } = 0;
        public bool AreVehiclesFlipped { get; set; } = false;
        public double VehicleLabelScale { get; set; } = 1.0;
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }

    public MapSettingsService()
    {
        // Store settings in 'data' folder next to exe file
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var dataDirectory = Path.Combine(exeDirectory, "data");

        // Create directory if it doesn't exist
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        _settingsPath = Path.Combine(dataDirectory, "map_settings.json");
    }

    public async Task<MapSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new MapSettings();
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            return JsonSerializer.Deserialize<MapSettings>(json) ?? new MapSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load map settings: {ex.Message}");
            return new MapSettings();
        }
    }

    public async Task SaveSettingsAsync(MapSettings settings)
    {
        try
        {
            settings.LastSaved = DateTime.Now;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save map settings: {ex.Message}");
        }
    }
}
