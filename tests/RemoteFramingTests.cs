using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class RemoteFramingTests
{
    [Fact]
    public async Task FragmentedLengthAndChunkedBodiesBothReturnThePairingToken()
    {
        using var server = Start();
        var body = JsonSerializer.Serialize(new { code = server.PairingCode });
        var response = await Send(server, new[]
        {
            Header("/pair", $"Content-Length: {body.Length}\r\n"), body[..3], body[3..9], body[9..]
        });
        Assert.StartsWith("HTTP/1.1 200", response);
        Assert.Equal(server.Token, Token(response));

        response = await Send(server, new[]
        {
            Header("/pair", "Transfer-Encoding: chunked\r\n"), "3\r\n", body[..1], body[1..3], "\r\n",
            (body.Length - 3).ToString("X") + "\r\n", body[3..], "\r\n0\r\n\r\n"
        });
        Assert.StartsWith("HTTP/1.1 200", response);
        Assert.Equal(server.Token, Token(response));
    }

    [Fact]
    public async Task TruncatedBodyClosesWithoutAuthenticatingOrOperating()
    {
        var commands = 0;
        using var server = Start(() => Interlocked.Increment(ref commands));
        var response = await Send(server, new[] { Header("/pair", "Content-Length: 20\r\n"), "{}" }, truncate: true);
        Assert.Empty(response);
        Assert.Equal(0, commands);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"http://127.0.0.1:{server.Port}/state")).StatusCode);
    }

    [Fact]
    public async Task FragmentedCommandRequiresAuthorizationAndOperatesOnce()
    {
        var commands = 0;
        using var server = Start(() => Interlocked.Increment(ref commands));
        var body = "{\"command\":\"next\"}";
        var response = await Send(server, new[] { Header("/command", $"Content-Length: {body.Length}\r\n"), body });
        Assert.StartsWith("HTTP/1.1 401", response);
        Assert.Equal(0, commands);
        response = await Send(server, new[]
        {
            Header("/command", $"Content-Length: {body.Length}\r\nAuthorization: Bearer {server.Token}\r\n"), body[..6], body[6..]
        });
        Assert.StartsWith("HTTP/1.1 200", response);
        Assert.Equal(1, commands);
    }

    private static string Token(string response)
    {
        using var json = JsonDocument.Parse(response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]);
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private static RemoteServer Start(Action? command = null)
    {
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var server = new RemoteServer();
        server.Start(port, () => new { title = "Prova", text = "Paz", count = 1 }, (_, _) => command?.Invoke(), IPAddress.Loopback);
        return server;
    }

    private static string Header(string path, string headers) => $"POST {path} HTTP/1.1\r\nHost: localhost\r\nContent-Type: application/json\r\n{headers}Connection: close\r\n\r\n";

    private static async Task<string> Send(RemoteServer server, IEnumerable<string> fragments, bool truncate = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        using var stream = client.GetStream();
        foreach (var fragment in fragments)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(fragment), timeout.Token);
            await Task.Delay(15, timeout.Token);
        }
        if (truncate) client.Client.Shutdown(SocketShutdown.Send);
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, timeout.Token);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
