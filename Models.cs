using System.Text.Json.Serialization;

namespace ProxyForward;

public enum AuthMode
{
    Password,
    PrivateKey
}

public sealed class ForwardConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ServerHost { get; set; } = "";
    public int SshPort { get; set; } = 22;
    public string Username { get; set; } = "";
    public AuthMode AuthMode { get; set; } = AuthMode.Password;
    public string EncryptedPassword { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public string LocalHost { get; set; } = "127.0.0.1";
    public int LocalPort { get; set; } = 7897;
    public string RemoteBindHost { get; set; } = "127.0.0.1";
    public int RemoteBindPort { get; set; } = 43897;
    public bool ProxyAuthEnabled { get; set; }
    public string ProxyAuthUsername { get; set; } = "";
    public string EncryptedProxyAuthPassword { get; set; } = "";
    public string Notes { get; set; } = "";

    [JsonIgnore]
    public string DisplayServer => $"{ServerHost}:{SshPort}";

    [JsonIgnore]
    public string DisplayLocal => $"{LocalHost}:{LocalPort}";

    [JsonIgnore]
    public string DisplayRemote => $"{RemoteBindHost}:{RemoteBindPort}";

    [JsonIgnore]
    public string DisplayProxyAuth => ProxyAuthEnabled ? ProxyAuthUsername : "Off";
}

public enum ForwardState
{
    Stopped,
    Starting,
    Running,
    Failed
}

public sealed class ForwardStatus
{
    public ForwardState State { get; set; } = ForwardState.Stopped;
    public string Message { get; set; } = "已停止";
}
