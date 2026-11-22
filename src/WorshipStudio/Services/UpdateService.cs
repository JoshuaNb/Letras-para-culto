using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorshipStudio.Services;

public sealed record UpdateRelease(
    Version Version, string Tag, string Title, string Notes, DateTimeOffset? PublishedAt,
    Uri PageUri, Uri InstallerUri, string InstallerFileName, long InstallerSize,
    string? InstallerSha256, Uri ManifestUri, string ManifestFileName, long ManifestSize,
    string? ManifestSha256);

public sealed record UpdateDownloadProgress(long BytesReceived, long TotalBytes)
{
    public double Fraction => TotalBytes <= 0 ? 0 : Math.Clamp((double)BytesReceived / TotalBytes, 0, 1);
}

/// <summary>Consulta e baixa somente releases estáveis do repositório público. Nunca executa arquivos.</summary>
public sealed class UpdateService : IDisposable
{
    public const string RepositoryUrl = "https://github.com/JoshuaNb/Letras-para-culto";
    public const string LatestApiUrl = "https://api.github.com/repos/JoshuaNb/Letras-para-culto/releases/latest";
    public const long MaximumInstallerBytes = 1024L * 1024 * 1024;
    private const long MaximumManifestBytes = 64 * 1024;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _downloadDirectory;

    public static Version CurrentVersion
    {
        get
        {
            var assembly = typeof(UpdateService).Assembly;
            var text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
            if (Version.TryParse(text, out var version)) return NormalizeVersion(version);
            return NormalizeVersion(assembly.GetName().Version ?? new Version(1, 0, 0));
        }
    }

    public UpdateService(HttpClient? client = null, string? downloadDirectory = null)
    {
        _ownsClient = client == null;
        // Redirecionamentos são seguidos explicitamente: cada destino precisa ser um host de releases do GitHub.
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        _downloadDirectory = downloadDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorshipStudio", "Updates");
    }

