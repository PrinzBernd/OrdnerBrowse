using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Channels;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebUI.Infrastructure;

public sealed class PaperlessApiClient
{
    private const int NavigationPageSize = 1000;

    private readonly HttpClient _httpClient;

    public string? DiagnosticRunId { get; set; }

    public PaperlessApiClient(
        HttpClient httpClient,
        string baseUrl,
        string token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException(
                "Die Paperless-Adresse fehlt.",
                nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                "Das API-Token fehlt.",
                nameof(token));
        }

        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _httpClient.BaseAddress =
            new Uri(baseUrl.TrimEnd('/'));
        _httpClient.Timeout =
            TimeSpan.FromSeconds(45);

        _httpClient
            .DefaultRequestHeaders
            .Authorization =
                new AuthenticationHeaderValue(
                    "Token",
                    token);

        _httpClient
            .DefaultRequestHeaders
            .Accept
            .Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));
    }

    public async Task ValidateCurrentTokenAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/documents/?page_size=1&fields=id");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task<T> MeasureApiAsync<T>(
        string dataClass,
        string operation,
        Func<Task<T>> action,
        Func<T, string>? details = null)
    {
        var watch = Stopwatch.StartNew();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, dataClass, operation, "START", 0);
        try
        {
            var result = await action();
            watch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, dataClass, operation, "OK", watch.Elapsed.TotalMilliseconds, details?.Invoke(result));
            return result;
        }
        catch (Exception exception)
        {
            watch.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, dataClass, operation, watch.Elapsed.TotalMilliseconds, exception);
            throw;
        }
    }

    public async Task<PaperlessSystemStatusDto> GetSystemStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return await MeasureApiAsync(
            "SYS",
            "STATUS",
            async () => await _httpClient.GetFromJsonAsync<PaperlessSystemStatusDto>(
                "/api/status/?format=json",
                cancellationToken) ?? new PaperlessSystemStatusDto());
    }

    public async Task<
        IReadOnlyList<CorrespondentDto>>
        GetCorrespondentsAsync(
            CancellationToken cancellationToken =
                default)
    {
        return await GetAllPagedResultsAsync<CorrespondentDto>(
            "REF",
            "CORRESPONDENTS",
            "/api/correspondents/?page_size=2000&ordering=name",
            cancellationToken);
    }

    public async Task<
        IReadOnlyList<DocumentTypeDto>>
        GetDocumentTypesAsync(
            CancellationToken cancellationToken =
                default)
    {
        return await GetAllPagedResultsAsync<DocumentTypeDto>(
            "REF",
            "DOCUMENT_TYPES",
            "/api/document_types/?page_size=2000&ordering=name",
            cancellationToken);
    }


    public async Task<IReadOnlyList<StoragePathDto>>
        GetStoragePathsAsync(
            CancellationToken cancellationToken = default)
    {
        return await GetAllPagedResultsAsync<StoragePathDto>(
            "REF",
            "STORAGE_PATHS",
            "/api/storage_paths/?page_size=2000&ordering=name",
            cancellationToken);
    }

    public async Task<
        IReadOnlyList<TagDto>>
        GetTagsAsync(
            CancellationToken cancellationToken = default)
    {
        return await GetAllPagedResultsAsync<TagDto>(
            "REF",
            "TAGS",
            "/api/tags/?page_size=2000&ordering=name",
            cancellationToken);
    }

    public async Task<
        IReadOnlyList<CustomFieldDto>>
        GetCustomFieldsAsync(
            CancellationToken cancellationToken = default)
    {
        return await GetAllPagedResultsAsync<CustomFieldDto>(
            "REF",
            "CUSTOM_FIELDS",
            "/api/custom_fields/?page_size=2000&ordering=name",
            cancellationToken);
    }

    private async Task<IReadOnlyList<T>>
        GetAllPagedResultsAsync<T>(
            string dataClass,
            string operation,
            string initialPath,
            CancellationToken cancellationToken)
    {
        var overall = Stopwatch.StartNew();
        var results = new List<T>();
        var visitedPages = new HashSet<string>(
            StringComparer.Ordinal);
        string? nextPath = initialPath;
        var pageNumber = 0;
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, dataClass, operation, "START", 0, "page_size=2000");

        try
        {
        while (!string.IsNullOrWhiteSpace(nextPath))
        {
            pageNumber++;
            var pageWatch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();

            var pageUri = ResolvePaginationUri(
                _httpClient.BaseAddress!,
                nextPath);

            if (!visitedPages.Add(pageUri.AbsoluteUri))
            {
                throw new InvalidOperationException(
                    "Die Paperless-API hat eine bereits geladene Folgeseite erneut gemeldet.");
            }

            var response =
                await _httpClient.GetFromJsonAsync<
                    PagedResponse<T>>(
                    pageUri,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Die Paperless-API hat für eine Stammdatenseite keine auswertbare Antwort geliefert.");

            results.AddRange(response.Results);
            pageWatch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, dataClass, operation, "PAGE_OK", pageWatch.Elapsed.TotalMilliseconds, $"page={pageNumber};received={response.Results.Count};total_received={results.Count};has_next={!string.IsNullOrWhiteSpace(response.Next)}");

            if (response.Results.Count == 0 &&
                !string.IsNullOrWhiteSpace(response.Next))
            {
                throw new InvalidOperationException(
                    "Die Paperless-API hat eine leere Stammdatenseite mit einer weiteren Folgeseite gemeldet.");
            }

            nextPath = response.Next;
        }

        overall.Stop();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, dataClass, operation, "OK", overall.Elapsed.TotalMilliseconds, $"pages={pageNumber};count={results.Count}");
        return results;
        }
        catch (Exception exception)
        {
            overall.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, dataClass, operation, overall.Elapsed.TotalMilliseconds, exception);
            throw;
        }
    }

    private static Uri ResolvePaginationUri(
        Uri baseAddress,
        string nextPath)
    {
        Uri pageUri;

        try
        {
            pageUri = new Uri(
                baseAddress,
                nextPath);
        }
        catch (UriFormatException exception)
        {
            throw new InvalidOperationException(
                "Die Paperless-API hat eine ungültige Folgeseite gemeldet.",
                exception);
        }

        if (!string.IsNullOrEmpty(pageUri.UserInfo) ||
            !string.Equals(
                pageUri.Scheme,
                baseAddress.Scheme,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                pageUri.IdnHost,
                baseAddress.IdnHost,
                StringComparison.OrdinalIgnoreCase) ||
            pageUri.Port != baseAddress.Port)
        {
            throw new InvalidOperationException(
                "Die Paperless-API hat eine Folgeseite außerhalb der zulässigen Paperless-Adresse gemeldet.");
        }

        return pageUri;
    }

    public async Task<int> GetVisibleNavigationDocumentCountAsync(
        CancellationToken cancellationToken = default)
    {
        const string path = "/api/documents/?page_size=1&fields=id";

        return await MeasureApiAsync(
            "SYNC",
            "VISIBLE_COUNT",
            async () =>
            {
                var response = await _httpClient.GetFromJsonAsync<
                        PagedResponse<NavigationModifiedDocumentDto>>(
                        path,
                        cancellationToken)
                    ?? new PagedResponse<NavigationModifiedDocumentDto>();
                return response.Count;
            },
            count => $"count={count}");
    }

    public async Task<bool> HasNavigationDocumentChangesAsync(
        DateTimeOffset latestKnownModifiedUtc,
        int knownDocumentsAtLatestTimestamp,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(
            latestKnownModifiedUtc.UtcDateTime.ToString("O"));

        var path = "/api/documents/" +
            $"?modified__gte={encoded}&page_size=1" +
            "&ordering=-modified&fields=id,modified";

        return await MeasureApiAsync(
            "SYNC",
            "MODIFIED_CHECK",
            async () =>
            {
                var response = await _httpClient.GetFromJsonAsync<
                        PagedResponse<NavigationModifiedDocumentDto>>(
                        path,
                        cancellationToken)
                    ?? new PagedResponse<NavigationModifiedDocumentDto>();

                var newestModifiedUtc = response.Results
                    .FirstOrDefault()?
                    .ModifiedUtc;

                return response.Count != knownDocumentsAtLatestTimestamp ||
                       (newestModifiedUtc.HasValue &&
                        newestModifiedUtc.Value > latestKnownModifiedUtc);
            },
            changed => $"changed={changed};known_at_latest={knownDocumentsAtLatestTimestamp}");
    }


    public async Task<IReadOnlySet<int>> GetTrashDocumentIdsAsync(
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 1000;
        var watch = Stopwatch.StartNew();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, "SYNC", "TRASH_IDS", "START", 0, $"page_size={pageSize}");
        var page = 1;
        var ids = new HashSet<int>();

        while (true)
        {
            var path =
                $"/api/trash/?page={page}&page_size={pageSize}&fields=id,deleted_at";

            var response = await _httpClient.GetFromJsonAsync<
                    PagedResponse<TrashDocumentDto>>(
                    path,
                    cancellationToken)
                ?? new PagedResponse<TrashDocumentDto>();

            foreach (var item in response.Results)
            {
                if (item.Id > 0)
                {
                    ids.Add(item.Id);
                }
            }

            if (ids.Count >= response.Count || response.Results.Count == 0)
            {
                break;
            }

            page++;
        }

        watch.Stop();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, "SYNC", "TRASH_IDS", "OK", watch.Elapsed.TotalMilliseconds, $"pages={page};count={ids.Count}");
        return ids;
    }

    public async Task<
        IReadOnlyDictionary<int, NavigationAreaStateDto>>
        GetNavigationSourceStateAsync(
            CancellationToken cancellationToken = default)
    {
        return await MeasureApiAsync(
            "NAV",
            "SOURCE_STATE",
            async () =>
            {
                var path = "/api/documents/?page=1&page_size=1&ordering=-modified&fields=id,modified";
                var response = await _httpClient.GetFromJsonAsync<PagedResponse<NavigationModifiedDocumentDto>>(
                    path,
                    cancellationToken) ?? new PagedResponse<NavigationModifiedDocumentDto>();

                return (IReadOnlyDictionary<int, NavigationAreaStateDto>)new Dictionary<int, NavigationAreaStateDto>
                {
                    [-1] = new NavigationAreaStateDto(
                        -1,
                        response.Count,
                        response.Results.FirstOrDefault()?.ModifiedUtc)
                };
            },
            state => $"count={state.Values.Sum(value => value.DocumentCount)}");
    }

    public async Task<IReadOnlyList<NavigationDocumentDto>>
        GetNavigationDocumentsAsync(
            IProgress<AreaNavigationProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        var overall = Stopwatch.StartNew();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, "NAV", "FULL_REBUILD_FETCH", "START", 0, $"requested_page_size={NavigationPageSize}");
        var firstPage = await GetAllNavigationPageAsync(1, cancellationToken);
        var totalDocuments = firstPage.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalDocuments / (double)NavigationPageSize));
        var documents = new List<NavigationDocumentDto>(Math.Max(totalDocuments, 0));
        documents.AddRange(firstPage.Results);
        progress?.Report(new AreaNavigationProgress(1, totalPages, documents.Count, totalDocuments));

        for (var page = 2; page <= totalPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await GetAllNavigationPageAsync(page, cancellationToken);
            documents.AddRange(result.Results);
            progress?.Report(new AreaNavigationProgress(page, totalPages, documents.Count, totalDocuments));
        }

        overall.Stop();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, "NAV", "FULL_REBUILD_FETCH", "OK", overall.Elapsed.TotalMilliseconds, $"pages={totalPages};count={documents.Count};reported_count={totalDocuments}");
        return documents;
    }

    public async Task<IReadOnlyList<NavigationDocumentDto>>
        GetNavigationDocumentsModifiedSinceAsync(
            DateTimeOffset modifiedSinceUtc,
            CancellationToken cancellationToken = default)
    {
        var overall = Stopwatch.StartNew();
        var encoded = Uri.EscapeDataString(modifiedSinceUtc.UtcDateTime.ToString("O"));
        var page = 1;
        var documents = new List<NavigationDocumentDto>();
        PerformanceDiagnosticsLog.Write(DiagnosticRunId, "NAV", "INCREMENTAL_FETCH", "START", 0, $"requested_page_size={NavigationPageSize}");

        try
        {
            while (true)
            {
                var pageWatch = Stopwatch.StartNew();
                var path = "/api/documents/" +
                    $"?modified__gte={encoded}&page={page}&page_size={NavigationPageSize}" +
                    "&ordering=modified&fields=id,storage_path,correspondent,document_type,modified";
                var result = await _httpClient.GetFromJsonAsync<PagedResponse<NavigationDocumentDto>>(path, cancellationToken)
                    ?? new PagedResponse<NavigationDocumentDto>();

                documents.AddRange(result.Results);
                pageWatch.Stop();
                PerformanceDiagnosticsLog.Write(DiagnosticRunId, "NAV", "INCREMENTAL_PAGE", "OK", pageWatch.Elapsed.TotalMilliseconds, $"page={page};requested_page_size={NavigationPageSize};received={result.Results.Count};total_received={documents.Count};has_next={!string.IsNullOrWhiteSpace(result.Next)}");

                if (string.IsNullOrWhiteSpace(result.Next))
                    break;
                page++;
            }

            overall.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, "NAV", "INCREMENTAL_FETCH", "OK", overall.Elapsed.TotalMilliseconds, $"pages={page};count={documents.Count}");
            return documents;
        }
        catch (Exception exception)
        {
            overall.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, "NAV", "INCREMENTAL_FETCH", overall.Elapsed.TotalMilliseconds, exception, $"page={page};count={documents.Count}");
            throw;
        }
    }

    public async Task<PagedResponse<DocumentListItemDto>>
        GetDocumentsAsync(
            int page = 1,
            int pageSize = 20,
            int? storagePathId = null,
            IReadOnlyCollection<int>? storagePathIds = null,
            bool storagePathIsNull = false,
            int? correspondentId = null,
            IReadOnlyCollection<int>? correspondentIds = null,
            bool correspondentIsNull = false,
            int? documentTypeId = null,
            IReadOnlyCollection<int>? documentTypeIds = null,
            bool documentTypeIsNull = false,
            bool sortAscending = false,
            string? titleContains = null,
            CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"page_size={pageSize}",
            $"ordering={(sortAscending ? "created" : "-created")}",
            "fields=id,title,created,correspondent,document_type,storage_path,tags,custom_fields,page_count,mime_type,original_file_name"
        };

        if (storagePathIsNull) query.Add("storage_path__isnull=true");
        else if (storagePathIds is { Count: > 0 })
            query.Add($"storage_path__id__in={string.Join(",", storagePathIds.Distinct().OrderBy(id => id))}");
        else if (storagePathId.HasValue) query.Add($"storage_path__id={storagePathId.Value}");

        if (correspondentIsNull) query.Add("correspondent__isnull=true");
        else if (correspondentIds is { Count: > 0 })
            query.Add($"correspondent__id__in={string.Join(",", correspondentIds.Distinct().OrderBy(id => id))}");
        else if (correspondentId.HasValue) query.Add($"correspondent__id={correspondentId.Value}");

        if (documentTypeIsNull) query.Add("document_type__isnull=true");
        else if (documentTypeIds is { Count: > 0 })
            query.Add($"document_type__id__in={string.Join(",", documentTypeIds.Distinct().OrderBy(id => id))}");
        else if (documentTypeId.HasValue) query.Add($"document_type__id={documentTypeId.Value}");

        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            query.Add($"title__icontains={Uri.EscapeDataString(titleContains.Trim())}");
        }

        var path = "/api/documents/?" + string.Join("&", query);
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await _httpClient.GetFromJsonAsync<PagedResponse<DocumentListItemDto>>(path, cancellationToken)
                ?? new PagedResponse<DocumentListItemDto>();
            watch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, "LIST", "DOCUMENT_PAGE", "OK", watch.Elapsed.TotalMilliseconds, $"page={page};requested_page_size={pageSize};received={result.Results.Count};count={result.Count}");
            return result;
        }
        catch (Exception exception)
        {
            watch.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, "LIST", "DOCUMENT_PAGE", watch.Elapsed.TotalMilliseconds, exception, $"page={page};requested_page_size={pageSize}");
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentListItemDto>>
        GetAllDocumentsAsync(
            int? storagePathId = null,
            IReadOnlyCollection<int>? storagePathIds = null,
            bool storagePathIsNull = false,
            int? correspondentId = null,
            IReadOnlyCollection<int>? correspondentIds = null,
            bool correspondentIsNull = false,
            int? documentTypeId = null,
            IReadOnlyCollection<int>? documentTypeIds = null,
            bool documentTypeIsNull = false,
            bool sortAscending = false,
            string? titleContains = null,
            CancellationToken cancellationToken = default)
    {
        const int pageSize = 1000;
        var page = 1;
        var documents = new List<DocumentListItemDto>();

        while (true)
        {
            var result = await GetDocumentsAsync(
                page: page,
                pageSize: pageSize,
                storagePathId: storagePathId,
                storagePathIds: storagePathIds,
                storagePathIsNull: storagePathIsNull,
                correspondentId: correspondentId,
                correspondentIds: correspondentIds,
                correspondentIsNull: correspondentIsNull,
                documentTypeId: documentTypeId,
                documentTypeIds: documentTypeIds,
                documentTypeIsNull: documentTypeIsNull,
                sortAscending: sortAscending,
                titleContains: titleContains,
                cancellationToken: cancellationToken);

            documents.AddRange(result.Results);

            if (documents.Count >= result.Count || result.Results.Count == 0)
            {
                break;
            }

            page++;
        }

        return documents;
    }

    public async Task<DocumentBinaryDto>
        GetDocumentThumbnailAsync(
            int documentId,
            CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(documentId),
                "Die Dokument-ID muss größer als 0 sein.");
        }

        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.GetAsync(
                $"/api/documents/{documentId}/thumb/",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var contentType =
                response.Content.Headers.ContentType?.MediaType
                ?? "image/webp";

            var bytes = await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

            watch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, "DETAIL", "DOCUMENT_THUMBNAIL", "OK", watch.Elapsed.TotalMilliseconds, $"bytes={bytes.Length}");
            return new DocumentBinaryDto(bytes, contentType);
        }
        catch (Exception exception)
        {
            watch.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, "DETAIL", "DOCUMENT_THUMBNAIL", watch.Elapsed.TotalMilliseconds, exception);
            throw;
        }
    }

    public async Task<DocumentBinaryDto>
        GetDocumentPreviewAsync(
            int documentId,
            CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(documentId),
                "Die Dokument-ID muss größer als 0 sein.");
        }

        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.GetAsync(
                $"/api/documents/{documentId}/preview/",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var contentType =
                response.Content.Headers.ContentType?.MediaType
                ?? "application/pdf";

            var bytes = await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

            watch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, "DETAIL", "DOCUMENT_PREVIEW", "OK", watch.Elapsed.TotalMilliseconds, $"bytes={bytes.Length}");
            return new DocumentBinaryDto(bytes, contentType);
        }
        catch (Exception exception)
        {
            watch.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, "DETAIL", "DOCUMENT_PREVIEW", watch.Elapsed.TotalMilliseconds, exception);
            throw;
        }
    }

    public async Task<DocumentDetailsDto?>
        GetDocumentAsync(
            int documentId,
            CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(documentId),
                "Die Dokument-ID muss größer als 0 sein.");
        }

        const string fields =
            "id,title,correspondent,document_type,storage_path," +
            "created,modified,added,page_count,mime_type," +
            "original_file_name,archive_serial_number,notes";

        var path =
            $"/api/documents/{documentId}/?fields={fields}";

        var watch = Stopwatch.StartNew();
        try
        {
            var result = await _httpClient.GetFromJsonAsync<DocumentDetailsDto>(
                path,
                cancellationToken);
            watch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, "DETAIL", "DOCUMENT_DETAILS", "OK", watch.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception exception)
        {
            watch.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, "DETAIL", "DOCUMENT_DETAILS", watch.Elapsed.TotalMilliseconds, exception);
            throw;
        }
    }

    private async Task<PagedResponse<NavigationDocumentDto>>
        GetAllNavigationPageAsync(int page, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var path = "/api/documents/" +
                $"?page={page}&page_size={NavigationPageSize}" +
                "&ordering=id&fields=id,storage_path,correspondent,document_type,modified";
            var result = await _httpClient.GetFromJsonAsync<PagedResponse<NavigationDocumentDto>>(path, cancellationToken)
                ?? new PagedResponse<NavigationDocumentDto>();
            watch.Stop();
            PerformanceDiagnosticsLog.Write(DiagnosticRunId, "NAV", "FULL_PAGE", "OK", watch.Elapsed.TotalMilliseconds, $"page={page};requested_page_size={NavigationPageSize};received={result.Results.Count};count={result.Count};has_next={!string.IsNullOrWhiteSpace(result.Next)}");
            return result;
        }
        catch (Exception exception)
        {
            watch.Stop();
            PerformanceDiagnosticsLog.WriteFailure(DiagnosticRunId, "NAV", "FULL_PAGE", watch.Elapsed.TotalMilliseconds, exception, $"page={page};requested_page_size={NavigationPageSize}");
            throw;
        }
    }


}

