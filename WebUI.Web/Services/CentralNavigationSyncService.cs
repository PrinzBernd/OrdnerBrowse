using System.Collections.Concurrent;
using System.Diagnostics;
using WebUI.Infrastructure;

namespace WebUI.Web.Services;

public sealed class WebUiSessionUnavailableException : InvalidOperationException
{
    public WebUiSessionUnavailableException()
        : base("Die Web.UI-Sitzung ist nicht mehr aktiv.")
    {
    }
}

public sealed record CentralSyncProgress(
    int Percent,
    int LoadedDocuments,
    int TotalDocuments,
    string StatusText);

public sealed record CentralSyncStatus(
    bool IsRunning,
    bool IsFullRefresh,
    DateTimeOffset? StartedAtUtc,
    int Percent,
    int LoadedDocuments,
    int TotalDocuments,
    string StatusText);

public sealed class CentralNavigationSyncService : IAsyncDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    private readonly NavigationCacheService _navigationCache;
    private readonly ILogger<CentralNavigationSyncService> _logger;
    private readonly ConcurrentDictionary<string, SyncChannel> _channels = new();
    private readonly CancellationTokenSource _shutdown = new();

    public CentralNavigationSyncService(
        NavigationCacheService navigationCache,
        ILogger<CentralNavigationSyncService> logger)
    {
        _navigationCache = navigationCache;
        _logger = logger;
    }

    public IDisposable Register(
        string cacheKey,
        Func<bool> isAvailable,
        Func<NavigationCacheSnapshot, IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> checkAsync,
        Func<CentralSyncStatus, Task> statusChangedAsync,
        Func<NavigationRefreshResult, bool, Task> completedAsync,
        Func<CentralSyncFailure, Task> failedAsync,
        string? diagnosticRunId = null)
    {
        var channel = _channels.GetOrAdd(
            cacheKey,
            key => new SyncChannel(
                key,
                _navigationCache,
                _logger,
                CheckInterval,
                _shutdown.Token));

        return channel.Register(isAvailable, checkAsync, statusChangedAsync, completedAsync, failedAsync, diagnosticRunId);
    }

    public void RequestImmediateCheck(string cacheKey)
    {
        if (_channels.TryGetValue(cacheKey, out var channel))
        {
            channel.RequestImmediateCheck();
        }
    }

    public Task RequestFullRefreshAsync(
        string cacheKey,
        Func<bool> isAvailable,
        Func<IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> refreshAsync)
    {
        if (!_channels.TryGetValue(cacheKey, out var channel))
        {
            throw new InvalidOperationException("Für den Navigationscache ist noch kein zentraler Kanal registriert.");
        }

        return channel.RequestFullRefreshAsync(isAvailable, refreshAsync);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();

        foreach (var channel in _channels.Values)
        {
            await channel.DisposeAsync();
        }

        _shutdown.Dispose();
    }

    private sealed class SyncChannel : IAsyncDisposable
    {
        private readonly string _cacheKey;
        private readonly NavigationCacheService _navigationCache;
        private readonly ILogger<CentralNavigationSyncService> _logger;
        private readonly TimeSpan _interval;
        private readonly CancellationToken _shutdown;
        private readonly object _gate = new();
        private readonly Dictionary<Guid, Subscriber> _subscribers = [];
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private readonly SemaphoreSlim _operationGate = new(1, 1);
        private readonly Task _loopTask;
        private Task? _manualRefreshTask;
        private CentralSyncStatus _status = new(false, false, null, 0, 0, 0, "");

        public SyncChannel(
            string cacheKey,
            NavigationCacheService navigationCache,
            ILogger<CentralNavigationSyncService> logger,
            TimeSpan interval,
            CancellationToken shutdown)
        {
            _cacheKey = cacheKey;
            _navigationCache = navigationCache;
            _logger = logger;
            _interval = interval;
            _shutdown = shutdown;
            _loopTask = RunAsync();
        }

        public IDisposable Register(
            Func<bool> isAvailable,
            Func<NavigationCacheSnapshot, IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> checkAsync,
            Func<CentralSyncStatus, Task> statusChangedAsync,
            Func<NavigationRefreshResult, bool, Task> completedAsync,
            Func<CentralSyncFailure, Task> failedAsync,
            string? diagnosticRunId)
        {
            var id = Guid.NewGuid();
            var subscriber = new Subscriber(
                isAvailable,
                checkAsync,
                statusChangedAsync,
                completedAsync,
                failedAsync,
                diagnosticRunId);
            CentralSyncStatus currentStatus;

            lock (_gate)
            {
                _subscribers[id] = subscriber;
                currentStatus = _status;
            }

            if (currentStatus.IsRunning && IsSubscriberAvailable(subscriber))
            {
                _ = SafeNotifyAsync(() => statusChangedAsync(currentStatus));
            }

            return new Registration(() =>
            {
                subscriber.Deactivate();

                lock (_gate)
                {
                    _subscribers.Remove(id);
                }
            });
        }

        public void RequestImmediateCheck()
        {
            try
            {
                _wakeSignal.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }

        public Task RequestFullRefreshAsync(
            Func<bool> isAvailable,
            Func<IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> refreshAsync)
        {
            TaskCompletionSource<bool> completion;

            lock (_gate)
            {
                if (_manualRefreshTask is not null)
                {
                    return _manualRefreshTask;
                }

                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _manualRefreshTask = completion.Task;
            }

            _ = RunFullRefreshAndCompleteAsync(
                isAvailable,
                refreshAsync,
                completion);
            return completion.Task;
        }

        private async Task RunFullRefreshAndCompleteAsync(
            Func<bool> isAvailable,
            Func<IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> refreshAsync,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                await RunFullRefreshCoreAsync(isAvailable, refreshAsync);
                completion.TrySetResult(true);
            }
            catch (OperationCanceledException exception)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_manualRefreshTask, completion.Task))
                    {
                        _manualRefreshTask = null;
                    }
                }
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    var wasSignaled = await _wakeSignal.WaitAsync(_interval, _shutdown);
                    await RunSingleCheckAsync(wasSignaled ? "IMMEDIATE" : "PERIODIC");
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RunSingleCheckAsync(string trigger)
        {
            lock (_gate)
            {
                if (_manualRefreshTask is not null)
                {
                    return;
                }
            }

            var gateWatch = Stopwatch.StartNew();
            await _operationGate.WaitAsync(_shutdown);
            gateWatch.Stop();
            try
            {
                lock (_gate)
                {
                    if (_manualRefreshTask is not null)
                    {
                        return;
                    }
                }

                var snapshot = await _navigationCache.LoadAsync(_cacheKey, _shutdown);
                if (snapshot is null)
                {
                    PerformanceDiagnosticsLog.Write(null, "SYNC", "CHECK", "SKIP_NO_CACHE", gateWatch.Elapsed.TotalMilliseconds, $"trigger={trigger}");
                    return;
                }

                var firstSubscriber = GetAvailableSubscriber(new HashSet<Subscriber>());
                if (firstSubscriber is null)
                {
                    PerformanceDiagnosticsLog.Write(null, "SYNC", "CHECK", "SKIP_NO_SUBSCRIBER", gateWatch.Elapsed.TotalMilliseconds, $"trigger={trigger};count={snapshot.Documents.Count}");
                    return;
                }

                PerformanceDiagnosticsLog.Write(firstSubscriber.DiagnosticRunId, "SYNC", "CHECK", "START", 0, $"trigger={trigger};gate_wait_ms={gateWatch.Elapsed.TotalMilliseconds:F3};count={snapshot.Documents.Count}");

                await SetStatusAsync(new(
                    true, false, DateTimeOffset.UtcNow, 0, 0, 0,
                    "Paperless wird zentral auf Änderungen geprüft …"));

                var overallCheckWatch = Stopwatch.StartNew();
                try
                {
                    var startedAt = DateTimeOffset.UtcNow;
                    _logger.LogInformation(
                        "Zentrale Navigationsprüfung gestartet.");
                    var progress = new InlineProgress<CentralSyncProgress>(value =>
                    {
                        _ = SetStatusAsync(new(
                            true,
                            false,
                            startedAt,
                            value.Percent,
                            value.LoadedDocuments,
                            value.TotalDocuments,
                            value.StatusText));
                    });

                    var attemptedSubscribers = new HashSet<Subscriber>();
                    var subscriber = firstSubscriber;

                    for (var attempt = 0; attempt < 2; attempt++)
                    {
                        if (subscriber is null)
                        {
                            break;
                        }

                        attemptedSubscribers.Add(subscriber);

                        if (!IsSubscriberAvailable(subscriber))
                        {
                            subscriber = GetAvailableSubscriber(attemptedSubscribers);
                            continue;
                        }

                        try
                        {
                            var checkWatch = Stopwatch.StartNew();
                            var result = await subscriber.CheckAsync(snapshot, progress);
                            checkWatch.Stop();
                            PerformanceDiagnosticsLog.Write(subscriber.DiagnosticRunId, "SYNC", "CHECK", "OK", checkWatch.Elapsed.TotalMilliseconds, $"trigger={trigger};rebuilt={result.Rebuilt};count={result.Snapshot.Documents.Count}");
                            _logger.LogInformation(
                                "Zentrale Navigationsprüfung beendet. Rebuilt: {Rebuilt}; DauerMs: {DurationMs}",
                                result.Rebuilt,
                                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
                            await NotifyCompletedAsync(result, false);
                            return;
                        }
                        catch (WebUiSessionUnavailableException)
                        {
                            _logger.LogInformation(
                                "Ausführende Web.UI-Sitzung während der zentralen Navigationsprüfung weggefallen. Versuch: {Attempt}",
                                attempt + 1);
                            subscriber = GetAvailableSubscriber(attemptedSubscribers);
                        }
                    }

                    _logger.LogInformation(
                        "Zentrale Navigationsprüfung ohne Fehler beendet, weil keine ausführbare Web.UI-Sitzung mehr verfügbar ist.");
                }
                catch (Exception exception)
                {
                    var error = PaperlessErrorClassifier.Classify(
                        exception,
                        _shutdown);

                    if (!error.IsControlledCancellation)
                    {
                        overallCheckWatch.Stop();
                        PerformanceDiagnosticsLog.WriteFailure(firstSubscriber.DiagnosticRunId, "SYNC", "CHECK", overallCheckWatch.Elapsed.TotalMilliseconds, exception, $"trigger={trigger};category={error.Category};status={error.StatusCode};timeout={error.IsTimeout}");
                        LogFailure(
                            "Zentrale Navigationsprüfung",
                            error,
                            isFullRefresh: false);
                        await NotifyFailedAsync(new(false, error));
                    }
                }
                finally
                {
                    await SetStatusAsync(new(false, false, null, 0, 0, 0, ""));
                }
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task RunFullRefreshCoreAsync(
            Func<bool> isAvailable,
            Func<IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> refreshAsync)
        {
            var gateWatch = Stopwatch.StartNew();
            await _operationGate.WaitAsync(_shutdown);
            gateWatch.Stop();
            var diagnosticSubscriber = GetAvailableSubscriber(new HashSet<Subscriber>());
            PerformanceDiagnosticsLog.Write(diagnosticSubscriber?.DiagnosticRunId, "SYNC", "FULL_REFRESH", "START", 0, $"gate_wait_ms={gateWatch.Elapsed.TotalMilliseconds:F3}");
            try
            {
                if (!IsCallbackAvailable(isAvailable))
                {
                    _logger.LogInformation(
                        "Manuelle Navigationsaktualisierung nicht gestartet, weil die anfordernde Web.UI-Sitzung nicht mehr verfügbar ist.");
                    return;
                }

                var startedAt = DateTimeOffset.UtcNow;
                await SetStatusAsync(new(
                    true, true, startedAt, 0, 0, 0,
                    "Vollständige Aktualisierung wird gestartet …"));

                var progress = new InlineProgress<CentralSyncProgress>(value =>
                {
                    _ = SetStatusAsync(new(
                        true,
                        true,
                        startedAt,
                        value.Percent,
                        value.LoadedDocuments,
                        value.TotalDocuments,
                        value.StatusText));
                });

                var overallRefreshWatch = Stopwatch.StartNew();
                try
                {
                    if (!IsCallbackAvailable(isAvailable))
                    {
                        _logger.LogInformation(
                            "Manuelle Navigationsaktualisierung vor dem Rückruf beendet, weil die anfordernde Web.UI-Sitzung nicht mehr verfügbar ist.");
                        return;
                    }

                    var refreshWatch = Stopwatch.StartNew();
                    var result = await refreshAsync(progress);
                    refreshWatch.Stop();
                    PerformanceDiagnosticsLog.Write(diagnosticSubscriber?.DiagnosticRunId, "SYNC", "FULL_REFRESH", "OK", refreshWatch.Elapsed.TotalMilliseconds, $"rebuilt={result.Rebuilt};count={result.Snapshot.Documents.Count}");
                    await NotifyCompletedAsync(result, true);
                }
                catch (WebUiSessionUnavailableException)
                {
                    _logger.LogInformation(
                        "Manuelle Navigationsaktualisierung kontrolliert beendet, weil die anfordernde Web.UI-Sitzung weggefallen ist.");
                }
                catch (Exception exception)
                {
                    var error = PaperlessErrorClassifier.Classify(
                        exception,
                        _shutdown);

                    if (!error.IsControlledCancellation)
                    {
                        overallRefreshWatch.Stop();
                        PerformanceDiagnosticsLog.WriteFailure(diagnosticSubscriber?.DiagnosticRunId, "SYNC", "FULL_REFRESH", overallRefreshWatch.Elapsed.TotalMilliseconds, exception, $"category={error.Category};status={error.StatusCode};timeout={error.IsTimeout}");
                        LogFailure(
                            "Manuelle Navigationsaktualisierung",
                            error,
                            isFullRefresh: true);
                        await NotifyFailedAsync(new(true, error));
                    }
                }
                finally
                {
                    await SetStatusAsync(new(false, false, null, 0, 0, 0, ""));
                }
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private Subscriber? GetAvailableSubscriber(
            IReadOnlySet<Subscriber> excludedSubscribers)
        {
            Subscriber[] subscribers;

            lock (_gate)
            {
                subscribers = _subscribers.Values.ToArray();
            }

            return subscribers.FirstOrDefault(subscriber =>
                !excludedSubscribers.Contains(subscriber) &&
                IsSubscriberAvailable(subscriber));
        }

        private Subscriber[] GetAvailableSubscribers()
        {
            Subscriber[] subscribers;

            lock (_gate)
            {
                subscribers = _subscribers.Values.ToArray();
            }

            return subscribers
                .Where(IsSubscriberAvailable)
                .ToArray();
        }

        private static bool IsSubscriberAvailable(Subscriber subscriber)
        {
            return subscriber.IsRegistered &&
                   IsCallbackAvailable(subscriber.IsAvailable);
        }

        private static bool IsCallbackAvailable(Func<bool> isAvailable)
        {
            try
            {
                return isAvailable();
            }
            catch
            {
                return false;
            }
        }

        private async Task SetStatusAsync(CentralSyncStatus status)
        {
            lock (_gate)
            {
                _status = status;
            }

            var subscribers = GetAvailableSubscribers();
            await NotifyAsync(subscribers, subscriber => subscriber.StatusChangedAsync(status));
        }

        private Task NotifyCompletedAsync(NavigationRefreshResult result, bool isFullRefresh)
        {
            var subscribers = GetAvailableSubscribers();
            return NotifyAsync(subscribers, subscriber => subscriber.CompletedAsync(result, isFullRefresh));
        }

        private Task NotifyFailedAsync(CentralSyncFailure failure)
        {
            var subscribers = GetAvailableSubscribers();
            return NotifyAsync(subscribers, subscriber => subscriber.FailedAsync(failure));
        }

        private void LogFailure(
            string action,
            PaperlessErrorInfo error,
            bool isFullRefresh)
        {
            _logger.LogError(
                "Paperless-Aktion fehlgeschlagen. Ereigniskennung: {EventId}; Aktion: {Action}; Kategorie: {Category}; HTTP-Status: {StatusCode}; Ausnahmetyp: {ExceptionType}; Zeitüberschreitung: {IsTimeout}; Vollständige Aktualisierung: {IsFullRefresh}",
                error.EventId,
                action,
                error.Category,
                error.StatusCode,
                error.ExceptionType,
                error.IsTimeout,
                isFullRefresh);
        }

        private static async Task NotifyAsync(
            IEnumerable<Subscriber> subscribers,
            Func<Subscriber, Task> notification)
        {
            foreach (var subscriber in subscribers)
            {
                if (!IsSubscriberAvailable(subscriber))
                {
                    continue;
                }

                await SafeNotifyAsync(() => notification(subscriber));
            }
        }

        private static async Task SafeNotifyAsync(Func<Task> notification)
        {
            try
            {
                await notification();
            }
            catch
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
            }

            _wakeSignal.Dispose();
            _operationGate.Dispose();
        }

        private sealed class Subscriber
        {
            private int _isRegistered = 1;

            public Subscriber(
                Func<bool> isAvailable,
                Func<NavigationCacheSnapshot, IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> checkAsync,
                Func<CentralSyncStatus, Task> statusChangedAsync,
                Func<NavigationRefreshResult, bool, Task> completedAsync,
                Func<CentralSyncFailure, Task> failedAsync,
                string? diagnosticRunId)
            {
                IsAvailable = isAvailable;
                CheckAsync = checkAsync;
                StatusChangedAsync = statusChangedAsync;
                CompletedAsync = completedAsync;
                FailedAsync = failedAsync;
                DiagnosticRunId = diagnosticRunId;
            }

            public bool IsRegistered => Volatile.Read(ref _isRegistered) == 1;
            public Func<bool> IsAvailable { get; }
            public Func<NavigationCacheSnapshot, IProgress<CentralSyncProgress>, Task<NavigationRefreshResult>> CheckAsync { get; }
            public Func<CentralSyncStatus, Task> StatusChangedAsync { get; }
            public Func<NavigationRefreshResult, bool, Task> CompletedAsync { get; }
            public Func<CentralSyncFailure, Task> FailedAsync { get; }
            public string? DiagnosticRunId { get; }

            public void Deactivate()
            {
                Volatile.Write(ref _isRegistered, 0);
            }
        }

        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public InlineProgress(Action<T> report)
            {
                _report = report;
            }

            public void Report(T value)
            {
                _report(value);
            }
        }

        private sealed class Registration : IDisposable
        {
            private Action? _dispose;

            public Registration(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _dispose, null)?.Invoke();
            }
        }
    }
}
