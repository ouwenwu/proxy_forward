namespace ProxyForward;

public static class AppPaths
{
    public static string AppDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProxyForward");

    public static string ConfigPath { get; } = Path.Combine(AppDirectory, "configs.json");

    public static string LogDirectory { get; } = Path.Combine(AppDirectory, "logs");

    public static string LogPath { get; } = Path.Combine(LogDirectory, "app.log");

    public static void Ensure()
    {
        Directory.CreateDirectory(AppDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
