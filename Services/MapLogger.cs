using System;
using System.IO;
using System.Text;

namespace AntMissionManager.Services;

public static class MapLogger
{
    private static readonly string LogFilePath;
    private static readonly object LockObject = new object();

    static MapLogger()
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AntMissionManager", "Logs");
        Directory.CreateDirectory(logDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        LogFilePath = Path.Combine(logDir, $"MapDebug_{timestamp}.log");

        Log("=== Map Logger Started ===");
        Log($"Log file: {LogFilePath}");
    }

    public static void Log(string message)
    {
        try
        {
            lock (LockObject)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] {message}";

                // Write to file
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine, Encoding.UTF8);

                // Also write to debug output
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
        return LogFilePath;
    }
}
