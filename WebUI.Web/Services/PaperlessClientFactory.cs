using System.Security.Cryptography;
using System.Text;
using WebUI.Infrastructure;

namespace WebUI.Web.Services;

public interface IPaperlessTokenProvider
{
    Task<PaperlessTokenContext> GetTokenContextAsync(
        CancellationToken cancellationToken = default);
}

public sealed record PaperlessTokenContext(
    string Token,
    string CachePartition,
    string? SessionId = null);

public sealed record PaperlessClientContext(
    PaperlessApiClient Client,
    string CacheKey,
    string BaseUrl,
    string? SessionId);

public sealed class PaperlessClientFactory
{
    public const string HttpClientName = "PaperlessApi";

    private readonly IPaperlessTokenProvider _tokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly LocalKeychainPaperlessSettings? _localKeychainSettings;
    private readonly bool _localMultiUserEnabled;

    public PaperlessClientFactory(
        IPaperlessTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        OidcAuthenticationSettings oidcSettings,
        LocalKeychainPaperlessSettings? localKeychainSettings = null)
    {
        _tokenProvider = tokenProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _localKeychainSettings = localKeychainSettings;
        _localMultiUserEnabled = LocalTestUserSettings.IsEnabled(
            environment,
            configuration,
            oidcSettings.Enabled);
    }

    public async Task<PaperlessClientContext> CreateContextAsync(
        CancellationToken cancellationToken = default)
    {
        var baseUrl = await GetBaseUrlAsync(cancellationToken);
        var tokenContext = await _tokenProvider.GetTokenContextAsync(
            cancellationToken);

        return new PaperlessClientContext(
            new PaperlessApiClient(
                _httpClientFactory.CreateClient(HttpClientName),
                baseUrl,
                tokenContext.Token),
            CreateCacheKey(baseUrl, tokenContext.CachePartition),
            baseUrl,
            tokenContext.SessionId);
    }

    public async Task<PaperlessApiClient> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        var baseUrl = await GetBaseUrlAsync(cancellationToken);
        var tokenContext = await _tokenProvider.GetTokenContextAsync(
            cancellationToken);

        return new PaperlessApiClient(
            _httpClientFactory.CreateClient(HttpClientName),
            baseUrl,
            tokenContext.Token);
    }

    private async Task<string> GetBaseUrlAsync(
        CancellationToken cancellationToken)
    {
        if (_localMultiUserEnabled)
        {
            if (_localKeychainSettings is null)
            {
                throw new InvalidOperationException(
                    "Der lokale Schlüsselbundzugriff für die Paperless-Basisadresse ist nicht registriert.");
            }

            return await _localKeychainSettings.GetBaseUrlAsync(
                cancellationToken);
        }

        return ReadAndValidateBaseUrl(_configuration);
    }

    private static string CreateCacheKey(
        string baseUrl,
        string cachePartition)
    {
        var source = $"{baseUrl}\n{cachePartition}";
        var bytes = Encoding.UTF8.GetBytes(source);
        var hash = SHA256.HashData(bytes);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private static string ReadAndValidateBaseUrl(
        IConfiguration configuration)
    {
        var configuredValue = ConfigurationFileValueReader
            .ReadRequiredValueOrFile(
                configuration.GetSection("Paperless"),
                "BaseUrl",
                "BaseUrlFilePath",
                "Paperless-Basisadresse");

        var normalizedValue = configuredValue.TrimEnd('/');

        if (!Uri.TryCreate(
                normalizedValue,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "Die konfigurierte Paperless-Basisadresse ist ungültig. Erwartet wird eine absolute HTTP- oder HTTPS-Adresse ohne Benutzerinformationen, Abfrage oder Fragment.");
        }

        return normalizedValue;
    }
}