public sealed class TrashDocumentDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAtUtc { get; init; }
}

public sealed record NavigationAreaStateDto(
    int StoragePathId,
    int DocumentCount,
    DateTimeOffset? LatestModifiedUtc);

public sealed record AreaNavigationProgress(
    int LoadedPages,
    int TotalPages,
    int LoadedDocuments,
    int TotalDocuments)
{
    public int Percent =>
        TotalPages <= 0
            ? 0
            : Math.Clamp(
                (int)Math.Round(
                    LoadedPages *
                    100d /
                    TotalPages),
                0,
                100);
}

public sealed record AreaNavigationIndexDto(
    int DocumentCount,
    IReadOnlySet<int> CorrespondentIds,
    IReadOnlyDictionary<int, HashSet<int>> DocumentTypeIdsByCorrespondent,
    IReadOnlySet<int> AllDocumentTypeIds,
    IReadOnlySet<int> DocumentTypeIdsForUnassignedCorrespondent,
    bool HasUnassignedCorrespondent,
    bool HasAnyUnassignedDocumentType,
    bool HasUnassignedDocumentTypeForUnassignedCorrespondent,
    IReadOnlySet<int> CorrespondentIdsWithUnassignedDocumentType)
{
    public static AreaNavigationIndexDto Empty { get; } = new(
        0, new HashSet<int>(), new Dictionary<int, HashSet<int>>(),
        new HashSet<int>(), new HashSet<int>(), false, false, false,
        new HashSet<int>());
}

