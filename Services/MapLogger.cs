using System;

namespace AntManager.Services;

public static class MapLogger
{
    private static readonly object LockObject = new object();

    static MapLogger()
    {
        Log("=== Map Logger Started (Debug Output Only) ===");
    }

    public static void Log(string message)
    {
        try
        {
            lock (LockObject)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] {message}";

                // Write to debug output only
                System.Diagnostics.Debug.WriteLine(logMessage);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MapLogger Error: {ex.Message}");
        }
    }

    public static void LogSection(string sectionName)
    {
        Log("");
        Log($"========== {sectionName} ==========");
    }

    public static void LogError(string message, Exception? ex = null)
    {
        Log($"ERROR: {message}");
        if (ex != null)
        {
            Log($"Exception: {ex.GetType().Name}");
            Log($"Message: {ex.Message}");
            Log($"StackTrace: {ex.StackTrace ?? "No stack trace available"}");
        }
    }

    public static string GetLogFilePath()
    {
        return "Debug Output Only - No File";
    }
}
