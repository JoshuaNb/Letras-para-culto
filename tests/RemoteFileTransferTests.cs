using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class RemoteFileTransferTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-transfer-" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        for (var retry = 0; retry < 100; retry++)
        {
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); return; }
            catch (IOException) when (retry < 99) { Thread.Sleep(20); }
        }
    }

    [Fact]
    public void InboxConstructionIsLazyAndDoesNotCreateFolders()
    {
        var store = new ReceivedFileStore(_directory);
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(store.List());
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task BinaryUnicodeNameAndHashAreStoredThenListedWithoutAnAbsolutePath()
    {
        using var server = Start();
        using var client = Client(server);
        ReceivedFile? notification = null;
        server.FileReceived += file => notification = file;
        var bytes = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();
        using var response = await Upload(client, "Adoração + paz 🌿.mp4", bytes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var file = Assert.Single(server.ReceivedFiles.List());
        Assert.Equal(notification, file);
        Assert.Equal("Adoração + paz 🌿.mp4", file.DisplayName);
        Assert.Equal(bytes.LongLength, file.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), file.Sha256);
        Assert.Equal(bytes, File.ReadAllBytes(server.ReceivedFiles.GetFilePath(file.Id)));
        using var listing = JsonDocument.Parse(await client.GetStringAsync("/files"));
        Assert.Equal(2048, listing.RootElement.GetProperty("maxFileBytes").GetInt64());
        var item = listing.RootElement.GetProperty("files")[0];
        Assert.Equal(new[] { "displayName", "id", "receivedAt", "sha256", "size" }, item.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task SameFileNameNeverOverwritesAnEarlierReceipt()
    {
        using var server = Start();
        using var client = Client(server);
        using var first = await Upload(client, "louvor.txt", Encoding.UTF8.GetBytes("Primeira letra"));
        using var second = await Upload(client, "louvor.txt", Encoding.UTF8.GetBytes("Segunda letra"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var files = server.ReceivedFiles.List();
        Assert.Equal(2, files.Count);
        Assert.NotEqual(files[0].Id, files[1].Id);
        Assert.Equal(new[] { "Primeira letra", "Segunda letra" }, files.Select(file => File.ReadAllText(server.ReceivedFiles.GetFilePath(file.Id))).OrderBy(text => text));
        Assert.Equal(2, new ReceivedFileStore(_directory).List().Count);
    }

    [Theory]
    [InlineData("../fora.txt")]
    [InlineData("C:\\fora.txt")]
    [InlineData("CON.txt")]
    [InlineData("COM¹.txt")]
    [InlineData("NUL")]
    [InlineData("nome. ")]
    [InlineData(".worship-receipt.json")]
    [InlineData("nome\nruim.txt")]
    public async Task InvalidWindowsNamesAreRejectedBeforeCreatingFiles(string name)
    {
        using var server = Start();
        using var client = Client(server);
        using var response = await Upload(client, name, new byte[] { 1, 2 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(server.ReceivedFiles.List());
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task AuthenticationIsCheckedBeforeReadingADeclaredLargeBody()
    {
        using var server = Start();
        var response = await Send(server, Header(server, "Content-Length: 999999999\r\n", authenticated: false));
        Assert.StartsWith("HTTP/1.1 401", response);
        Assert.False(Directory.Exists(_directory));
        using var client = Client(server);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/files")).StatusCode);
    }

    [Fact]
    public async Task DeclaredSizeLimitIsRejectedWithoutWaitingForTheBody()
    {
        using var server = Start();
        var response = await Send(server, Header(server, "Content-Length: 2049\r\n"));
        Assert.StartsWith("HTTP/1.1 413", response);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ChunkedBodyWithFragmentedFramesIsReceivedExactly()
    {
        using var server = Start();
        var response = await Send(server, Header(server, "Transfer-Encoding: chunked\r\n"), "2\r\n", "o", "i", "\r\n3\r\n", " +!", "\r\n0\r\n\r\n");
        Assert.StartsWith("HTTP/1.1 201", response);
        Assert.Equal("oi +!", File.ReadAllText(server.ReceivedFiles.GetFilePath(Assert.Single(server.ReceivedFiles.List()).Id)));
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task ChunkSizeLimitRejectsBeforeReadingTheOversizedChunk()
    {
        using var server = Start();
        var response = await Send(server, Header(server, "Transfer-Encoding: chunked\r\n"), "801\r\n");
        Assert.StartsWith("HTTP/1.1 413", response);
        Assert.Empty(server.ReceivedFiles.List());
        AssertNoPartial(server.ReceivedFiles);
    }

    [Theory]
    [InlineData("Content-Length: 1\r\nTransfer-Encoding: chunked\r\n")]
    [InlineData("Content-Length: -1\r\n")]
    [InlineData("Content-Length: 1\r\nContent-Length: 2\r\n")]
    [InlineData("Transfer-Encoding: gzip\r\n")]
    public async Task AmbiguousOrInvalidFramingDoesNotCommitAFile(string framing)
    {
        using var server = Start();
        Assert.StartsWith("HTTP/1.1 400", await Send(server, Header(server, framing)));
        Assert.Empty(server.ReceivedFiles.List());
        AssertNoPartial(server.ReceivedFiles);
    }

    [Theory]
    [InlineData("%FF")]
    [InlineData("%ZZ")]
    public async Task InvalidEncodedUtf8NameIsRejected(string name)
    {
        using var server = Start();
        var header = Header(server, "Content-Length: 0\r\n").Replace("X-File-Name: arquivo.bin", "X-File-Name: " + name);
        Assert.StartsWith("HTTP/1.1 400", await Send(server, header));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task TruncatedBodyNeverBecomesACompletedReceipt()
    {
        using var server = Start();
        using var socket = await OpenPartial(server, 40, "abc");
        var stream = socket.GetStream();
        socket.Client.Shutdown(SocketShutdown.Send);
        var response = await Response(socket, stream);
        Assert.StartsWith("HTTP/1.1 400", response);
        Assert.Empty(server.ReceivedFiles.List());
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task StopRevokesAndCleansAnUploadInProgress()
    {
        using var server = Start();
        using var socket = await OpenPartial(server, 1000, "abc");
        await WaitUntil(() => server.ReceivedFiles.IsReceiving);
        server.Stop();
        await WaitUntil(() => !server.ReceivedFiles.IsReceiving);
        Assert.Empty(server.ReceivedFiles.List());
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task OperatorCommandsAndListingStayAvailableDuringAnUpload()
    {
        var commands = 0;
        using var server = Start(command: () => Interlocked.Increment(ref commands));
        using var socket = await OpenPartial(server, 1000, "abc");
        await WaitUntil(() => server.ReceivedFiles.IsReceiving);
        using var client = Client(server);
        using var content = new StringContent("{\"command\":\"next\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/command", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, commands);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/files")).StatusCode);
        socket.Dispose();
        await WaitUntil(() => !server.ReceivedFiles.IsReceiving);
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task SimultaneousUploadIsRejectedWithoutDisturbingTheFirst()
    {
        using var server = Start();
        using var socket = await OpenPartial(server, 1000, "abc");
        await WaitUntil(() => server.ReceivedFiles.IsReceiving);
        using var client = Client(server);
        using var response = await Upload(client, "segundo.txt", new byte[] { 1 });
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.True(server.ReceivedFiles.IsReceiving);
        socket.Dispose();
        await WaitUntil(() => !server.ReceivedFiles.IsReceiving);
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task StorageQuotaPreservesPreviousFilesAndRejectsTheNextBody()
    {
        using var server = Start(maximumFile: 20, maximumStorage: 12);
        using var client = Client(server);
        using var first = await Upload(client, "primeiro.bin", new byte[8]);
        using var second = await Upload(client, "segundo.bin", new byte[8]);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal((HttpStatusCode)507, second.StatusCode);
        Assert.Single(server.ReceivedFiles.List());
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task EmptyFilesAndUninterpretedExecutableBytesAreAccepted()
    {
        using var server = Start();
        using var client = Client(server);
        using var empty = await Upload(client, "vazio.txt", Array.Empty<byte>());
        using var executable = await Upload(client, "conteúdo.exe", new byte[] { 0x4D, 0x5A, 0, 255 });
        Assert.Equal(HttpStatusCode.Created, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Created, executable.StatusCode);
        Assert.Equal(2, server.ReceivedFiles.List().Count);
        Assert.Contains(server.ReceivedFiles.List(), file => file.Size == 0 && file.Sha256 == Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant());
    }

    [Fact]
    public async Task AFileNamedLikeTheInternalPartialBufferStillArrivesAsACompleteFile()
    {
        using var server = Start();
        using var client = Client(server);
        using var response = await Upload(client, "CONTENT.PARTIAL", new byte[] { 3, 2, 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var file = Assert.Single(server.ReceivedFiles.List());
        Assert.Equal("CONTENT.PARTIAL", file.DisplayName);
        Assert.Equal(new byte[] { 3, 2, 1 }, File.ReadAllBytes(server.ReceivedFiles.GetFilePath(file.Id)));
        AssertNoPartial(server.ReceivedFiles);
    }

    [Fact]
    public async Task UnknownLengthChunkedUploadCannotExceedTheRemainingStorageQuota()
    {
        using var server = Start(maximumFile: 20, maximumStorage: 12);
        using var client = Client(server);
        using var first = await Upload(client, "primeiro.bin", new byte[8]);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var response = await Send(server, Header(server, "Transfer-Encoding: chunked\r\n"), "8\r\n12345678\r\n0\r\n\r\n");
        Assert.StartsWith("HTTP/1.1 507", response);
        Assert.Single(server.ReceivedFiles.List());
        AssertNoPartial(server.ReceivedFiles);
    }

    private RemoteServer Start(long maximumFile = 2048, long maximumStorage = 8192, Action? command = null)
    {
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var store = new ReceivedFileStore(_directory, maximumFile, maximumStorage);
        var server = new RemoteServer(receivedFiles: store);
        server.Start(port, () => new { title = "Envio", text = "Paz" }, (_, _) => command?.Invoke(), IPAddress.Loopback);
        return server;
    }

    private static HttpClient Client(RemoteServer server)
    {
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Port}"), Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);
        return client;
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string name, byte[] bytes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/files");
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(name));
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return await client.SendAsync(request);
    }

    private static string Header(RemoteServer server, string framing, bool authenticated = true) =>
        "POST /files HTTP/1.1\r\nHost: localhost\r\nContent-Type: application/octet-stream\r\nX-File-Name: arquivo.bin\r\n" +
        (authenticated ? "Authorization: Bearer " + server.Token + "\r\n" : "") + framing + "Connection: close\r\n\r\n";

    private static async Task<TcpClient> OpenPartial(RemoteServer server, int length, string partial)
    {
        var socket = new TcpClient { NoDelay = true };
        await socket.ConnectAsync(IPAddress.Loopback, server.Port);
        await socket.GetStream().WriteAsync(Encoding.ASCII.GetBytes(Header(server, $"Content-Length: {length}\r\n") + partial));
        return socket;
    }

    private static async Task<string> Send(RemoteServer server, params string[] fragments)
    {
        using var socket = new TcpClient { NoDelay = true };
        await socket.ConnectAsync(IPAddress.Loopback, server.Port);
        foreach (var fragment in fragments)
        {
            await socket.GetStream().WriteAsync(Encoding.ASCII.GetBytes(fragment));
            await Task.Delay(10);
        }
        return await Response(socket);
    }

    private static async Task<string> Response(TcpClient socket, NetworkStream? existing = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var output = new MemoryStream();
        await (existing ?? socket.GetStream()).CopyToAsync(output, timeout.Token);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void AssertNoPartial(ReceivedFileStore store)
    {
        if (Directory.Exists(store.DirectoryPath)) Assert.Empty(Directory.EnumerateDirectories(store.DirectoryPath, ".incoming-*"));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var retry = 0; retry < 100; retry++) { if (condition()) return; await Task.Delay(20); }
        Assert.True(condition(), "A transferência não concluiu a limpeza a tempo.");
    }
}
