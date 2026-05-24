using System.Text.Json;

namespace ProxyForward;

public sealed class ConfigRepository
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public List<ForwardConfig> Load()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.ConfigPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(AppPaths.ConfigPath);
            return JsonSerializer.Deserialize<List<ForwardConfig>>(json, _options) ?? [];
        }
        catch (Exception ex)
        {
            AppLogger.Error("读取配置失败。", ex);
            MessageBox.Show($"读取配置失败：{ex.Message}", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return [];
        }
    }

    public void Save(IReadOnlyCollection<ForwardConfig> configs)
    {
        AppPaths.Ensure();
        var json = JsonSerializer.Serialize(configs, _options);
        File.WriteAllText(AppPaths.ConfigPath, json);
    }
}
