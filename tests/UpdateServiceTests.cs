using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-updates-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly Version Current = new(1, 3, 0);
    private static readonly byte[] Installer = Enumerable.Range(0, 150_000).Select(i => (byte)(i % 251)).ToArray();
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Installer)).ToLowerInvariant();
    private static string InstallerName(string version = "1.4.0") => $"WorshipStudio-Setup-{version}.exe";
    private static string Manifest(string version = "1.4.0") => $"{Hash}  {InstallerName(version)}\n";
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Theory]
    [InlineData("v1.4.0", true)]
    [InlineData("1.4.0", true)]
    [InlineData("v2.0.12", true)]
    [InlineData("v1.4.0-beta.1", false)]
    [InlineData("v1.4", false)]
    [InlineData("v1.4.0.1", false)]
    [InlineData("v01.4.0", false)]
    [InlineData("v1.4.0/../../file", false)]
    [InlineData("v2147483648.0.0", false)]
    public void AcceptsOnlyStableSafeVersionTags(string tag, bool valid)
        => Assert.Equal(valid, UpdateService.TryParseStableVersion(tag, out _));

    [Fact]
    public async Task StableNewerReleaseReturnsExpectedAssetsAndNoInstallerIsDownloadedDuringCheck()
    {
        var requested = new List<string>();
        using var client = Client(request => { requested.Add(request.RequestUri!.AbsoluteUri); return JsonResponse(Metadata()); });
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        Assert.Equal(new Version(1, 4, 0), release.Version);
        Assert.Equal(InstallerName(), release.InstallerFileName);
        Assert.Equal(Installer.LongLength, release.InstallerSize);
        Assert.Equal("Letras para culto 1.4.0", release.Title);
        Assert.Equal("Novidades", release.Notes);
        Assert.Equal(new[] { UpdateService.LatestApiUrl }, requested);
        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData("v1.3.0", false, false)]
    [InlineData("v1.2.0", false, false)]
    [InlineData("v1.5.0", true, false)]
    [InlineData("v1.5.0", false, true)]
    [InlineData("v1.5.0-beta.1", false, false)]
    public async Task OlderCurrentDraftAndPrereleaseAreIgnored(string tag, bool draft, bool prerelease)
    {
        using var client = Client(_ => JsonResponse(Metadata(tag, draft: draft, prerelease: prerelease)));
        using var updater = new UpdateService(client, _directory);
        Assert.Null(await updater.CheckAsync(new Version(1, 3, 0, 0)));
    }

    [Fact]
    public async Task MissingLatestReleaseReturnsNoUpdate()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var updater = new UpdateService(client, _directory);
        Assert.Null(await updater.CheckAsync(Current));
    }

    [Fact]
    public async Task OfflineFailureIsReportedWithoutTouchingLocalFiles()
    {
        using var client = Client(_ => throw new HttpRequestException("Sem rede"));
        using var updater = new UpdateService(client, _directory);
        await Assert.ThrowsAsync<HttpRequestException>(() => updater.CheckAsync(Current));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ApiRateLimitProducesAnActionableFailure()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var updater = new UpdateService(client, _directory);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => updater.CheckAsync(Current));
        Assert.Contains("mais tarde", error.Message);
    }

    [Theory]
    [InlineData("https://evil.example/setup.exe")]
    [InlineData("http://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.4.0/WorshipStudio-Setup-1.4.0.exe")]
    [InlineData("https://github.com/other/repo/releases/download/v1.4.0/WorshipStudio-Setup-1.4.0.exe")]
    [InlineData("https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.3.0/WorshipStudio-Setup-1.4.0.exe")]
    public async Task RejectsInstallerUrlsOutsideTheExactOfficialVersion(string uri)
    {
        using var client = Client(_ => JsonResponse(Metadata(installerUrl: uri)));
        using var updater = new UpdateService(client, _directory);
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.CheckAsync(Current));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(UpdateService.MaximumInstallerBytes + 1)]
    public async Task RejectsMissingOrOversizedInstaller(long size)
    {
        using var client = Client(_ => JsonResponse(Metadata(installerSize: size)));
        using var updater = new UpdateService(client, _directory);
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.CheckAsync(Current));
    }

    [Fact]
    public async Task DownloadsVerifiesAndReturnsTheActualInstallerWithoutExecutingIt()
    {
        using var client = ReleaseClient();
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        var received = new List<UpdateDownloadProgress>();
        var path = await updater.DownloadAsync(release, new ImmediateProgress<UpdateDownloadProgress>(received.Add));
        Assert.Equal(Installer, await File.ReadAllBytesAsync(path));
        Assert.Equal(InstallerName(), Path.GetFileName(path));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.partial", SearchOption.AllDirectories));
        Assert.Equal(0, received[0].BytesReceived);
        Assert.Equal(1, received[^1].Fraction);
    }

    [Fact]
    public async Task ManifestCanVerifyAnOlderGitHubAssetWithoutApiDigests()
    {
        using var client = ReleaseClient(includeDigests: false);
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        Assert.Null(release.InstallerSha256);
        Assert.True(File.Exists(await updater.DownloadAsync(release)));
    }

    [Fact]
    public async Task CorruptedInstallerIsRejectedAndItsPartialFileRemoved()
    {
        var corrupt = Installer.ToArray(); corrupt[50] ^= 1;
        using var client = ReleaseClient(installerBody: corrupt);
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release));
        Assert.Contains("SHA-256", error.Message);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CorruptedManifestIsRejectedBeforeDownloadingInstaller()
    {
        var downloadedInstaller = false;
        using var client = Client(request =>
        {
            if (request.RequestUri!.AbsoluteUri == UpdateService.LatestApiUrl) return JsonResponse(Metadata());
            if (request.RequestUri.AbsoluteUri.EndsWith(".txt")) return BytesResponse(Encoding.UTF8.GetBytes(Manifest().Replace(Hash, new string('0', 64))));
            downloadedInstaller = true; return BytesResponse(Installer);
        });
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release));
        Assert.False(downloadedInstaller);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ManifestAndApiInstallerDigestsMustAgree()
    {
        using var client = ReleaseClient();
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release with { InstallerSha256 = new string('a', 64) }));
        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task ManifestNeedsOneUnambiguousInstallerHash(string mode)
    {
        var text = mode == "missing" ? Manifest().Replace(InstallerName(), "another.exe") : Manifest() + Manifest();
        using var client = ReleaseClient(manifest: text);
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task TruncatedInstallerIsRejectedAndRemoved()
    {
        using var client = ReleaseClient(installerBody: Installer[..^10]);
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CancelDuringDownloadRemovesPartialFileAndNeverReturnsExecutable()
    {
        using var client = ReleaseClient();
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        using var cancellation = new CancellationTokenSource();
        var progress = new ImmediateProgress<UpdateDownloadProgress>(p => { if (p.BytesReceived > 0) cancellation.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updater.DownloadAsync(release, progress, cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RejectsUntrustedRedirectBeforeSendingAnyRequestToIt()
    {
        var requested = new List<string>();
        using var client = Client(request =>
        {
            requested.Add(request.RequestUri!.AbsoluteUri);
            if (request.RequestUri.AbsoluteUri == UpdateService.LatestApiUrl) return JsonResponse(Metadata());
            var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri("https://evil.example/installer"); return response;
        });
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release));
        Assert.DoesNotContain(requested, x => x.Contains("evil.example"));
    }

    [Fact]
    public async Task FollowsGitHubReleaseAssetRedirectsOverHttps()
    {
        using var client = Client(request =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsoluteUri == UpdateService.LatestApiUrl) return JsonResponse(Metadata());
            if (uri.Host == "github.com")
            {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://release-assets.githubusercontent.com/assets/" + Path.GetFileName(uri.AbsolutePath) + "?signature=test");
                return response;
            }
            return BytesResponse(uri.AbsolutePath.EndsWith(".txt") ? Encoding.UTF8.GetBytes(Manifest()) : Installer);
        });
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        Assert.Equal(Installer, await File.ReadAllBytesAsync(await updater.DownloadAsync(release)));
    }

    [Fact]
    public async Task CallerCannotInjectTraversalFilenameIntoDownload()
    {
        using var client = ReleaseClient();
        using var updater = new UpdateService(client, _directory);
        var release = Assert.IsType<UpdateRelease>(await updater.CheckAsync(Current));
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(release with { InstallerFileName = "../bad.exe" }));
        Assert.False(Directory.Exists(_directory));
    }

    private static string Metadata(string tag = "v1.4.0", bool draft = false, bool prerelease = false, string? installerUrl = null,
        long? installerSize = null, string? manifest = null, bool includeDigests = true)
    {
        var version = tag.TrimStart('v');
        var installerName = InstallerName(version);
        var manifestName = $"SHA256SUMS-v{version}.txt";
        var manifestBytes = Encoding.UTF8.GetBytes(manifest ?? Manifest(version));
        return JsonSerializer.Serialize(new
        {
            tag_name = tag, draft, prerelease, name = $"Letras para culto {version}", body = "Novidades", published_at = "2026-11-22T12:00:00Z",
            assets = new[]
            {
                new { name = installerName, state = "uploaded", size = installerSize ?? Installer.LongLength,
                    browser_download_url = installerUrl ?? $"{UpdateService.RepositoryUrl}/releases/download/{tag}/{installerName}", digest = includeDigests ? "sha256:" + Hash : null },
                new { name = manifestName, state = "uploaded", size = manifestBytes.LongLength,
                    browser_download_url = $"{UpdateService.RepositoryUrl}/releases/download/{tag}/{manifestName}",
                    digest = includeDigests ? "sha256:" + Convert.ToHexString(SHA256.HashData(manifestBytes)) : null }
            }
        });
    }

    private static HttpClient ReleaseClient(byte[]? installerBody = null, string? manifest = null, bool includeDigests = true)
        => Client(request => request.RequestUri!.AbsoluteUri == UpdateService.LatestApiUrl
            ? JsonResponse(Metadata(manifest: manifest, includeDigests: includeDigests))
            : BytesResponse(request.RequestUri.AbsoluteUri.EndsWith(".txt") ? Encoding.UTF8.GetBytes(manifest ?? Manifest()) : installerBody ?? Installer));
    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage BytesResponse(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response));
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = response(request); result.RequestMessage ??= request; return Task.FromResult(result);
        }
    }
    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
