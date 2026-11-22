using System.Net.Http;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using WorshipStudio.Models;

namespace WorshipStudio.Services;

public sealed record HolyricsPresentation(string Title, string Text, int Index, int Count, bool Blackout, bool HideLyrics)
{
    public IReadOnlyList<Slide> Slides { get; init; } = Array.Empty<Slide>();
}

public sealed record HolyricsCatalogSong(string Id, string Title, string Artist)
{
    internal Song? EmbeddedSong { get; init; }
}

public sealed record HolyricsDownloadProgress(int Completed, int Total, string Title)
{
    public double Fraction => Total == 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}

/// <summary>Read-only integration with the official local Holyrics API Server.</summary>
public sealed class HolyricsService : IDisposable
{
    private const int MaximumSongs = 50000;
    private const int MaximumResponseBytes = 32 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    public event Action<string>? Error;

    public HolyricsService(HttpClient? http = null)
    {
        _ownsClient = http == null;
        _http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
    }

    public async Task<List<Song>> ImportLibraryAsync(string baseUrl, string token, CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(baseUrl, token, ct).ConfigureAwait(false);
        return await DownloadSongsAsync(baseUrl, token, catalog, null, ct).ConfigureAwait(false);
    }

    /// <summary>Lists metadata from the connected Holyrics library; no public cloud catalog is queried.</summary>
    public async Task<IReadOnlyList<HolyricsCatalogSong>> GetCatalogAsync(string baseUrl, string token, CancellationToken ct = default)
    {
        var data = await Request(baseUrl, token, "GetSongs", new { fields = "id,title,artist,author" }, ct).ConfigureAwait(false);
        if (data.ValueKind != JsonValueKind.Array) throw new InvalidDataException("O Holyrics não retornou uma lista de músicas.");
        if (data.GetArrayLength() > MaximumSongs) throw new InvalidDataException("A biblioteca excede o limite de 50.000 músicas por importação.");
        var catalog = new List<HolyricsCatalogSong>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        foreach (var item in data.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            position++;
            if (item.ValueKind != JsonValueKind.Object) continue;
            var cached = ParseJsonSong(item);
            var id = Identifier(item);
            if (id.Length == 0 && cached != null) id = "embedded-" + position.ToString(CultureInfo.InvariantCulture);
            if (!ValidIdentifier(id) || !ids.Add(id)) continue;
            var title = String(item, "title").Trim();
            if (title.Length == 0) title = cached?.Title ?? "Sem título";
            var artist = String(item, "artist").Trim();
            if (artist.Length == 0) artist = String(item, "author").Trim();
            if (title.Length > 4096 || artist.Length > 4096) throw new InvalidDataException("O catálogo contém nomes de músicas inválidos.");
            catalog.Add(new HolyricsCatalogSong(id, title, artist) { EmbeddedSong = cached });
        }
        return catalog;
    }

    public static IReadOnlyList<HolyricsCatalogSong> FilterCatalog(IEnumerable<HolyricsCatalogSong> catalog, string? query)
    {
        var text = query?.Trim() ?? "";
        if (text.Length == 0) return catalog.ToArray();
        var comparison = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return catalog.Where(song => comparison.IndexOf(song.Title, text, options) >= 0 || comparison.IndexOf(song.Artist, text, options) >= 0).ToArray();
    }