public sealed class NavigationDocumentDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("storage_path")]
    public int? StoragePathId { get; init; }

    [JsonPropertyName("correspondent")]
    public int? CorrespondentId { get; init; }

    [JsonPropertyName("document_type")]
    public int? DocumentTypeId { get; init; }

    [JsonPropertyName("modified")]
    public DateTimeOffset?
        ModifiedUtc { get; init; }
}

public sealed class NavigationModifiedDocumentDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("modified")]
    public DateTimeOffset?
        ModifiedUtc { get; init; }
}

public sealed class PagedResponse<T>
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("next")]
    public string? Next { get; init; }

    [JsonPropertyName("previous")]
    public string? Previous { get; init; }

    [JsonPropertyName("results")]
    public List<T> Results { get; init; } = [];
}

public sealed class CorrespondentDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } =
        string.Empty;

    [JsonPropertyName("document_count")]
    public int DocumentCount { get; init; }
}

public sealed class DocumentTypeDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } =
        string.Empty;

    [JsonPropertyName("document_count")]
    public int DocumentCount { get; init; }
}

public sealed class PaperlessSystemStatusDto
{
    [JsonPropertyName("pngx_version")]
    public string? PaperlessNgxVersion { get; init; }
}

public sealed class StoragePathDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed class TagDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; init; }
}

