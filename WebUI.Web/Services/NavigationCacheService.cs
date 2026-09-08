using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using WebUI.Infrastructure;

namespace WebUI.Web.Services;

public sealed class NavigationCacheService
{
    private const int CurrentFormatVersion = 3;

    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

    private readonly ConcurrentDictionary<
        string,
        NavigationCacheSnapshot> _memoryCache = new();

    private readonly ConcurrentDictionary<
        string,
        Task<NavigationRefreshResult>> _runningOperations = new();

    public NavigationCacheService(
        IConfiguration configuration)
    {
        var configuredDirectory =
            configuration["WebUi:NavigationCacheDirectory"];

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var normalizedDirectory =
                configuredDirectory.Trim();

            if (!Path.IsPathFullyQualified(normalizedDirectory))
            {
                throw new InvalidOperationException(
                    "Der konfigurierte Navigationscache-Pfad muss absolut sein.");
            }

            _cacheDirectory =
                Path.GetFullPath(normalizedDirectory);

            return;
        }

        var userProfile =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new InvalidOperationException(
                "Das Benutzerverzeichnis für den Navigationscache konnte nicht ermittelt werden.");
        }

        _cacheDirectory = OperatingSystem.IsMacOS()
            ? Path.Combine(
                userProfile,
                "Library",
                "Application Support",
                "WebUI",
                "navigation-cache")
            : Path.Combine(
                userProfile,
                ".local",
                "share",
                "WebUI",
                "navigation-cache");
    }

    public string CacheDirectory => _cacheDirectory;

    public Task<NavigationCacheSnapshot?> LoadAsync(
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        return LoadAsync(cacheKey, diagnosticRunId: null, cancellationToken: cancellationToken);
    }

    public async Task<NavigationCacheSnapshot?> LoadAsync(
        string cacheKey,
        string? diagnosticRunId,
        CancellationToken cancellationToken = default)
    {
        var overall = Stopwatch.StartNew();

        if (_memoryCache.TryGetValue(
                cacheKey,
                out var cached))
        {
            overall.Stop();
            PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "LOAD", "RAM_HIT", overall.Elapsed.TotalMilliseconds, $"count={cached.Documents.Count}");
            return cached;
        }

        var path = GetCachePath(cacheKey);

        if (!File.Exists(path))
        {
            overall.Stop();
            PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "LOAD", "FILE_NOT_FOUND", overall.Elapsed.TotalMilliseconds);
            return null;
        }

        try
        {
            var openWatch = Stopwatch.StartNew();
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
            openWatch.Stop();

            var deserializeWatch = Stopwatch.StartNew();
            var snapshot =
                await JsonSerializer.DeserializeAsync<
                    NavigationCacheSnapshot>(
                    stream,
                    _jsonOptions,
                    cancellationToken);
            deserializeWatch.Stop();

            if (snapshot is null)
            {
                overall.Stop();
                PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "LOAD", "DESERIALIZE_NULL", overall.Elapsed.TotalMilliseconds, $"open_ms={openWatch.Elapsed.TotalMilliseconds:F3};deserialize_ms={deserializeWatch.Elapsed.TotalMilliseconds:F3}");
                return null;
            }

            if (snapshot.FormatVersion != CurrentFormatVersion)
            {
                overall.Stop();
                PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "LOAD", "FORMAT_INVALID", overall.Elapsed.TotalMilliseconds, $"format={snapshot.FormatVersion};expected={CurrentFormatVersion};count={snapshot.Documents.Count}");
                return null;
            }

            _memoryCache[cacheKey] = snapshot;
            overall.Stop();
            PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "LOAD", "FILE_HIT", overall.Elapsed.TotalMilliseconds, $"count={snapshot.Documents.Count};open_ms={openWatch.Elapsed.TotalMilliseconds:F3};deserialize_ms={deserializeWatch.Elapsed.TotalMilliseconds:F3}");
            return snapshot;
        }
        catch (
            Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException)
        {
            overall.Stop();
            PerformanceDiagnosticsLog.WriteFailure(diagnosticRunId, "CACHE", "LOAD", overall.Elapsed.TotalMilliseconds, exception);
            return null;
        }
    }

    public Task SaveAsync(
        string cacheKey,
        NavigationCacheSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(cacheKey, snapshot, diagnosticRunId: null, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(
        string cacheKey,
        NavigationCacheSnapshot snapshot,
        string? diagnosticRunId,
        CancellationToken cancellationToken = default)
    {
        var overall = Stopwatch.StartNew();
        PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "SAVE", "START", 0, $"count={snapshot.Documents.Count}");
        Directory.CreateDirectory(
            _cacheDirectory);

        var probePath = Path.Combine(
            _cacheDirectory,
            $".write-test-{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllTextAsync(
                probePath,
                "ok",
                cancellationToken);
        }
        finally
        {
            if (File.Exists(probePath))
            {
                File.Delete(probePath);
            }
        }

        var path = GetCachePath(cacheKey);
        var temporaryPath =
            $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    _jsonOptions,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                path,
                overwrite: true);

            TryRestrictFilePermissions(path);

            _memoryCache[cacheKey] = snapshot;
            overall.Stop();
            PerformanceDiagnosticsLog.Write(diagnosticRunId, "CACHE", "SAVE", "OK", overall.Elapsed.TotalMilliseconds, $"count={snapshot.Documents.Count}");
        }
        catch (Exception exception)
        {
            overall.Stop();
            PerformanceDiagnosticsLog.WriteFailure(diagnosticRunId, "CACHE", "SAVE", overall.Elapsed.TotalMilliseconds, exception, $"count={snapshot.Documents.Count}");
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Eine übrig gebliebene temporäre Datei
                    // beeinträchtigt den laufenden Cache nicht.
                }
            }
        }
    }

    public Task<NavigationRefreshResult>
        RunSingleOperationAsync(
            string cacheKey,
            Func<Task<NavigationRefreshResult>>
                operationFactory)
    {
        return _runningOperations.GetOrAdd(
            cacheKey,
            _ => ExecuteAndRemoveAsync(
                cacheKey,
                operationFactory));
    }

    private async Task<NavigationRefreshResult>
        ExecuteAndRemoveAsync(
            string cacheKey,
            Func<Task<NavigationRefreshResult>>
                operationFactory)
    {
        try
        {
            return await operationFactory();
        }
        finally
        {
            _runningOperations.TryRemove(
                cacheKey,
                out _);
        }
    }

    private string GetCachePath(string cacheKey)
    {
        var safeKey = new string(
            cacheKey
                .Where(char.IsAsciiHexDigit)
                .ToArray());

        if (safeKey.Length < 16)
        {
            throw new InvalidOperationException(
                "Der Cacheschlüssel ist ungültig.");
        }

        return Path.Combine(
            _cacheDirectory,
            $"navigation-{safeKey}.json");
    }

    private static void TryRestrictFilePermissions(
        string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);
        }
        catch
        {
            // Der Cache enthält keine Tokens oder Dokumentinhalte.
            // Fehlende Unix-Rechteunterstützung blockiert daher
            // nicht die Anwendung.
        }
    }
}