    /// <summary>Returns a complete selected batch. Cancellation/failure never writes the local library.</summary>
    public async Task<List<Song>> DownloadSongsAsync(string baseUrl, string token, IEnumerable<HolyricsCatalogSong> selection,
        IProgress<HolyricsDownloadProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var selected = selection.Take(MaximumSongs + 1).ToArray();
        if (selected.Length > MaximumSongs) throw new InvalidDataException("Selecione no máximo 50.000 músicas.");
        if (selected.Any(song => song == null || !ValidIdentifier(song.Id))) throw new InvalidDataException("Uma música selecionada não possui identificador válido.");
        selected = selected.DistinctBy(song => song.Id, StringComparer.Ordinal).ToArray();
        var songs = new List<Song>();
        progress?.Report(new HolyricsDownloadProgress(0, selected.Length, ""));
        foreach (var item in selected)
        {
            ct.ThrowIfCancellationRequested();
            var song = item.EmbeddedSong;
            if (song == null)
            {
                var details = await Request(baseUrl, token, "GetLyrics", new { id = item.Id, fields = "id,title,artist,author,slides,order" }, ct).ConfigureAwait(false);
                song = ParseJsonSong(details);
            }
            if (song == null) throw new InvalidDataException($"A música “{item.Title}” não está disponível com letra. Atualize o catálogo e confira as permissões GetLyrics no Holyrics.");
            if (song.Lyrics.Length > 2_000_000) throw new InvalidDataException("Uma letra excedeu o limite de tamanho permitido.");
            songs.Add(song with { Id = Guid.NewGuid(), Title = song.Title == "Sem título" ? item.Title : song.Title,
                Artist = string.IsNullOrWhiteSpace(song.Artist) ? item.Artist : song.Artist });
            progress?.Report(new HolyricsDownloadProgress(songs.Count, selected.Length, item.Title));
        }
        ct.ThrowIfCancellationRequested();
        return songs;
    }

