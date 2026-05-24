using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace ProxyForward;

public sealed class ForwardSession : IDisposable
{
    private SshClient? _client;
    private ForwardedPortRemote? _port;

    public ForwardConfig Config { get; }
    public ForwardStatus Status { get; } = new();

    public bool IsRunning =>
        _client is { IsConnected: true } &&
        _port is { IsStarted: true };

    public ForwardSession(ForwardConfig config)
    {
        Config = config;
    }

    public Task StartAsync()
    {
        return Task.Run(() =>
        {
            Status.State = ForwardState.Starting;
            Status.Message = "启动中";
            AppLogger.Info($"启动转发：{Config.Name} {Config.DisplayRemote} -> {Config.DisplayLocal}");

            Stop();

            EnsureLocalEndpointAvailable();
            _client = CreateClient();
            _client.KeepAliveInterval = TimeSpan.FromSeconds(30);
            _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(12);
            _client.Connect();

            _port = new ForwardedPortRemote(
                Config.RemoteBindHost,
                (uint)Config.RemoteBindPort,
                Config.LocalHost,
                (uint)Config.LocalPort);

            _port.Exception += (_, args) =>
            {
                Status.State = ForwardState.Failed;
                Status.Message = TranslateException(args.Exception);
                AppLogger.Error($"转发异常：{Config.Name}", args.Exception);
            };

            _client.AddForwardedPort(_port);
            _port.Start();

            Status.State = ForwardState.Running;
            Status.Message = "运行中";
            AppLogger.Info($"转发已启动：{Config.Name}");
        });
    }

    public Task TestAsync()
    {
        return Task.Run(() =>
        {
            EnsureLocalEndpointAvailable();
            using var client = CreateClient();
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
            client.Connect();
            client.Disconnect();
        });
    }

    public void Stop()
    {
        try
        {
            if (_port is not null)
            {
                if (_port.IsStarted)
                {
                    _port.Stop();
                }

                _client?.RemoveForwardedPort(_port);
                _port.Dispose();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"停止端口转发失败：{Config.Name}", ex);
        }
        finally
        {
            _port = null;
        }

        try
        {
            if (_client is not null)
            {
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                }

                _client.Dispose();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"断开 SSH 失败：{Config.Name}", ex);
        }
        finally
        {
            _client = null;
            Status.State = ForwardState.Stopped;
            Status.Message = "已停止";
        }
    }

    public void RefreshState()
    {
        if (Status.State == ForwardState.Running && !IsRunning)
        {
            Status.State = ForwardState.Stopped;
            Status.Message = "连接已断开";
            AppLogger.Info($"转发已断开：{Config.Name}");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public static string TranslateException(Exception ex)
    {
        return ex switch
        {
            SshAuthenticationException => "认证失败，请检查用户名、密码或密钥。",
            SshConnectionException => "SSH 连接失败，请检查服务器地址、端口或网络。",
            SocketException => "网络连接失败，请检查服务器或本机代理端口。",
            SshException when ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase) ||
                              ex.Message.Contains("forward", StringComparison.OrdinalIgnoreCase) ||
                              ex.Message.Contains("bind", StringComparison.OrdinalIgnoreCase)
                => "远程端口可能已被占用，或服务器不允许远程端口转发。",
            InvalidOperationException => ex.Message,
            _ => ex.Message
        };
    }

    private SshClient CreateClient()
    {
        if (Config.AuthMode == AuthMode.PrivateKey)
        {
            if (string.IsNullOrWhiteSpace(Config.PrivateKeyPath) || !File.Exists(Config.PrivateKeyPath))
            {
                throw new InvalidOperationException("私钥文件不存在。");
            }

            var keyFile = new PrivateKeyFile(Config.PrivateKeyPath);
            return new SshClient(Config.ServerHost, Config.SshPort, Config.Username, keyFile);
        }

        var password = SecretProtector.Unprotect(Config.EncryptedPassword);
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("未保存密码。");
        }

        return new SshClient(Config.ServerHost, Config.SshPort, Config.Username, password);
    }

    private void EnsureLocalEndpointAvailable()
    {
        using var tcpClient = new TcpClient();
        var connectTask = tcpClient.ConnectAsync(Config.LocalHost, Config.LocalPort);
        if (!connectTask.Wait(TimeSpan.FromSeconds(3)) || !tcpClient.Connected)
        {
            throw new InvalidOperationException($"本机 {Config.LocalHost}:{Config.LocalPort} 不可连接。");
        }
    }
}