public sealed class CustomFieldDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("data_type")]
    public string DataType { get; init; } = string.Empty;

    [JsonPropertyName("extra_data")]
    public JsonElement ExtraData { get; init; }
}

public sealed class DocumentListItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } =
        string.Empty;

    [JsonPropertyName("created")]
    public DateTimeOffset? CreatedUtc { get; init; }

    [JsonPropertyName("correspondent")]
    public int? CorrespondentId { get; init; }

    [JsonPropertyName("document_type")]
    public int? DocumentTypeId { get; init; }

    [JsonPropertyName("storage_path")]
    public int? StoragePathId { get; init; }

    [JsonPropertyName("tags")]
    public List<int> TagIds { get; init; } = [];

    [JsonPropertyName("custom_fields")]
    public List<DocumentCustomFieldValueDto>
        CustomFields { get; init; } = [];

    [JsonPropertyName("page_count")]
    public int? PageCount { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("original_file_name")]
    public string? OriginalFileName { get; init; }
}

public sealed class DocumentCustomFieldValueDto
{
    [JsonPropertyName("field")]
    public int FieldId { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }
}


public sealed record DocumentBinaryDto(byte[] Content, string ContentType);

public sealed class DocumentDetailsDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("correspondent")]
    public int? CorrespondentId { get; init; }

    [JsonPropertyName("document_type")]
    public int? DocumentTypeId { get; init; }

    [JsonPropertyName("storage_path")]
    public int? StoragePathId { get; init; }

    [JsonPropertyName("created")]
    public DateTimeOffset? CreatedUtc { get; init; }

    [JsonPropertyName("modified")]
    public DateTimeOffset? ModifiedUtc { get; init; }

    [JsonPropertyName("added")]
    public DateTimeOffset? AddedUtc { get; init; }

    [JsonPropertyName("page_count")]
    public int? PageCount { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("original_file_name")]
    public string? OriginalFileName { get; init; }

    [JsonPropertyName("archive_serial_number")]
    public int? ArchiveSerialNumber { get; init; }

    [JsonPropertyName("notes")]
    public List<DocumentNoteDto> Notes { get; init; } = [];
}