    public async Task<UpdateRelease?> CheckAsync(Version? currentVersion = null, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = CreateRequest(new Uri(LatestApiUrl), true);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        ValidateApiResponseUri(response.RequestMessage?.RequestUri);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        EnsureSuccess(response);
        var json = await ReadBoundedAsync(response, 2 * 1024 * 1024, timeout.Token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = RequiredString(release, "tag_name");
        if (!TryParseStableVersion(tag, out var version)) return null;
        if (version <= NormalizeVersion(currentVersion ?? CurrentVersion)) return null;
        var installerName = $"WorshipStudio-Setup-{version}.exe";
        var manifestName = $"SHA256SUMS-v{version}.txt";
        var installer = FindAsset(release, installerName, MaximumInstallerBytes);
        var manifest = FindAsset(release, manifestName, MaximumManifestBytes);
        var pageUri = new Uri($"{RepositoryUrl}/releases/tag/{tag}");
        DateTimeOffset? published = null;
        if (release.TryGetProperty("published_at", out var date) && DateTimeOffset.TryParse(date.GetString(), out var parsed)) published = parsed;
        return new UpdateRelease(version, tag,
            OptionalString(release, "name") ?? $"Letras para culto {version}", OptionalString(release, "body") ?? "", published,
            pageUri, AssetUri(installer, tag, installerName), installerName, installer.GetProperty("size").GetInt64(), ReadDigest(installer),
            AssetUri(manifest, tag, manifestName), manifestName, manifest.GetProperty("size").GetInt64(), ReadDigest(manifest));
    }

    public async Task<string> DownloadAsync(UpdateRelease release, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateRelease(release);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        var token = timeout.Token;
        using var manifestResponse = await GetAssetAsync(release.ManifestUri, token).ConfigureAwait(false);
        ValidateContentLength(manifestResponse, release.ManifestSize);
        var manifest = await ReadBoundedAsync(manifestResponse, MaximumManifestBytes, token).ConfigureAwait(false);
        if (manifest.LongLength != release.ManifestSize) throw new InvalidDataException("O arquivo de hashes foi recebido incompleto.");
        CheckDigest(manifest, release.ManifestSha256, "O arquivo de hashes não confere com o GitHub.");
        var expectedHash = ParseManifestHash(Encoding.UTF8.GetString(manifest), release.InstallerFileName);
        if (release.InstallerSha256 != null && !expectedHash.Equals(release.InstallerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O hash do instalador diverge entre o manifesto e o GitHub.");

        // Cada tentativa usa uma pasta nova, evitando reutilizar ou sobrescrever um download antigo.
        var folder = Path.Combine(_downloadDirectory, $"v{release.Version}-{Guid.NewGuid():N}");
        var partial = Path.Combine(folder, release.InstallerFileName + ".partial");
        var complete = Path.Combine(folder, release.InstallerFileName);
        Directory.CreateDirectory(folder);
        try
        {
            using var response = await GetAssetAsync(release.InstallerUri, token).ConfigureAwait(false);
            ValidateContentLength(response, release.InstallerSize);
            await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;
            progress?.Report(new UpdateDownloadProgress(0, release.InstallerSize));
            await using (var destination = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true))
            {
                var buffer = new byte[128 * 1024];
                int count;
                while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    received += count;
                    if (received > release.InstallerSize || received > MaximumInstallerBytes)
                        throw new InvalidDataException("O instalador ultrapassou o tamanho informado pelo GitHub.");
                    hasher.AppendData(buffer, 0, count);
                    await destination.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    progress?.Report(new UpdateDownloadProgress(received, release.InstallerSize));
                }
                await destination.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            if (received != release.InstallerSize) throw new InvalidDataException("O instalador foi recebido incompleto. Tente baixar novamente.");
            var actualHash = Convert.ToHexString(hasher.GetHashAndReset());
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A verificação SHA-256 falhou. O instalador não será executado.");
            File.Move(partial, complete);
            return complete;
        }
        catch
        {
            // Só remove a pasta criada nesta tentativa, nunca instalações ou dados da biblioteca.
            try { if (File.Exists(partial)) File.Delete(partial); if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    public static bool TryParseStableVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (tag == null || !Regex.IsMatch(tag, @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)) return false;
        return Version.TryParse(tag.TrimStart('v'), out version!);
    }

    private static Version NormalizeVersion(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));

    private static JsonElement FindAsset(JsonElement release, string name, long maximumSize)
    {
        var matches = release.GetProperty("assets").EnumerateArray().Where(a => OptionalString(a, "name") == name).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"A atualização não contém um único arquivo {name}.");
        var asset = matches[0];
        var size = asset.GetProperty("size").GetInt64();
        if (OptionalString(asset, "state") != "uploaded" || size <= 0 || size > maximumSize)
            throw new InvalidDataException($"O arquivo {name} ainda não está disponível ou excede o limite permitido.");
        return asset;
    }

    private static Uri AssetUri(JsonElement asset, string tag, string name)
    {
        var expected = $"{RepositoryUrl}/releases/download/{tag}/{name}";
        if (!Uri.TryCreate(RequiredString(asset, "browser_download_url"), UriKind.Absolute, out var uri) || uri.AbsoluteUri != expected)
            throw new InvalidDataException("O endereço de download não pertence à versão oficial do projeto.");
        return uri;
    }

    private static string? ReadDigest(JsonElement asset)
    {
        var digest = OptionalString(asset, "digest");
        if (string.IsNullOrEmpty(digest)) return null;
        if (!Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("O GitHub informou um hash de arquivo inválido.");
        return digest[7..];
    }

    private static string ParseManifestHash(string manifest, string name)
    {
        var matches = manifest.TrimStart('\uFEFF').Split('\n')
            .Select(line => Regex.Match(line.TrimEnd('\r'), @"^([a-fA-F0-9]{64})[ \t]+\*?(.+)$", RegexOptions.CultureInvariant))
            .Where(m => m.Success && m.Groups[2].Value == name).Select(m => m.Groups[1].Value).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("O manifesto não contém um único hash SHA-256 para o instalador.");
        return matches[0];
    }

    private static void ValidateRelease(UpdateRelease release)
    {
        if (!TryParseStableVersion(release.Tag, out var version) || version != release.Version ||
            release.InstallerFileName != $"WorshipStudio-Setup-{version}.exe" || release.ManifestFileName != $"SHA256SUMS-v{version}.txt" ||
            release.InstallerUri.AbsoluteUri != $"{RepositoryUrl}/releases/download/{release.Tag}/{release.InstallerFileName}" ||
            release.ManifestUri.AbsoluteUri != $"{RepositoryUrl}/releases/download/{release.Tag}/{release.ManifestFileName}" ||
            release.InstallerSize <= 0 || release.InstallerSize > MaximumInstallerBytes || release.ManifestSize <= 0 || release.ManifestSize > MaximumManifestBytes)
            throw new InvalidDataException("A atualização não corresponde ao pacote oficial esperado.");
        foreach (var hash in new[] { release.InstallerSha256, release.ManifestSha256 })
            if (hash != null && !Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidDataException("A atualização contém um hash inválido.");
    }

    private async Task<HttpResponseMessage> GetAssetAsync(Uri uri, CancellationToken token)
    {
        var current = uri;
        for (var redirect = 0; redirect <= 5; redirect++)
        {
            ValidateDownloadHost(current);
            using var request = CreateRequest(current, false);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            try
            {
                ValidateDownloadHost(response.RequestMessage?.RequestUri ?? current);
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    if (redirect == 5 || response.Headers.Location == null) throw new InvalidDataException("Redirecionamento de download inválido.");
                    current = new Uri(current, response.Headers.Location);
                    ValidateDownloadHost(current);
                    response.Dispose();
                    continue;
                }
                EnsureSuccess(response);
                return response;
            }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Não foi possível resolver o download do GitHub.");
    }

    private static void ValidateDownloadHost(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
            throw new InvalidDataException("O download foi redirecionado para um endereço não autorizado.");
    }

    private static void ValidateApiResponseUri(Uri? uri)
    {
        if (uri != null && uri.AbsoluteUri != LatestApiUrl) throw new InvalidDataException("A consulta de atualizações foi redirecionada para um endereço não autorizado.");
    }

    private static HttpRequestMessage CreateRequest(Uri uri, bool api)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd($"WorshipStudio/{CurrentVersion}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(api ? "application/vnd.github+json" : "application/octet-stream"));
        if (api) request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("O GitHub limitou as consultas. Tente novamente mais tarde.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"O GitHub não conseguiu fornecer a atualização (HTTP {(int)response.StatusCode}).", null, response.StatusCode);
    }

    private static void ValidateContentLength(HttpResponseMessage response, long expected)
    {
        if (response.Content.Headers.ContentLength is long length && length != expected)
            throw new InvalidDataException("O tamanho do arquivo diverge do publicado no GitHub.");
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, long maximum, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("A resposta do GitHub excedeu o limite permitido.");
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            if (destination.Length + read > maximum) throw new InvalidDataException("A resposta do GitHub excedeu o limite permitido.");
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }

    private static void CheckDigest(byte[] bytes, string? expected, string error)
    {
        if (expected != null && !Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(error);
    }

    private static string RequiredString(JsonElement element, string name) => OptionalString(element, name) ?? throw new InvalidDataException($"O GitHub não informou {name}.");
    private static string? OptionalString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
