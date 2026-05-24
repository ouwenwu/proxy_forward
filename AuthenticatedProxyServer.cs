using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProxyForward;

public sealed class AuthenticatedProxyServer : IDisposable
{
    private readonly string _upstreamHost;
    private readonly int _upstreamPort;
    private readonly string _expectedAuthHeader;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;

    public int Port { get; private set; }

    public AuthenticatedProxyServer(string upstreamHost, int upstreamPort, string username, string password)
    {
        _upstreamHost = upstreamHost;
        _upstreamPort = upstreamPort;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _expectedAuthHeader = $"Basic {token}";
    }

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoopAsync);
        AppLogger.Info($"Authenticated proxy started on 127.0.0.1:{Port}, upstream {_upstreamHost}:{_upstreamPort}");
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch
        {
            // Ignore shutdown races.
        }
        _cts.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => HandleClientAsync(client, _cts.Token));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Authenticated proxy accept failed.", ex);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientOwner = client;
        try
        {
            using var clientStream = client.GetStream();
            var headerBytes = await ReadHeaderAsync(clientStream, cancellationToken);
            if (headerBytes.Length == 0)
            {
                return;
            }

            var headerText = Encoding.ASCII.GetString(headerBytes);
            if (!IsAuthorized(headerText))
            {
                await WriteProxyAuthRequiredAsync(clientStream, cancellationToken);
                return;
            }

            var sanitizedHeader = RemoveProxyAuthorization(headerText);
            using var upstream = new TcpClient();
            await upstream.ConnectAsync(_upstreamHost, _upstreamPort, cancellationToken);
            using var upstreamStream = upstream.GetStream();

            var sanitizedBytes = Encoding.ASCII.GetBytes(sanitizedHeader);
            await upstreamStream.WriteAsync(sanitizedBytes, cancellationToken);

            var clientToUpstream = PumpAsync(clientStream, upstreamStream, cancellationToken);
            var upstreamToClient = PumpAsync(upstreamStream, clientStream, cancellationToken);
            await Task.WhenAny(clientToUpstream, upstreamToClient);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error("Authenticated proxy client failed.", ex);
        }
    }

    private static async Task<byte[]> ReadHeaderAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var data = new List<byte>(4096);

        while (data.Count < 65536)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            data.AddRange(buffer.AsSpan(0, read).ToArray());
            if (EndsWithHeader(data))
            {
                break;
            }
        }

        return data.ToArray();
    }

    private static bool EndsWithHeader(List<byte> data)
    {
        var count = data.Count;
        return count >= 4 &&
               data[count - 4] == '\r' &&
               data[count - 3] == '\n' &&
               data[count - 2] == '\r' &&
               data[count - 1] == '\n';
    }

    private bool IsAuthorized(string headerText)
    {
        var lines = headerText.Split(["\r\n"], StringSplitOptions.None);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) &&
                value.Equals(_expectedAuthHeader, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string RemoveProxyAuthorization(string headerText)
    {
        var headerEnd = headerText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headerOnly = headerEnd >= 0 ? headerText[..headerEnd] : headerText;
        var lines = headerOnly.Split(["\r\n"], StringSplitOptions.None);
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.Append(line).Append("\r\n");
        }

        builder.Append("\r\n");
        return builder.ToString();
    }

    private static Task WriteProxyAuthRequiredAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        const string response =
            "HTTP/1.1 407 Proxy Authentication Required\r\n" +
            "Proxy-Authenticate: Basic realm=\"ProxyForward\"\r\n" +
            "Content-Length: 0\r\n" +
            "Connection: close\r\n\r\n";
        return stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken).AsTask();
    }

    private static async Task PumpAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
    }
}
