using WorshipStudio.Models;

namespace WorshipStudio.Services;

public sealed record StartupLibraryResult(StudioDocument Document, LibraryStore? Store, string? Error);

public enum StartupUpdateState { Disabled, UpToDate, Available, Unavailable, TimedOut, Cancelled }

public sealed record StartupUpdateResult(StartupUpdateState State, UpdateRelease? Release, string Message);

/// <summary>Loads local data and checks the optional update independently, away from the UI thread.</summary>
public sealed class StartupCoordinator
{
    private readonly Func<CancellationToken, StartupLibraryResult> _loadLibrary;
    private readonly Func<CancellationToken, Task<UpdateRelease?>> _checkUpdates;
    private readonly TimeSpan _updateTimeout;

    public StartupCoordinator(string? dataDirectory = null,
        Func<CancellationToken, StartupLibraryResult>? loadLibrary = null,
        Func<CancellationToken, Task<UpdateRelease?>>? checkUpdates = null,
        TimeSpan? updateTimeout = null)
    {
        // Capture this launch's directory instead of consulting a mutable override from a worker later.
        var directory = dataDirectory ?? LibraryStore.DataDirectoryOverride ?? StudioPreferences.DefaultDirectory;
        _loadLibrary = loadLibrary ?? (token => LoadLocalLibrary(directory, token));
        _checkUpdates = checkUpdates ?? CheckOfficialUpdates;
        _updateTimeout = updateTimeout ?? TimeSpan.FromSeconds(6);
        if (_updateTimeout <= TimeSpan.Zero || _updateTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(updateTimeout));
    }

    public StartupSession Start(bool autoCheckUpdates, CancellationToken cancellationToken = default) =>
        new(_loadLibrary, _checkUpdates, autoCheckUpdates, _updateTimeout, cancellationToken);

    private static StartupLibraryResult LoadLocalLibrary(string directory, CancellationToken token)
    {
        LibraryStore? store = null;
        try
        {
            token.ThrowIfCancellationRequested();
            store = new LibraryStore(directory);
            var document = store.Load();
            token.ThrowIfCancellationRequested();
            return new StartupLibraryResult(document, store, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { return LibraryFailure(ex, store); }
    }

    internal static StartupLibraryResult LibraryFailure(Exception exception, LibraryStore? store = null) => new(
        LibraryStore.CreateInitial(), store,
        "Não foi possível carregar a biblioteca: " + exception.Message +
        "\nO arquivo original foi preservado. Escolha um backup para restaurar ou corrija library.json na pasta de dados. A gravação automática foi suspensa.");

    private static async Task<UpdateRelease?> CheckOfficialUpdates(CancellationToken token)
    {
        using var service = new UpdateService();
        return await service.CheckAsync(cancellationToken: token).ConfigureAwait(false);
    }
}

/// <summary>A single opening attempt. Skipping the network never skips or replaces the local library read.</summary>
public sealed class StartupSession : IDisposable
{
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationTokenSource _skipUpdate = new();
    private int _disposed;
    public Task<StartupLibraryResult> Library { get; }
    public Task<StartupUpdateResult> Update { get; }

    internal StartupSession(Func<CancellationToken, StartupLibraryResult> loadLibrary,
        Func<CancellationToken, Task<UpdateRelease?>> checkUpdates, bool enabled,
        TimeSpan updateTimeout, CancellationToken cancellationToken)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _lifetime.Token;
        Library = Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var result = loadLibrary(token);
                token.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { return StartupCoordinator.LibraryFailure(ex); }
        }, token);
        Update = CheckAsync(checkUpdates, enabled, updateTimeout, token);
        ObserveFailure(Library);
    }

    public void SkipUpdateCheck() => SafeCancel(_skipUpdate);
    public void Cancel() => SafeCancel(_lifetime);

    private async Task<StartupUpdateResult> CheckAsync(Func<CancellationToken, Task<UpdateRelease?>> checkUpdates,
        bool enabled, TimeSpan timeout, CancellationToken lifetime)
    {
        if (!enabled) return new(StartupUpdateState.Disabled, null, "Busca automática desativada nas preferências.");
        using var deadline = new CancellationTokenSource(timeout);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime, _skipUpdate.Token, deadline.Token);
        var token = requestCancellation.Token;
        // Even a transport that blocks before returning a Task cannot stall the opening window.
        var request = Task.Run(() => checkUpdates(token), CancellationToken.None);
        try
        {
            var release = await request.WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return release == null
                ? new(StartupUpdateState.UpToDate, null, "Você está usando a versão mais recente.")
                : new(StartupUpdateState.Available, release, "Versão " + release.Version + " disponível. Você pode atualizar pelo aplicativo.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return deadline.IsCancellationRequested && !lifetime.IsCancellationRequested && !_skipUpdate.IsCancellationRequested
                ? new(StartupUpdateState.TimedOut, null, "A consulta demorou. O programa continuará offline.")
                : new(StartupUpdateState.Cancelled, null, "Consulta dispensada. O programa continuará offline.");
        }
        catch (Exception)
        { return new(StartupUpdateState.Unavailable, null, "Não foi possível consultar agora. Seus louvores continuam disponíveis offline."); }
        finally
        {
            SafeCancel(requestCancellation);
            // A client may ignore cancellation and fail after we have already opened offline.
            ObserveFailure(request);
        }
    }

    private static void ObserveFailure(Task task) => _ = task.ContinueWith(
        completed => { _ = completed.Exception; }, CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private static void SafeCancel(CancellationTokenSource cancellation)
    {
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { } // A third-party cancellation callback must not prevent opening or closing.
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        SafeCancel(_lifetime);
        SafeCancel(_skipUpdate);
        _lifetime.Dispose();
        _skipUpdate.Dispose();
    }
}