public sealed class NavigationCacheSnapshot
{
    public int FormatVersion { get; init; } = 3;

    public DateTimeOffset BuiltAtUtc { get; init; }

    public Dictionary<int, NavigationAreaStateDto>
        SourceStates { get; set; } = [];

    public List<NavigationDocumentDto>
        Documents { get; init; } = [];

    public static NavigationCacheSnapshot Create(
        IReadOnlyCollection<NavigationDocumentDto> documents)
    {
        var storedDocuments = documents
            .GroupBy(document => document.Id)
            .Select(group => group.OrderByDescending(item => item.ModifiedUtc).First())
            .OrderBy(document => document.Id)
            .ToList();

        var snapshot = new NavigationCacheSnapshot
        {
            FormatVersion = 3,
            BuiltAtUtc = DateTimeOffset.UtcNow,
            Documents = storedDocuments
        };
        snapshot.SourceStates = snapshot.CalculateSourceStates();
        return snapshot;
    }

    public NavigationCacheSnapshot ApplyChanges(
        IReadOnlyCollection<NavigationDocumentDto> changedDocuments)
    {
        var byId = Documents.ToDictionary(document => document.Id);

        foreach (var document in changedDocuments)
        {
            byId[document.Id] = document;
        }

        var snapshot = new NavigationCacheSnapshot
        {
            FormatVersion = 3,
            BuiltAtUtc = DateTimeOffset.UtcNow,
            Documents = byId.Values.OrderBy(document => document.Id).ToList()
        };
        snapshot.SourceStates = snapshot.CalculateSourceStates();
        return snapshot;
    }

    private Dictionary<int, NavigationAreaStateDto> CalculateSourceStates()
    {
        DateTimeOffset? latest = Documents
            .Where(document => document.ModifiedUtc.HasValue)
            .Select(document => document.ModifiedUtc!.Value)
            .OrderByDescending(value => value)
            .Select(value => (DateTimeOffset?)value)
            .FirstOrDefault();

        return new Dictionary<int, NavigationAreaStateDto>
        {
            [-1] = new NavigationAreaStateDto(-1, Documents.Count, latest)
        };
    }