public sealed class DocumentNoteDto
{
    [JsonPropertyName("note")]
    public string Note { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTimeOffset? CreatedUtc { get; init; }

    [JsonPropertyName("user")]
    public DocumentNoteUserDto? User { get; init; }
}

public sealed class DocumentNoteUserDto
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; init; } = string.Empty;
}


public static class PerformanceDiagnosticsLog
{
    private sealed record LogEntry(DateTimeOffset Timestamp, string Line);
    private static readonly Channel<LogEntry> Queue = Channel.CreateUnbounded<LogEntry>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private static readonly object Sync = new();
    private static readonly Task WriterTask = Task.Run(WriteLoopAsync);
    private static string? _logDirectory;
    private static int _retentionDays = 7;
    private static string _applicationVersion = "unbekannt";
    private static DateTimeOffset _lastCleanupUtc = DateTimeOffset.MinValue;

    public static string CreateRunId() =>
        $"PERF-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")[..8]}";

    public static void Configure(string? logDirectory, int retentionDays, string applicationVersion)
    {
        if (retentionDays is < 1 or > 365)
            throw new InvalidOperationException("WebUi:PerformanceDiagnostics:RetentionDays muss zwischen 1 und 365 liegen.");
        var normalized = string.IsNullOrWhiteSpace(logDirectory) ? null : logDirectory.Trim();
        if (normalized is not null && !Path.IsPathRooted(normalized))
            throw new InvalidOperationException("WebUi:PerformanceDiagnostics:LogDirectory muss ein absoluter Pfad sein.");
        if (string.IsNullOrWhiteSpace(applicationVersion))
            throw new InvalidOperationException("Die WebUI-Version für Performance Diagnostics fehlt.");
        lock (Sync)
        {
            _logDirectory = normalized is null ? null : Path.GetFullPath(normalized);
            _retentionDays = retentionDays;
            _applicationVersion = applicationVersion.Trim();
            _lastCleanupUtc = DateTimeOffset.MinValue;
        }
    }

    public static void Write(string? runId, string dataClass, string operation, string result, double durationMs, string? details = null)
    {
        _ = WriterTask;
        var now = DateTimeOffset.UtcNow;
        string version;
        lock (Sync) version = _applicationVersion;
        var line = string.Join('\t', new[]
        {
            now.ToString("O"), Sanitize(version), Sanitize(runId ?? "PERF-NO-RUN"), Sanitize(dataClass),
            Sanitize(operation), Sanitize(result), durationMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            Sanitize(details ?? string.Empty)
        });
        Queue.Writer.TryWrite(new LogEntry(now,line));
    }

    public static void WriteFailure(string? runId, string dataClass, string operation, double durationMs, Exception exception, string? details = null)
    {
        var timeout = exception is TaskCanceledException or TimeoutException;
        var statusCode = exception is HttpRequestException h && h.StatusCode.HasValue
            ? ((int)h.StatusCode.Value).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        var safe = $"timeout_candidate={timeout};exception={exception.GetType().Name};http_status={statusCode}";
        if (!string.IsNullOrWhiteSpace(details)) safe += $";{details}";
        Write(runId,dataClass,operation,"ERROR",durationMs,safe);
    }

    private static async Task WriteLoopAsync()
    {
        await foreach (var entry in Queue.Reader.ReadAllAsync())
        {
            try
            {
                string? configured; int retention;
                lock (Sync) { configured=_logDirectory; retention=_retentionDays; }
                var dir = configured ?? Path.Combine(Path.GetTempPath(),"WebUI","performance-diagnostics");
                Directory.CreateDirectory(dir);
                CleanupIfDue(dir,retention,entry.Timestamp);
                var path = Path.Combine(dir,$"performance-diagnostics-{entry.Timestamp:yyyy-MM-dd}.log");
                var header = !File.Exists(path) || new FileInfo(path).Length==0;
                await using var stream = new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite,16*1024,useAsync:true);
                await using var writer = new StreamWriter(stream);
                if (header) await writer.WriteLineAsync("ZeitUtc\tVersion\tMesslauf\tKlasse\tOperation\tErgebnis\tDauerMs\tDetails");
                await writer.WriteLineAsync(entry.Line);
                await writer.FlushAsync();
            }
            catch { }
        }
    }

    private static void CleanupIfDue(string directory, int retentionDays, DateTimeOffset now)
    {
        lock (Sync)
        {
            if (_lastCleanupUtc != DateTimeOffset.MinValue && now-_lastCleanupUtc < TimeSpan.FromHours(1)) return;
            _lastCleanupUtc=now;
        }
        var cutoff=now.UtcDateTime-TimeSpan.FromDays(retentionDays);
        foreach (var path in Directory.EnumerateFiles(directory,"performance-diagnostics-*.log",SearchOption.TopDirectoryOnly))
        {
            try { if (File.GetLastWriteTimeUtc(path)<cutoff) File.Delete(path); } catch { }
        }
    }

    private static string Sanitize(string value) => value.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');
}
