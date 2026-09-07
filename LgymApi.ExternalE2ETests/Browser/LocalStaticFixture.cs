using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LgymApi.ExternalE2ETests.Browser;

internal sealed class LocalStaticFixture : IAsyncDisposable
{
    private const string HomePage = "<!doctype html><title>External browser lease fixture</title><main>Local static fixture</main>";
    private const string WorkerScript = "self.addEventListener('install', () => self.skipWaiting()); self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));";
    private readonly CancellationTokenSource _cancellationSource = new();
    private readonly TcpListener _listener;
    private readonly Task _serveTask;

    private LocalStaticFixture(TcpListener listener)
    {
        _listener = listener;
        HomeUri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        _serveTask = ServeAsync();
    }

    public Uri HomeUri { get; }

    public static Task<LocalStaticFixture> StartAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return Task.FromResult(new LocalStaticFixture(listener));
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationSource.CancelAsync();
        _listener.Stop();

        try
        {
            await _serveTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_cancellationSource.IsCancellationRequested)
        {
        }

        _cancellationSource.Dispose();
    }

    private async Task ServeAsync()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(_cancellationSource.Token);
                _ = ServeClientAsync(client, _cancellationSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var ownedClient = client;
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        var isWorkerRequest = requestLine?.Contains("/worker.js", StringComparison.Ordinal) is true;
        var content = isWorkerRequest ? WorkerScript : HomePage;
        var contentType = isWorkerRequest ? "application/javascript" : "text/html";
        var bytes = Encoding.UTF8.GetBytes(content);
        var headers = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nCache-Control: no-store\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
    }
}
