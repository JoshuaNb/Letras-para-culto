using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class PairingTests
{
    [Fact]
    public async Task APhoneCanPairThenReadStateAndOperateWithBearerToken()
    {
        using var server = Start();
        using var client = Client(server);
        Assert.Matches("^[0-9]{6}$", server.PairingCode);
        Assert.InRange(server.PairingExpiresAt, DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/state")).StatusCode);
        using var paired = await client.PostAsJsonAsync("/pair", new { code = server.PairingCode });
        Assert.Equal(HttpStatusCode.OK, paired.StatusCode);
        var token = (await paired.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        Assert.Equal(server.Token, token);
        Assert.Equal("no-store", paired.Headers.CacheControl?.ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var state = await client.GetFromJsonAsync<JsonElement>("/state");
        Assert.Equal("Paz e luz", state.GetProperty("text").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/command", new { command = "next" })).StatusCode);
    }

    [Fact]
    public async Task InvalidCodeDoesNotReturnAnAccessToken()
    {
        using var server = Start();
        using var client = Client(server);
        var wrong = server.PairingCode == "123456" ? "654321" : "123456";
        using var response = await client.PostAsJsonAsync("/pair", new { code = wrong });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.TryGetProperty("token", out _));
        Assert.DoesNotContain(server.Token, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{\"code\":123456}")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{broken")]
    public async Task PairingRequiresAJsonObjectWithAStringCode(string payload)
    {
        using var server = Start();
        using var client = Client(server);
        using var response = await client.PostAsync("/pair", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(server.Token, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("１２３４５６")]
    [InlineData("12 456")]
    public async Task PairingAcceptsOnlySixAsciiDigits(string code)
    {
        using var server = Start();
        using var client = Client(server);
        using var response = await client.PostAsJsonAsync("/pair", new { code });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/jsonp")]
    public async Task PairingRejectsOtherContentTypes(string contentType)
    {
        using var server = Start();
        using var client = Client(server);
        using var response = await client.PostAsync("/pair", new StringContent(JsonSerializer.Serialize(new { code = server.PairingCode }), Encoding.UTF8, contentType));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PairingNeverAcceptsACodeFromTheQueryString()
    {
        using var server = Start();
        using var client = Client(server);
        using var response = await client.PostAsJsonAsync("/pair?code=" + server.PairingCode, new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(server.Token, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PairingCodeExpiresButAnAlreadyPairedOperatorKeepsWorking()
    {
        var clock = new ManualClock();
        using var server = Start(clock);
        using var client = Client(server);
        var code = server.PairingCode;
        using var paired = await client.PostAsJsonAsync("/pair", new { code });
        Assert.Equal(HttpStatusCode.OK, paired.StatusCode);
        clock.Advance(TimeSpan.FromMinutes(10));
        using var expired = await client.PostAsJsonAsync("/pair", new { code });
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.DoesNotContain(server.Token, await expired.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/state")).StatusCode);
    }

    [Fact]
    public async Task FiveAttemptsPerMinuteAreAllowedPerAddressThenPairingIsLimited()
    {
        var clock = new ManualClock();
        using var server = Start(clock);
        using var client = Client(server);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
    }

    [Fact]
    public async Task GlobalLimitPreventsBruteForceAcrossDifferentLocalAddresses()
    {
        var clock = new ManualClock();
        using var server = Start(clock);
        for (var address = 2; address < 8; address++)
        {
            using var client = Client(server, IPAddress.Parse($"127.0.0.{address}"));
            for (var attempt = 0; attempt < 5; attempt++)
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
        }
        using var extra = Client(server, IPAddress.Parse("127.0.0.8"));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await extra.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await extra.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
    }

    [Fact]
    public async Task StoppingClearsPairingAndRestartingRevokesTheIssuedToken()
    {
        using var server = Start();
        using var client = Client(server);
        var port = server.Port;
        var oldToken = server.Token;
        server.Stop();
        Assert.Empty(server.PairingCode);
        Assert.Empty(server.Token);
        Assert.Equal(default, server.PairingExpiresAt);
        server.Start(port, () => new { text = "Novo controle" }, (_, _) => { }, IPAddress.Loopback);
        Assert.NotEqual(oldToken, server.Token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/state")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/pair", new { code = server.PairingCode })).StatusCode);
    }

    [Fact]
    public async Task ExistingBrowserControlAndBearerProtocolRemainAvailable()
    {
        using var server = Start();
        using var client = Client(server);
        using var browser = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, browser.StatusCode);
        Assert.Contains("Worship Studio", await browser.Content.ReadAsStringAsync());
        Assert.DoesNotContain(server.PairingCode, await browser.Content.ReadAsStringAsync());
        Assert.DoesNotContain(server.Token, await browser.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/state")).StatusCode);
    }

    private static RemoteServer Start(TimeProvider? clock = null)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var server = new RemoteServer(clock);
        server.Start(port, () => new { title = "Adoração", text = "Paz e luz", index = 0, count = 2 }, (_, _) => { }, IPAddress.Loopback);
        return server;
    }

    private static HttpClient Client(RemoteServer server, IPAddress? source = null)
    {
        var handler = new SocketsHttpHandler { UseProxy = false };
        if (source != null) handler.ConnectCallback = async (context, token) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Bind(new IPEndPoint(source, 0));
                await socket.ConnectAsync(context.DnsEndPoint, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); throw; }
        };
        return new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{server.Port}"), Timeout = TimeSpan.FromSeconds(4) };
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