    private static string Identifier(JsonElement item) => item.TryGetProperty("id", out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "" : "";
    private static bool ValidIdentifier(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 512 && !id.Any(char.IsControl);

    public async Task FollowAsync(string baseUrl, string token, Action<HolyricsPresentation> onChange, CancellationToken ct = default)
    {
        HolyricsPresentation? previous = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await Request(baseUrl, token, "GetCurrentPresentation", new { include_slides = true, include_slide_comment = false }, ct);
                var current = ParsePresentation(data);
                if (previous == null || !SamePresentation(current, previous)) { onChange(current); previous = current; }
                await Task.Delay(300, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Error?.Invoke("Sincronização com Holyrics interrompida: " + ex.Message);
            throw;
        }
    }

    private static bool SamePresentation(HolyricsPresentation current, HolyricsPresentation previous) =>
        current.Title == previous.Title && current.Text == previous.Text && current.Index == previous.Index && current.Count == previous.Count &&
        current.Blackout == previous.Blackout && current.HideLyrics == previous.HideLyrics && current.Slides.SequenceEqual(previous.Slides);

    public static HolyricsPresentation ParsePresentation(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return new("Sem apresentação", "", 0, 0, false, true);
        var title = String(data, "name");
        var type = String(data, "slide_type");
        var number = data.TryGetProperty("slide_number", out var numberValue) && numberValue.ValueKind == JsonValueKind.Number && numberValue.TryGetInt32(out var n) ? n : 1;
        var total = data.TryGetProperty("total_slides", out var totalValue) && totalValue.ValueKind == JsonValueKind.Number && totalValue.TryGetInt32(out var count) ? count : 0;
        string text = "";
        var index = Math.Max(0, number - 1);
        IReadOnlyList<Slide> sourceSlides = Array.Empty<Slide>();
        if (data.TryGetProperty("slides", out var slides) && slides.ValueKind == JsonValueKind.Array)
        {
            var array = slides.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object).Select((slide, position) => new
            {
                Number = slide.TryGetProperty("number", out var num) && num.ValueKind == JsonValueKind.Number && num.TryGetInt32(out var sn) ? sn : position + 1,
                Slide = new Slide(String(slide, "slide_description") is { Length: > 0 } label ? label : "Estrofe", String(slide, "text"))
            }).OrderBy(slide => slide.Number).ToArray();
            sourceSlides = array.Select(slide => slide.Slide).ToArray();
            total = array.Length;
            if (array.Length > 0)
            {
                var exact = Array.FindIndex(array, slide => slide.Number == number);
                index = exact >= 0 ? exact : Math.Clamp(number - 1, 0, array.Length - 1);
                text = sourceSlides[index].Text;
            }
            else index = 0;
        }
        return new(title, text, index, total, type == "black", type is "blank" or "wallpaper" or "final_slide" || text.Length == 0) { Slides = sourceSlides };
    }

    public List<Song> ImportFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 64 * 1024 * 1024) throw new InvalidDataException("O arquivo excede 64 MB.");
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".txt")
        {
            var text = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (text.Length == 0) throw new InvalidDataException("O arquivo de letra está vazio.");
            return [new Song { Id = Guid.NewGuid(), Title = Path.GetFileNameWithoutExtension(path), Artist = "", Lyrics = text }];
        }
        if (extension == ".json")
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8), new JsonDocumentOptions { MaxDepth = 64 });
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)) root = data;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("songs", out var nested)) root = nested;
            var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToArray() : [root];
            var songs = items.Select(ParseJsonSong).OfType<Song>().ToList();
            if (songs.Count == 0) throw new InvalidDataException("Nenhuma música com letra foi encontrada no JSON.");
            return songs;
        }
        if (extension == ".xml") return ImportXml(path);
        throw new InvalidDataException("Use TXT UTF-8, JSON de músicas ou XML OpenLyrics. Backups proprietários do Holyrics não são lidos; importe pela API local.");
    }

    private static List<Song> ImportXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
        var document = XDocument.Load(reader);
        var nodes = document.Root?.Name.LocalName == "song" ? new[] { document.Root } : document.Descendants().Where(x => x.Name.LocalName == "song").ToArray();
        var songs = new List<Song>();
        foreach (var node in nodes)
        {
            var title = node.Descendants().FirstOrDefault(x => x.Name.LocalName == "title")?.Value;
            var author = string.Join(", ", node.Descendants().Where(x => x.Name.LocalName == "author").Select(x => x.Value));
            var verses = node.Descendants().Where(x => x.Name.LocalName == "verse").Select(verse =>
            {
                var lines = verse.Elements().Where(x => x.Name.LocalName == "lines").Select(PlainLines);
                return (Name: (string?)verse.Attribute("name") ?? "Parte", Text: string.Join("\n", lines));
            }).Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToList();
            var order = node.Descendants().FirstOrDefault(x => x.Name.LocalName == "verseOrder")?.Value;
            if (!string.IsNullOrWhiteSpace(order))
            {
                var ordered = order.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(name => verses.FirstOrDefault(v => v.Name == name)).Where(v => v.Text != null).ToList();
                if (ordered.Count != 0) verses = ordered;
            }
            var lyrics = string.Join("\n\n", verses.Select(v => $"[{v.Name}]\n{v.Text}"));
            if (lyrics.Length > 0) songs.Add(new Song { Id = Guid.NewGuid(), Title = title ?? Path.GetFileNameWithoutExtension(path), Artist = author, Lyrics = lyrics });
        }
        if (songs.Count == 0) throw new InvalidDataException("Nenhuma letra OpenLyrics foi encontrada no XML.");
        return songs;
    }

    private static string PlainLines(XElement element) => string.Concat(element.Nodes().Select(node => node switch
    {
        XText text => text.Value,
        XElement e when e.Name.LocalName == "br" => "\n",
        XElement e => PlainLines(e),
        _ => ""
    }));

    private static Song? ParseJsonSong(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        var title = String(item, "title");
        if (title.Length == 0) title = String(item, "Title");
        var artist = String(item, "artist");
        if (artist.Length == 0) artist = String(item, "author");
        if (artist.Length == 0) artist = String(item, "Artist");
        var lyrics = String(item, "lyrics");
        if (lyrics.Length == 0) lyrics = String(item, "Lyrics");
        if (lyrics.Length == 0 && item.TryGetProperty("slides", out var slides) && slides.ValueKind == JsonValueKind.Array)
        {
            var sections = slides.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object).ToArray();
            IEnumerable<JsonElement> ordered = sections;
            var order = String(item, "order");
            if (order.Length > 0)
            {
                var indexed = order.Split(',').Select(x => int.TryParse(x.Trim(), out var i) ? i - 1 : -1).Where(i => i >= 0 && i < sections.Length).Select(i => sections[i]).ToArray();
                if (indexed.Length > 0) ordered = indexed;
            }
            lyrics = string.Join("\n\n", ordered.Select(s =>
            {
                var label = String(s, "slide_description").Replace("[", "").Replace("]", "").Replace("\r", " ").Replace("\n", " ");
                var text = String(s, "text");
                return label.Length > 0 ? $"[{label}]\n{text}" : text;
            }).Where(t => !string.IsNullOrWhiteSpace(t)));
        }
        if (string.IsNullOrWhiteSpace(lyrics)) return null;
        return new Song { Id = Guid.NewGuid(), Title = string.IsNullOrWhiteSpace(title) ? "Sem título" : title, Artist = artist, Lyrics = lyrics };
    }

    private static string String(JsonElement item, string name) => item.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    private async Task<JsonElement> Request(string baseUrl, string token, string action, object parameters, CancellationToken ct)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
            throw new ArgumentException("Informe o endereço do API Server, por exemplo http://127.0.0.1:8091.");
        if (!string.IsNullOrEmpty(root.UserInfo) || root.Query.Length > 0 || root.Fragment.Length > 0) throw new ArgumentException("O endereço deve conter apenas protocolo, computador e porta.");
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Informe o token criado nas permissões do API Server do Holyrics.");
        var url = new Uri(root, "api/" + action + "?token=" + Uri.EscapeDataString(token));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json") };
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri is { } finalUri && finalUri != url)
                throw new InvalidDataException("O Holyrics redirecionou a conexão. Confira o endereço do API Server.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("O Holyrics não autorizou a leitura. Confira o token e as permissões GetSongs e GetLyrics.");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"O API Server do Holyrics respondeu HTTP {(int)response.StatusCode}. Confira o endereço e a porta.", null, response.StatusCode);
            if (response.Content.Headers.ContentLength > MaximumResponseBytes) throw new InvalidDataException("A resposta do Holyrics excedeu 32 MB.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var bytes = new byte[32 * 1024];
            int read;
            while ((read = await stream.ReadAsync(bytes, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaximumResponseBytes) throw new InvalidDataException("A resposta do Holyrics excedeu 32 MB.");
                buffer.Write(bytes, 0, read);
            }
            using var doc = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
            var envelope = doc.RootElement;
            if (envelope.ValueKind != JsonValueKind.Object) throw new InvalidDataException("O API Server retornou uma resposta inválida.");
            if (String(envelope, "status") != "ok")
                throw new InvalidOperationException("O Holyrics não liberou esta consulta. Confira o token, as permissões GetSongs/GetLyrics e se o API Server está ativado.");
            return envelope.TryGetProperty("data", out var result) ? result.Clone() : default;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (OperationCanceledException) { throw new TimeoutException("O Holyrics demorou para responder. Confira se está aberto, com o API Server ativo e acessível na rede."); }
        catch (HttpRequestException ex) { throw new HttpRequestException(ex.StatusCode.HasValue ? $"Não foi possível consultar o Holyrics (HTTP {(int)ex.StatusCode.Value})." : "Não foi possível conectar ao Holyrics. Confira o endereço, a porta e se o programa está aberto."); }
        catch (IOException) { throw new IOException("A conexão com o Holyrics foi interrompida. Tente buscar a biblioteca novamente."); }
        catch (JsonException) { throw new InvalidDataException("O API Server do Holyrics retornou JSON inválido."); }
    }

    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}
