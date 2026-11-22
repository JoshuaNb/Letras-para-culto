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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string Token { get; private set; } = "";
    public int Port { get; private set; }
    public bool IsRunning => _listener != null;
    public IReadOnlyList<string> Urls => GetLocalAddresses().Select(ip => $"http://{ip}:{Port}/?token={Token}").ToArray();
    public event Action<string>? Error;

    public void Start(int port, Func<object> getState, Action<string, int?> command)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Use uma porta entre 1024 e 65535.");
        Stop();
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _commandTimes.Clear();
        _getState = getState;
        _command = command;
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start(8);
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
        Token = "";
    }

    private async Task AcceptLoop(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                if (!await _clients.WaitAsync(0, cancellationToken)) { client.Dispose(); continue; }
                _ = Serve(client, cancellationToken);
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
                foreach (var line in lines.Skip(1))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }
                byte[] body;
                try { body = await ReadBody(stream, headers, ct); }
                catch (InvalidDataException) { await Reply(stream, 400, "application/json", "{\"error\":\"Corpo inválido ou muito grande\"}", ct); return; }
                var path = first[1].Split('?')[0];
                if (first[0] == "GET" && path == "/")
                {
                    await Reply(stream, 200, "text/html; charset=utf-8", ControllerHtml, ct); return;
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
            finally { _clients.Release(); }
        }
    }

    private bool ValidToken(string authorization)
    {
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.Ordinal) || Token.Length == 0) return false;
        var incoming = Encoding.UTF8.GetBytes(authorization[prefix.Length..]);
        var expected = Encoding.UTF8.GetBytes(Token);
        return incoming.Length == expected.Length && CryptographicOperations.FixedTimeEquals(incoming, expected);
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
                await stream.ReadExactlyAsync(bytes, ct);
                await body.WriteAsync(bytes, ct);
                if ((await ReadLine(stream, 2, ct)).Length != 0) throw new InvalidDataException();
            }
            throw new InvalidDataException();
        }
        if (!headers.TryGetValue("Content-Length", out var lengthText)) return [];
        if (!int.TryParse(lengthText, out var length) || length < 0 || length > 2048) throw new InvalidDataException();
        var content = new byte[length];
        await stream.ReadExactlyAsync(content, ct);
        return content;
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

    private const string ControllerHtml = """
<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><title>Worship Studio · Controle</title>
<style>*{box-sizing:border-box}body{margin:0;background:#0b101b;color:#f4f6ff;font:16px system-ui,sans-serif;padding:24px 18px;max-width:680px;margin:auto}header{display:flex;align-items:center;gap:12px}header b{font-size:20px}.mark{background:#818cf8;width:38px;height:38px;border-radius:12px;display:grid;place-items:center;color:#101224;font-size:24px}small{color:#93a1b8}h1{font-size:24px;margin:30px 0 5px}#preview{white-space:pre-wrap;text-align:center;display:grid;place-content:center;min-height:200px;background:radial-gradient(at top,#293067,#101628);border:1px solid #2b3652;border-radius:18px;padding:24px;font-size:24px;font-weight:650;line-height:1.45;margin:18px 0}button{background:#1c263b;color:#eff2ff;border:1px solid #34415d;border-radius:13px;padding:18px 12px;font:600 17px system-ui;cursor:pointer;touch-action:manipulation;min-height:64px}button:active{transform:scale(.98)}button[disabled]{opacity:.45}.row{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin:12px 0}.primary{background:#827cf5;color:#0c1020;border-color:#827cf5}.active{border-color:#f1b76b;background:#523c2d}.slide{width:100%;text-align:left;margin:5px 0;padding:13px;font-size:15px;white-space:pre-line;min-height:50px}.slide.active{background:#343063;border-color:#9a94ff}#status{display:block;margin-top:16px;min-height:36px}.hint{font-size:13px;line-height:1.6}#slides{margin-top:24px}#disconnect{margin-top:24px;font-size:13px;min-height:40px;padding:10px}</style>
<header><span class="mark">W</span><div><b>Worship Studio</b><br><small>CONTROLE LOCAL</small></div></header><h1 id="title">Conectando…</h1><small id="position">Rede local · sem internet</small><div id="preview">Aguardando apresentação</div><div class="row"><button data-command="previous">← Anterior</button><button class="primary" data-command="next">Próximo →</button></div><div class="row"><button id="clear" data-command="clear">Ocultar letra</button><button id="blackout" data-command="blackout">Tela preta</button></div><button style="width:100%" data-command="tap">Pulso de efeito ♫</button><small id="status">Conectando ao computador…</small><p class="hint">Use a mesma rede Wi-Fi do computador. A apresentação continua se este celular desconectar.</p><div id="slides"></div><button id="disconnect">Esquecer acesso neste celular</button>
<script>const q=new URLSearchParams(location.search);if(q.has('token')){sessionStorage.setItem('ws-token',q.get('token'));history.replaceState(null,'',location.pathname)}const token=sessionStorage.getItem('ws-token')||'';const $=id=>document.getElementById(id);let busy=false,lastSlides='';async function request(path,options={}){const r=await fetch(path,{...options,headers:{Authorization:'Bearer '+token,...options.headers},cache:'no-store'});if(!r.ok)throw new Error(r.status===401?'Acesso expirado. Abra novamente o endereço mostrado no computador.':'Sem resposta do computador');return r.json()}async function command(command,index){if(busy)return;busy=true;try{await request('/command',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({command,index})});await refresh()}catch(e){$('status').textContent=e.message}finally{busy=false}}async function refresh(){try{const s=await request('/state');$('title').textContent=s.title||'Sem apresentação';$('position').textContent=s.count?'Slide '+(s.index+1)+' de '+s.count:'Selecione um louvor no computador';$('preview').textContent=s.blackout?'Tela preta':s.hideLyrics?'Letra oculta':s.text||'Sem letra ao vivo';$('clear').classList.toggle('active',!!s.hideLyrics);$('blackout').classList.toggle('active',!!s.blackout);$('status').textContent='● Conectado · '+new Date().toLocaleTimeString('pt-BR');const k=JSON.stringify([s.slides,s.index]);if(k!==lastSlides){lastSlides=k;$('slides').replaceChildren();(s.slides||[]).forEach((slide,i)=>{const b=document.createElement('button');b.className='slide'+(i===s.index?' active':'');b.textContent=(i+1)+'. '+(slide.label||'Slide')+'\n'+(slide.text||'');b.onclick=()=>command('slide',i);$('slides').append(b)})}}catch(e){$('status').textContent=e.message}}document.querySelectorAll('[data-command]').forEach(b=>b.onclick=()=>command(b.dataset.command));$('disconnect').onclick=()=>{sessionStorage.removeItem('ws-token');location.reload()};refresh();setInterval(()=>{if(!busy&&!document.hidden)refresh()},900);document.addEventListener('visibilitychange',()=>{if(!document.hidden)refresh()});</script></html>
""";
}
