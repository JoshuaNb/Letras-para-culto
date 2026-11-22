using System.Net.Http;
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

/// <summary>Read-only integration with the official local Holyrics API Server.</summary>
public sealed class HolyricsService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };
    public event Action<string>? Error;

    public async Task<List<Song>> ImportLibraryAsync(string baseUrl, string token, CancellationToken ct = default)
    {
        var data = await Request(baseUrl, token, "GetSongs", new { fields = "id,title,artist,author,slides,order" }, ct);
        if (data.ValueKind != JsonValueKind.Array) throw new InvalidDataException("O Holyrics não retornou uma lista de músicas.");
        var songs = new List<Song>();
        foreach (var item in data.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            var songData = item;
            if ((!item.TryGetProperty("slides", out var slides) || slides.ValueKind != JsonValueKind.Array || slides.GetArrayLength() == 0) &&
                item.TryGetProperty("id", out var id))
                songData = await Request(baseUrl, token, "GetLyrics", new { id = id.GetString(), fields = "id,title,artist,author,slides,order" }, ct);
            var song = ParseJsonSong(songData);
            if (song != null) songs.Add(song);
        }
        return songs;
    }

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
        using var content = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var envelope = doc.RootElement;
        if (String(envelope, "status") != "ok")
        {
            var message = envelope.TryGetProperty("error", out var error) ? error.ToString() : "Resposta desconhecida";
            throw new InvalidOperationException("Holyrics: " + message);
        }
        return envelope.TryGetProperty("data", out var result) ? result.Clone() : default;
    }

    public void Dispose() => _http.Dispose();
}
