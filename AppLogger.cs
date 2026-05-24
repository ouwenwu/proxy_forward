using System.Text;

namespace ProxyForward;

public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        AppPaths.Ensure();
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
        lock (SyncRoot)
        {
            File.AppendAllText(AppPaths.LogPath, line, Encoding.UTF8);
        }
    }
}
