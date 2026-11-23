using System.Net.Http;
using WorshipStudio.Models;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class StartupCoordinatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-startup-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public async Task LibraryLoadsWhileAnUnresponsiveUpdateCheckIsStillPending()
    {
        var store = new LibraryStore(_directory);
        var document = LibraryStore.CreateInitial();
        document.Songs[0].Title = "Biblioteca existente";
        store.Save(document);
        var slowNetwork = new TaskCompletionSource<UpdateRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new StartupCoordinator(_directory, checkUpdates: _ => slowNetwork.Task, updateTimeout: TimeSpan.FromSeconds(30));
        using var session = coordinator.Start(true);
        var loaded = await session.Library.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Null(loaded.Error);
        Assert.NotNull(loaded.Store);
        Assert.Equal("Biblioteca existente", loaded.Document.Songs[0].Title);
        Assert.False(session.Update.IsCompleted);
        session.SkipUpdateCheck();
        Assert.Equal(StartupUpdateState.Cancelled, (await session.Update.WaitAsync(TimeSpan.FromSeconds(3))).State);
        slowNetwork.TrySetResult(null);
    }

    [Fact]
    public async Task OptOutDoesNotCreateAnyUpdateRequestAndStillLoadsTheLibrary()
    {
        var requests = 0;
        var coordinator = new StartupCoordinator(_directory, checkUpdates: _ =>
        {
            Interlocked.Increment(ref requests);
            throw new InvalidOperationException("Should not contact the network");
        });
        using var session = coordinator.Start(false);
        Assert.Equal(StartupUpdateState.Disabled, (await session.Update).State);
        Assert.NotEmpty((await session.Library).Document.Songs);
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task OfflineFailureProducesAResultWithoutDiscardingTheLocalLibrary()
    {
        var coordinator = new StartupCoordinator(_directory, checkUpdates: _ => throw new HttpRequestException("Offline"));
        using var session = coordinator.Start(true);
        var result = await session.Update.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(StartupUpdateState.Unavailable, result.State);
        Assert.Null(result.Release);
        Assert.Contains("offline", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await session.Library).Error);
    }

    [Fact]
    public async Task TimeoutReturnsEvenWhenTheTransportIgnoresCancellation()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<UpdateRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken networkToken = default;
        var coordinator = new StartupCoordinator(_directory, checkUpdates: token =>
        {
            networkToken = token;
            started.TrySetResult(true);
            return pending.Task;
        }, updateTimeout: TimeSpan.FromMilliseconds(120));
        using var session = coordinator.Start(true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(StartupUpdateState.TimedOut, (await session.Update.WaitAsync(TimeSpan.FromSeconds(3))).State);
        Assert.True(networkToken.IsCancellationRequested);
        Assert.Null((await session.Library).Error);
        // The session observes this late error instead of surfacing it after operation has opened.
        pending.TrySetException(new HttpRequestException("Late network error"));
        Assert.Equal(StartupUpdateState.TimedOut, (await session.Update).State);
    }

    [Fact]
    public async Task SkipCancelsTheRequestWithoutChangingTheAutomaticUpdatePreference()
    {
        var preferences = new StudioPreferences();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<UpdateRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken networkToken = default;
        var coordinator = new StartupCoordinator(_directory, checkUpdates: token =>
        {
            networkToken = token;
            started.TrySetResult(true);
            return pending.Task;
        }, updateTimeout: TimeSpan.FromSeconds(30));
        using var session = coordinator.Start(preferences.AutoCheckUpdates);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.SkipUpdateCheck();
        Assert.Equal(StartupUpdateState.Cancelled, (await session.Update.WaitAsync(TimeSpan.FromSeconds(3))).State);
        Assert.True(networkToken.IsCancellationRequested);
        Assert.True(preferences.AutoCheckUpdates);
        Assert.Null((await session.Library).Error);
        pending.TrySetResult(null);
    }

    [Fact]
    public async Task CorruptLibraryIsPreservedByteForByteWithoutAnAutomaticSave()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "library.json");
        var original = System.Text.Encoding.UTF8.GetBytes("{corrompido: preservar este arquivo");
        File.WriteAllBytes(path, original);
        using var session = new StartupCoordinator(_directory).Start(false);
        var loaded = await session.Library;
        Assert.NotNull(loaded.Error);
        Assert.Contains("preservado", loaded.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("suspensa", loaded.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(loaded.Store);
        Assert.NotEmpty(loaded.Document.Songs);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task ADirectoryCreationFailureReturnsAnOfflineDocumentAndLeavesTheExistingFile()
    {
        Directory.CreateDirectory(_directory);
        var blocked = Path.Combine(_directory, "arquivo-em-vez-de-pasta");
        File.WriteAllText(blocked, "preservar");
        using var session = new StartupCoordinator(blocked).Start(false);
        var loaded = await session.Library;
        Assert.NotNull(loaded.Error);
        Assert.Null(loaded.Store);
        Assert.NotEmpty(loaded.Document.Songs);
        Assert.Equal("preservar", File.ReadAllText(blocked));
    }

    [Fact]
    public async Task AReleaseIsPassedThroughForTheOperatorToReviewWithoutDownloadingOrInstalling()
    {
        var release = SampleRelease();
        using var session = new StartupCoordinator(_directory, checkUpdates: _ => Task.FromResult<UpdateRelease?>(release)).Start(true);
        var result = await session.Update;
        Assert.Equal(StartupUpdateState.Available, result.State);
        Assert.Same(release, result.Release);
        Assert.Contains("9.0.0", result.Message);
        Assert.False(Directory.Exists(Path.Combine(_directory, "Updates")));
    }

    [Fact]
    public async Task NoNewReleaseHasASeparateUpToDateResult()
    {
        using var session = new StartupCoordinator(_directory, checkUpdates: _ => Task.FromResult<UpdateRelease?>(null)).Start(true);
        Assert.Equal(StartupUpdateState.UpToDate, (await session.Update).State);
    }

    [Fact]
    public async Task CancellationStopsTheLibraryAndTheNetworkWithoutReturningAnOpeningDocument()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new StartupCoordinator(_directory, loadLibrary: token =>
        {
            started.TrySetResult(true);
            token.WaitHandle.WaitOne();
            token.ThrowIfCancellationRequested();
            return new StartupLibraryResult(LibraryStore.CreateInitial(), null, null);
        }, checkUpdates: token => Task.Delay(Timeout.InfiniteTimeSpan, token).ContinueWith<UpdateRelease?>(_ => null));
        using var cancellation = new CancellationTokenSource();
        using var session = coordinator.Start(true, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.Library);
        Assert.Equal(StartupUpdateState.Cancelled, (await session.Update).State);
    }

    [Fact]
    public async Task EveryOpeningCreatesANewLibraryLoadAndUpdateCheck()
    {
        var loads = 0;
        var checks = 0;
        var coordinator = new StartupCoordinator(_directory,
            loadLibrary: _ => { Interlocked.Increment(ref loads); return new StartupLibraryResult(LibraryStore.CreateInitial(), null, null); },
            checkUpdates: _ => { Interlocked.Increment(ref checks); return Task.FromResult<UpdateRelease?>(null); });
        using (var first = coordinator.Start(true)) { await first.Library; await first.Update; }
        using (var second = coordinator.Start(true)) { await second.Library; await second.Update; }
        Assert.Equal(2, loads);
        Assert.Equal(2, checks);
    }

    private static UpdateRelease SampleRelease() => new(new Version(9, 0, 0), "v9.0.0", "Atualização de teste", "Somente metadados", null,
        new Uri(UpdateService.RepositoryUrl + "/releases/tag/v9.0.0"),
        new Uri(UpdateService.RepositoryUrl + "/releases/download/v9.0.0/WorshipStudio-Setup-9.0.0.exe"),
        "WorshipStudio-Setup-9.0.0.exe", 123, null,
        new Uri(UpdateService.RepositoryUrl + "/releases/download/v9.0.0/SHA256SUMS-v9.0.0.txt"), "SHA256SUMS-v9.0.0.txt", 100, null);
}
