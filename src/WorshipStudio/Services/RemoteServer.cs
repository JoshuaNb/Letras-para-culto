using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Globalization;

namespace WorshipStudio.Services;

/// <summary>A small HTTP server bound only while remote control is enabled. No admin URL reservation is required.</summary>
public sealed class RemoteServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Func<object>? _getState;
    private Action<string, int?>? _command;
    private readonly SemaphoreSlim _clients = new(8);
    private readonly ConcurrentDictionary<string, Queue<long>> _commandTimes = new();
    private readonly TimeProvider _timeProvider;
    private readonly object _pairingGate = new();
    private readonly Dictionary<string, Queue<long>> _pairingTimes = new();
    private readonly Queue<long> _allPairingTimes = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string Token { get; private set; } = "";
    public string PairingCode { get; private set; } = "";
    public DateTimeOffset PairingExpiresAt { get; private set; }
    public int Port { get; private set; }
    public bool IsRunning => _listener != null;
    public IReadOnlyList<string> Urls => GetLocalAddresses().Select(ip => $"http://{ip}:{Port}/?token={Token}").ToArray();
    public event Action<string>? Error;
    public ReceivedFileStore ReceivedFiles { get; }
    public event Action<ReceivedFile>? FileReceived;

    public RemoteServer(TimeProvider? timeProvider = null, ReceivedFileStore? receivedFiles = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        ReceivedFiles = receivedFiles ?? new ReceivedFileStore();
    }

    public void Start(int port, Func<object> getState, Action<string, int?> command, IPAddress? listenAddress = null)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Use uma porta entre 1024 e 65535.");
        Stop();
        _commandTimes.Clear();
        _getState = getState;
        _command = command;
        var listener = new TcpListener(listenAddress ?? IPAddress.Any, port);
        listener.Start(8);
        lock (_pairingGate)
        {
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            PairingCode = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            PairingExpiresAt = _timeProvider.GetUtcNow().AddMinutes(10);
        }
        Port = port;
        _listener = listener;
        _cts = new CancellationTokenSource();
        _ = AcceptLoop(listener, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        _cts?.Dispose();
        _cts = null;
        lock (_pairingGate)
        {
            Token = "";
            PairingCode = "";
            PairingExpiresAt = default;
            _pairingTimes.Clear();
            _allPairingTimes.Clear();
        }
    }

    private async Task AcceptLoop(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                if (!await _clients.WaitAsync(0, cancellationToken)) { client.Dispose(); continue; }
                // Inbox metadata may use synchronous filesystem APIs before the first network await.
                // Keep that work off the accept loop so projection commands can still connect.
                _ = Task.Run(() => Serve(client, cancellationToken));
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { Error?.Invoke("Controle remoto interrompido: " + ex.Message); }
    }

    private async Task Serve(TcpClient client, CancellationToken parentToken)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(parentToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var ct = timeout.Token;
            var gracefulClose = false;
            try
            {
                if (client.Client.RemoteEndPoint is not IPEndPoint remote || !IsLocalAddress(remote.Address)) return;
                client.NoDelay = true;
                var stream = client.GetStream();
                // Read exactly through the header terminator, without consuming body bytes into a buffered reader.
                var header = new List<byte>(512);
                var single = new byte[1];
                while (header.Count < 8192)
                {
                    if (await stream.ReadAsync(single, ct) == 0) return;
                    header.Add(single[0]);
                    if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
                }
                if (header.Count >= 8192) { await Reply(stream, 431, "text/plain", "Cabeçalho muito grande.", ct); return; }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                var first = lines[0].Split(' ');
                if (first.Length != 3 || !first[1].StartsWith('/')) { await Reply(stream, 400, "text/plain", "Requisição inválida.", ct); return; }
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var duplicateHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        var name = line[..colon].Trim();
                        if (headers.ContainsKey(name)) duplicateHeaders.Add(name);
                        headers[name] = line[(colon + 1)..].Trim();
                    }
                }
                var path = first[1].Split('?')[0];
                if (path == "/files")
                {
                    gracefulClose = first[0] == "POST";
                    // A file body can be large: authenticate and validate headers before reading a byte of it.
                    if (!headers.TryGetValue("Authorization", out var fileAuthorization) || !ValidToken(fileAuthorization))
                    { await Reply(stream, 401, "application/json", "{\"error\":\"Acesso expirado. Conecte novamente ao PC.\"}", ct); return; }
                    if (duplicateHeaders.Any(name => name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) || name.Equals("X-File-Name", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
                    { await Reply(stream, 400, "application/json", "{\"error\":\"Cabeçalhos de transferência ambíguos.\"}", ct); return; }
                    if (first[0] == "POST")
                    {
                        timeout.CancelAfter(TimeSpan.FromMinutes(15));
                        gracefulClose = await ReceiveFile(stream, headers, ct);
                    }
                    else if (first[0] == "GET")
                    {
                        try
                        {
                            await Reply(stream, 200, "application/json; charset=utf-8", JsonSerializer.Serialize(new
                            {
                                maxFileBytes = ReceivedFiles.MaximumFileBytes,
                                storageLimitBytes = ReceivedFiles.MaximumStorageBytes,
                                files = ReceivedFiles.List(100)
                            }, JsonOptions), ct);
                        }
                        catch (IOException) { await Reply(stream, 507, "application/json", "{\"error\":\"Não foi possível ler a pasta de recebidos no PC.\"}", ct); }
                        catch (UnauthorizedAccessException) { await Reply(stream, 507, "application/json", "{\"error\":\"Sem acesso à pasta de recebidos no PC.\"}", ct); }
                    }
                    else await Reply(stream, 405, "application/json", "{\"error\":\"Método de transferência inválido.\"}", ct);
                    return;
                }
                byte[] body;
                try { body = await ReadBody(stream, headers, ct); }
                catch (InvalidDataException) { await Reply(stream, 400, "application/json", "{\"error\":\"Corpo inválido ou muito grande\"}", ct); return; }
                if (first[0] == "GET" && path == "/")
                {
                    await Reply(stream, 200, "text/html; charset=utf-8", ControllerHtml, ct); return;
                }
                if (first[0] == "POST" && path == "/pair")
                {
                    await Pair(stream, headers, body, remote.Address, ct); return;
                }
                if (!headers.TryGetValue("Authorization", out var authorization) || !ValidToken(authorization))
                {
                    await Reply(stream, 401, "application/json", "{\"error\":\"Token inválido. Abra o endereço mostrado no computador.\"}", ct); return;
                }
                if (first[0] == "GET" && path == "/state")
                {
                    var state = _getState?.Invoke() ?? new { title = "Sem apresentação" };
                    await Reply(stream, 200, "application/json; charset=utf-8", JsonSerializer.Serialize(state, JsonOptions), ct);
                }
                else if (first[0] == "POST" && path == "/command")
                {
                    var clientKey = remote.Address.ToString();
                    var now = Environment.TickCount64;
                    if (_commandTimes.Count > 256) _commandTimes.Clear();
                    var times = _commandTimes.GetOrAdd(clientKey, _ => new Queue<long>());
                    bool rateLimited;
                    lock (times)
                    {
                        while (times.TryPeek(out var time) && now - time > 1000) times.Dequeue();
                        rateLimited = times.Count >= 20;
                        if (!rateLimited) times.Enqueue(now);
                    }
                    if (rateLimited)
                    { await Reply(stream, 429, "application/json", "{\"error\":\"Aguarde um instante\"}", ct); return; }
                    if (body.Length == 0)
                    { await Reply(stream, 400, "application/json", "{\"error\":\"Corpo inválido\"}", ct); return; }
                    if (!headers.TryGetValue("Content-Type", out var contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                    { await Reply(stream, 415, "application/json", "{\"error\":\"Use JSON\"}", ct); return; }
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("command", out var commandElement) || commandElement.ValueKind != JsonValueKind.String)
                    { await Reply(stream, 400, "application/json", "{\"error\":\"Comando inválido\"}", ct); return; }
                    var command = commandElement.GetString() ?? "";
                    int? index = doc.RootElement.TryGetProperty("index", out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n) ? n : null;
                    if (command is not ("next" or "previous" or "blackout" or "clear" or "slide" or "tap"))
                    { await Reply(stream, 400, "application/json", "{\"error\":\"Comando inválido\"}", ct); return; }
                    if (command == "slide" && (index == null || index < 0 || index > 10000))
                    { await Reply(stream, 400, "application/json", "{\"error\":\"Slide inválido\"}", ct); return; }
                    _command?.Invoke(command, index);
                    await Reply(stream, 200, "application/json", "{\"ok\":true}", ct);
                }
                else await Reply(stream, 404, "text/plain", "Não encontrado.", ct);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (JsonException) { }
            catch (KeyNotFoundException) { }
            catch (Exception ex) { Error?.Invoke("Controle remoto: " + ex.Message); }
            finally
            {
                // A rejected upload may already have body bytes in flight. Send FIN before closing,
                // and discard a small bounded tail so Windows does not reset a valid HTTP error response.
                if (gracefulClose) await GracefulClose(client).ConfigureAwait(false);
                _clients.Release();
            }
        }
    }

    private static async Task GracefulClose(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            client.Client.Shutdown(SocketShutdown.Send);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var buffer = new byte[8192];
            for (var received = 0; received < 64 * 1024;)
            {
                var count = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                if (count == 0) break;
                received += count;
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidOperationException or OperationCanceledException or ObjectDisposedException) { }
    }

    private async Task<bool> ReceiveFile(NetworkStream stream, Dictionary<string, string> headers, CancellationToken ct)
    {
        try
        {
            if (!headers.TryGetValue("Content-Type", out var contentType) || !contentType.Split(';')[0].Trim().Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                throw new ReceivedFileException(415, "Envie o arquivo como application/octet-stream.");
            if (!headers.TryGetValue("X-File-Name", out var encodedName)) throw new ReceivedFileException(400, "O nome do arquivo não foi informado.");
            var name = DecodeFileName(encodedName);
            long? length = null;
            var chunked = headers.TryGetValue("Transfer-Encoding", out var encoding);
            if (chunked)
            {
                if (!encoding!.Equals("chunked", StringComparison.OrdinalIgnoreCase) || headers.ContainsKey("Content-Length"))
                    throw new ReceivedFileException(400, "O enquadramento da transferência é inválido.");
            }
            else
            {
                if (!headers.TryGetValue("Content-Length", out var value)) throw new ReceivedFileException(411, "Informe o tamanho ou use transferência em blocos.");
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) throw new ReceivedFileException(400, "O tamanho do arquivo é inválido.");
                length = number;
            }
            if (headers.TryGetValue("Expect", out var expectation) && !expectation.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
                throw new ReceivedFileException(417, "A expectativa da transferência não é aceita.");
            var file = await ReceivedFiles.ReceiveAsync(name, length, async (target, token) =>
            {
                if (expectation != null) await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), token).ConfigureAwait(false);
                await CopyFileBody(stream, target, length, chunked, token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
            try { FileReceived?.Invoke(file); }
            catch (Exception ex) { AppLog.Write("Notificação de arquivo recebido: " + ex.Message); }
            await Reply(stream, 201, "application/json; charset=utf-8", JsonSerializer.Serialize(new { ok = true, file }, JsonOptions), ct);
            return false;
        }
        catch (ReceivedFileException ex)
        { await Reply(stream, ex.StatusCode, "application/json; charset=utf-8", JsonSerializer.Serialize(new { error = ex.Message }), ct); return true; }
        catch (ArgumentException)
        { await Reply(stream, 400, "application/json", "{\"error\":\"O nome do arquivo é inválido.\"}", ct); return true; }
    }

    private async Task CopyFileBody(NetworkStream source, Stream target, long? length, bool chunked, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        async Task Copy(long count)
        {
            while (count > 0)
            {
                var read = await ReadUpload(source, buffer.AsMemory(0, (int)Math.Min(buffer.Length, count)), ct).ConfigureAwait(false);
                if (read == 0) throw new ReceivedFileException(400, "A transferência foi interrompida antes do fim do arquivo.");
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                count -= read;
            }
        }
        if (!chunked) { await Copy(length!.Value).ConfigureAwait(false); return; }
        long received = 0;
        for (var chunks = 0; chunks < 1_000_000; chunks++)
        {
            var line = await ReadUploadLine(source, 128, ct).ConfigureAwait(false);
            if (!long.TryParse(line.Split(';')[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var count) || count < 0)
                throw new ReceivedFileException(400, "Um bloco da transferência é inválido.");
            if (count > ReceivedFiles.MaximumFileBytes - received) throw new ReceivedFileException(413, "O arquivo excede o limite de envio permitido pelo PC.");
            if (count == 0)
            {
                for (var trailer = 0; trailer < 16; trailer++)
                    if ((await ReadUploadLine(source, 512, ct).ConfigureAwait(false)).Length == 0) return;
                throw new ReceivedFileException(400, "O fim da transferência é inválido.");
            }
            await Copy(count).ConfigureAwait(false);
            received += count;
            if ((await ReadUploadLine(source, 2, ct).ConfigureAwait(false)).Length != 0) throw new ReceivedFileException(400, "Um bloco da transferência é inválido.");
        }
        throw new ReceivedFileException(400, "A transferência contém blocos demais.");
    }

    private static async Task<int> ReadUpload(NetworkStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(TimeSpan.FromSeconds(60));
        try { return await stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ReceivedFileException(408, "A transferência ficou sem receber dados por um minuto. Tente novamente."); }
    }

    private static async Task<string> ReadUploadLine(NetworkStream stream, int maximum, CancellationToken ct)
    {
        var bytes = new List<byte>();
        var next = new byte[1];
        while (bytes.Count <= maximum + 1)
        {
            if (await ReadUpload(stream, next, ct).ConfigureAwait(false) == 0) throw new ReceivedFileException(400, "A transferência foi interrompida antes do fim do arquivo.");
            bytes.Add(next[0]);
            if (bytes.Count >= 2 && bytes[^2] == 13 && bytes[^1] == 10) return Encoding.ASCII.GetString(bytes.ToArray(), 0, bytes.Count - 2);
        }
        throw new ReceivedFileException(400, "Um bloco da transferência é inválido.");
    }

    private static string DecodeFileName(string encoded)
    {
        if (encoded.Length > 2160) throw new ReceivedFileException(400, "O nome do arquivo é longo demais.");
        var bytes = new List<byte>(encoded.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            if (encoded[i] == '%')
            {
                if (i + 2 >= encoded.Length || !byte.TryParse(encoded.Substring(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                    throw new ReceivedFileException(400, "O nome do arquivo não está codificado corretamente.");
                bytes.Add(value);
                i += 2;
            }
            else
            {
                if (encoded[i] > 127) throw new ReceivedFileException(400, "O nome do arquivo deve usar codificação UTF-8.");
                bytes.Add((byte)encoded[i]);
            }
        }
        try { return ReceivedFileStore.ValidateName(new UTF8Encoding(false, true).GetString(bytes.ToArray())); }
        catch (DecoderFallbackException) { throw new ReceivedFileException(400, "O nome do arquivo não está em UTF-8 válido."); }
    }

    private bool ValidToken(string authorization)
    {
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.Ordinal) || Token.Length == 0) return false;
        var incoming = Encoding.UTF8.GetBytes(authorization[prefix.Length..]);
        var expected = Encoding.UTF8.GetBytes(Token);
        return incoming.Length == expected.Length && CryptographicOperations.FixedTimeEquals(incoming, expected);
    }

    private async Task Pair(NetworkStream stream, Dictionary<string, string> headers, byte[] body, IPAddress remote, CancellationToken ct)
    {
        // Pairing is local-only, short-lived, JSON-only and limited across both an address and the whole server.
        bool rateLimited;
        lock (_pairingGate)
        {
            var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            while (_allPairingTimes.TryPeek(out var time) && now - time >= 60_000) _allPairingTimes.Dequeue();
            foreach (var key in _pairingTimes.Keys.ToArray())
            {
                var queue = _pairingTimes[key];
                while (queue.TryPeek(out var time) && now - time >= 60_000) queue.Dequeue();
                if (queue.Count == 0) _pairingTimes.Remove(key);
            }
            var address = remote.ToString();
            if (!_pairingTimes.TryGetValue(address, out var attempts)) _pairingTimes[address] = attempts = new Queue<long>();
            rateLimited = attempts.Count >= 5 || _allPairingTimes.Count >= 30;
            if (!rateLimited) { attempts.Enqueue(now);_allPairingTimes.Enqueue(now); }
        }
        if (rateLimited)
        { await Reply(stream, 429, "application/json", "{\"error\":\"Muitas tentativas de pareamento. Aguarde um minuto.\"}", ct); return; }
        if (!headers.TryGetValue("Content-Type", out var contentType) || !contentType.Split(';', 2)[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase))
        { await Reply(stream, 415, "application/json", "{\"error\":\"Use JSON para parear o celular.\"}", ct); return; }
        string code;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("code", out var value) || value.ValueKind != JsonValueKind.String)
            { await Reply(stream, 400, "application/json", "{\"error\":\"Informe o código de seis dígitos mostrado no computador.\"}", ct); return; }
            code = value.GetString() ?? "";
        }
        catch (JsonException)
        { await Reply(stream, 400, "application/json", "{\"error\":\"Dados de pareamento inválidos.\"}", ct); return; }

        var status = 200;
        string response;
        lock (_pairingGate)
        {
            if (PairingCode.Length == 0 || Token.Length == 0)
            { status = 401; response = "{\"error\":\"Controle remoto desligado.\"}"; }
            else if (_timeProvider.GetUtcNow() >= PairingExpiresAt)
            { status = 410; response = "{\"error\":\"Código expirado. Reinicie o controle remoto no computador para gerar outro.\"}"; }
            else if (code.Length != 6 || code.Any(c => c is < '0' or > '9') ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(PairingCode)))
            { status = 401; response = "{\"error\":\"Código de pareamento inválido.\"}"; }
            else response = JsonSerializer.Serialize(new { token = Token });
        }
        await Reply(stream, status, "application/json; charset=utf-8", response, ct);
    }

    private static async Task<byte[]> ReadBody(NetworkStream stream, Dictionary<string, string> headers, CancellationToken ct)
    {
        if (headers.TryGetValue("Transfer-Encoding", out var encoding))
        {
            if (!string.Equals(encoding, "chunked", StringComparison.OrdinalIgnoreCase) || headers.ContainsKey("Content-Length")) throw new InvalidDataException();
            using var body = new MemoryStream();
            for (var chunks = 0; chunks < 128; chunks++)
            {
                var line = await ReadLine(stream, 128, ct);
                if (!int.TryParse(line.Split(';')[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var size) || size < 0 || body.Length + size > 2048) throw new InvalidDataException();
                if (size == 0)
                {
                    for (var trailer = 0; trailer < 16; trailer++)
                        if ((await ReadLine(stream, 512, ct)).Length == 0) return body.ToArray();
                    throw new InvalidDataException();
                }
                var bytes = new byte[size];
                await ReadExactly(stream, bytes, ct);
                await body.WriteAsync(bytes, ct);
                if ((await ReadLine(stream, 2, ct)).Length != 0) throw new InvalidDataException();
            }
            throw new InvalidDataException();
        }
        if (!headers.TryGetValue("Content-Length", out var lengthText)) return [];
        if (!int.TryParse(lengthText, out var length) || length < 0 || length > 2048) throw new InvalidDataException();
        var content = new byte[length];
        await ReadExactly(stream, content, ct);
        return content;
    }

    private static async Task ReadExactly(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        while (!buffer.IsEmpty)
        {
            var count = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (count == 0) throw new EndOfStreamException("Conexão encerrada antes do fim da mensagem.");
            buffer = buffer[count..];
        }
    }

    private static async Task<string> ReadLine(NetworkStream stream, int maximum, CancellationToken ct)
    {
        var bytes = new List<byte>();
        var next = new byte[1];
        while (bytes.Count <= maximum)
        {
            if (await stream.ReadAsync(next, ct) == 0) throw new IOException("Conexão encerrada.");
            bytes.Add(next[0]);
            if (bytes.Count >= 2 && bytes[^2] == 13 && bytes[^1] == 10) return Encoding.ASCII.GetString(bytes.ToArray(), 0, bytes.Count - 2);
        }
        throw new InvalidDataException();
    }

    private static async Task Reply(NetworkStream stream, int status, string contentType, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} OK\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, ct);
        await stream.WriteAsync(bytes, ct);
    }

    private static IEnumerable<string> GetLocalAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(n => n.Address)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && IsLocalAddress(ip))
            .Select(ip => ip.ToString()).Distinct().ToList();
        return addresses.Count == 0 ? new[] { "127.0.0.1" } : addresses;
    }

    private static bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        var b = address.MapToIPv4().GetAddressBytes();
        return b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 || b[0] == 169 && b[1] == 254;
    }

    public void Dispose() => Stop();

    public const string ControllerHtml = RemoteControllerPage.Html;
}