    public bool Matches(
        IReadOnlyDictionary<
            int,
            NavigationAreaStateDto> currentStates)
    {
        if (SourceStates.Count !=
            currentStates.Count)
        {
            return false;
        }

        foreach (var current in currentStates)
        {
            if (!SourceStates.TryGetValue(
                    current.Key,
                    out var cached))
            {
                return false;
            }

            if (cached.DocumentCount !=
                current.Value.DocumentCount)
            {
                return false;
            }

            if (ToUtcTicks(
                    cached.LatestModifiedUtc) !=
                ToUtcTicks(
                    current.Value.LatestModifiedUtc))
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyDictionary<int, AreaNavigationIndexDto> BuildIndexes(IEnumerable<int> storagePathKeys)
    {
        const int allAreaKey = -1;
        const int privateAreaKey = -3;
        var knownKeys = storagePathKeys.Where(id => id > 0).ToHashSet();
        var builders = storagePathKeys
            .Append(allAreaKey)
            .Append(privateAreaKey)
            .Distinct()
            .ToDictionary(id => id, _ => new AreaIndexBuilder());

        foreach (var document in Documents)
        {
            builders[allAreaKey].Add(document);

            var key = !document.StoragePathId.HasValue
                ? 0
                : knownKeys.Contains(document.StoragePathId.Value)
                    ? document.StoragePathId.Value
                    : privateAreaKey;

            if (builders.TryGetValue(key, out var builder))
                builder.Add(document);
        }

        return builders.ToDictionary(pair => pair.Key, pair => pair.Value.CreateResult());
    }

    private static long? ToUtcTicks(
        DateTimeOffset? timestamp)
    {
        return timestamp?.UtcDateTime.Ticks;
    }

    private sealed class MutableAreaState
    {
        public int DocumentCount { get; set; }

        public DateTimeOffset?
            LatestModifiedUtc { get; set; }
    }

    private sealed class AreaIndexBuilder
    {
        private readonly HashSet<int> _correspondentIds = [];
        private readonly Dictionary<int, HashSet<int>> _documentTypeIdsByCorrespondent = [];
        private readonly HashSet<int> _allDocumentTypeIds = [];
        private readonly HashSet<int> _documentTypeIdsForUnassignedCorrespondent = [];
        private readonly HashSet<int> _correspondentIdsWithUnassignedDocumentType = [];
        private int _documentCount;
        private bool _hasUnassignedCorrespondent;
        private bool _hasAnyUnassignedDocumentType;
        private bool _hasUnassignedDocumentTypeForUnassignedCorrespondent;

        public void Add(NavigationDocumentDto document)
        {
            _documentCount++;
            if (document.DocumentTypeId.HasValue)
                _allDocumentTypeIds.Add(document.DocumentTypeId.Value);
            else
                _hasAnyUnassignedDocumentType = true;

            if (!document.CorrespondentId.HasValue)
            {
                _hasUnassignedCorrespondent = true;
                if (document.DocumentTypeId.HasValue)
                    _documentTypeIdsForUnassignedCorrespondent.Add(document.DocumentTypeId.Value);
                else
                    _hasUnassignedDocumentTypeForUnassignedCorrespondent = true;
                return;
            }

            var correspondentId = document.CorrespondentId.Value;
            _correspondentIds.Add(correspondentId);
            if (!document.DocumentTypeId.HasValue)
            {
                _correspondentIdsWithUnassignedDocumentType.Add(correspondentId);
                return;
            }

            if (!_documentTypeIdsByCorrespondent.TryGetValue(correspondentId, out var typeIds))
            {
                typeIds = [];
                _documentTypeIdsByCorrespondent[correspondentId] = typeIds;
            }
            typeIds.Add(document.DocumentTypeId.Value);
        }

        public AreaNavigationIndexDto CreateResult() => new(
            _documentCount,
            _correspondentIds,
            _documentTypeIdsByCorrespondent,
            _allDocumentTypeIds,
            _documentTypeIdsForUnassignedCorrespondent,
            _hasUnassignedCorrespondent,
            _hasAnyUnassignedDocumentType,
            _hasUnassignedDocumentTypeForUnassignedCorrespondent,
            _correspondentIdsWithUnassignedDocumentType);
    }

}

public sealed record NavigationRefreshResult(
    NavigationCacheSnapshot Snapshot,
    bool Rebuilt,
    DateTimeOffset CheckedAtUtc);
