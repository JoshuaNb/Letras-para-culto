using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WorshipStudio.Models;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class IntegrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-integration-" + Guid.NewGuid().ToString("N"));
    public IntegrationTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public async Task RemoteRequiresTokenAndExecutesOnlyValidAuthenticatedCommands()
    {
        using var server = new RemoteServer();
        var received = new List<(string Command, int? Index)>();
        server.Start(FreePort(), () => new { title = "Louvor à criação", text = "Paz", index = 0, count = 2 }, (command, index) => { lock(received) received.Add((command,index)); }, IPAddress.Loopback);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Port}"), Timeout = TimeSpan.FromSeconds(4) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/state")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/command", new { command = "next" })).StatusCode);
        Assert.Empty(received);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);
        var state = await client.GetFromJsonAsync<JsonElement>("/state");
        Assert.Equal("Louvor à criação", state.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/command", new { command = "next" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/command", new { command = "slide", index = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/command", new { command = "delete" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/command", new { command = "slide", index = -1 })).StatusCode);
        Assert.Equal(new[] { ("next", (int?)null), ("slide", (int?)1) }, received);
    }

    [Fact]
    public async Task RestartingRemoteRevokesPreviousAccessToken()
    {
        using var server = new RemoteServer();
        var port = FreePort();
        server.Start(port, () => new { title = "Paz" }, (_, _) => { }, IPAddress.Loopback);
        var oldToken = server.Token;
        server.Stop();
        Assert.False(server.IsRunning);
        server.Start(port, () => new { title = "Paz" }, (_, _) => { }, IPAddress.Loopback);
        Assert.NotEqual(oldToken, server.Token);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/state")).StatusCode);
    }

    [Fact]
    public void OpenLyricsImportPreservesLineBreaksAndDeclaredVerseOrder()
    {
        var path = Path.Combine(_directory, "song.xml");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="utf-8"?>
            <song xmlns="http://openlyrics.info/namespace/2009/song" version="0.8">
              <properties><titles><title>Graça e paz</title></titles><authors><author>Equipe local</author></authors><verseOrder>v1 c1 v1</verseOrder></properties>
              <lyrics><verse name="v1"><lines>Graça<br/>e paz</lines></verse><verse name="c1"><lines>Aleluia!</lines></verse></lyrics>
            </song>
            """);
        using var service = new HolyricsService();
        var song = Assert.Single(service.ImportFile(path));
        var slides = LyricParser.Parse(song.Lyrics);
        Assert.Equal("Graça e paz", song.Title);
        Assert.Equal("Equipe local", song.Artist);
        Assert.Equal(new[] { "v1", "c1", "v1" }, slides.Select(s=>s.Label));
        Assert.Equal("Graça\ne paz", slides[0].Text);
        Assert.Equal(slides[0].Text, slides[2].Text);
    }

    [Fact]
    public void XmlExternalEntitiesAreRejected()
    {
        var path = Path.Combine(_directory, "unsafe.xml");
        File.WriteAllText(path, "<!DOCTYPE song [<!ENTITY secret SYSTEM 'file:///C:/Windows/win.ini'>]><song><title>&secret;</title></song>");
        using var service = new HolyricsService();
        Assert.ThrowsAny<Exception>(() => service.ImportFile(path));
    }

    [Fact]
    public void JsonImportUnderstandsHolyricsSlideOrder()
    {
        var path = Path.Combine(_directory, "songs.json");
        File.WriteAllText(path, """{"data":[{"title":"Paz","artist":"Local","order":"2,1,2","slides":[{"slide_description":"Verso","text":"Luz"},{"slide_description":"Refrão","text":"Paz"}]}]}""");
        using var service = new HolyricsService();
        var slides = LyricParser.Parse(Assert.Single(service.ImportFile(path)).Lyrics);
        Assert.Equal(new[] { "Paz", "Luz", "Paz" }, slides.Select(s=>s.Text));
    }

    [Fact]
    public async Task HolyricsImportUsesOfficialPostApiAndFetchesMissingLyrics()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = Task.Run(async () =>
        {
            var first = await ServeOne(listener, """{"status":"ok","data":[{"id":"42","title":"Paz"}]}""");
            var second = await ServeOne(listener, """{"status":"ok","data":{"id":"42","title":"Paz","slides":[{"slide_description":"Verso","text":"Paz e luz"}]}}""");
            return (first, second);
        });
        using var service = new HolyricsService();
        var songs = await service.ImportLibraryAsync($"http://127.0.0.1:{port}", "test token");
        var requests = await served.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.StartsWith("POST /api/GetSongs?token=test%20token ", requests.first.Header);
        Assert.StartsWith("POST /api/GetLyrics?token=test%20token ", requests.second.Header);
        Assert.Contains("\"id\":\"42\"", requests.second.Body);
        Assert.Equal("Paz e luz", Assert.Single(LyricParser.Parse(Assert.Single(songs).Lyrics)).Text);
    }

    [Fact]
    public void HolyricsCurrentPresentationMapsSelectedSlideAndBlackout()
    {
        using var doc = JsonDocument.Parse("""{"name":"Paz","slide_number":2,"slide_type":"black","slides":[{"number":1,"text":"Luz"},{"number":2,"text":"Paz"}]}""");
        var presentation = HolyricsService.ParsePresentation(doc.RootElement);
        Assert.Equal("Paz", presentation.Text);
        Assert.Equal(1, presentation.Index);
        Assert.Equal(2, presentation.Count);
        Assert.True(presentation.Blackout);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<(string Header,string Body)> ServeOne(TcpListener listener, string response)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var client = await listener.AcceptTcpClientAsync(cts.Token);
        var stream = client.GetStream();
        var bytes = new List<byte>();
        var buffer = new byte[1];
        while (bytes.Count < 8192)
        {
            if (await stream.ReadAsync(buffer,cts.Token) == 0) throw new IOException("Unexpected end of mock request");
            bytes.Add(buffer[0]);
            if(bytes.Count>=4 && bytes[^4]==13 && bytes[^3]==10 && bytes[^2]==13 && bytes[^1]==10) break;
        }
        var header = Encoding.ASCII.GetString(bytes.ToArray());
        var lengthLine = header.Split("\r\n").FirstOrDefault(x=>x.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase));
        var length = lengthLine == null ? 0 : int.Parse(lengthLine.Split(':')[1].Trim());
        var body = new byte[length];
        await stream.ReadExactlyAsync(body,cts.Token);
        var payload = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"),cts.Token);
        await stream.WriteAsync(payload,cts.Token);
        return (header,Encoding.UTF8.GetString(body));
    }
}
