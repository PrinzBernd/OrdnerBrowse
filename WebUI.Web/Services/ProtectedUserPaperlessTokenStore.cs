using Microsoft.AspNetCore.DataProtection;
using WebUI.Infrastructure;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace WebUI.Web.Services;

public sealed partial class ProtectedUserPaperlessTokenStore : IPaperlessConnectionTokenStore
{
    private const string ProtectorPurpose =
        "webui.user-paperless-token.v1";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly string _tokenDirectory;

    public ProtectedUserPaperlessTokenStore(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _configuration = configuration;

        var configuredDirectory = configuration["Paperless:UserTokenDirectory"];

        if (string.IsNullOrWhiteSpace(configuredDirectory) ||
            !Path.IsPathFullyQualified(configuredDirectory.Trim()))
        {
            throw new InvalidOperationException(
                "Für die persönliche Paperless-Zuordnung muss 'Paperless:UserTokenDirectory' als absoluter serverseitiger Pfad konfiguriert sein.");
        }

        _tokenDirectory = Path.GetFullPath(configuredDirectory.Trim());
    }

    public Task<PaperlessConnectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var technicalUserKey = GetTechnicalUserKey();
        var tokenFilePath = GetTokenFilePath(technicalUserKey);

        if (!File.Exists(tokenFilePath))
        {
            return Task.FromResult(
                new PaperlessConnectionStatus(false));
        }

        return Task.FromResult(
            new PaperlessConnectionStatus(true));
    }

    public async Task<PreparedPaperlessToken> PrepareAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        _ = GetTechnicalUserKey();

        var normalizedToken = ValidateTokenInput(token);
        var baseUrl = ReadAndValidateBaseUrl();

        var client = new PaperlessApiClient(
            _httpClientFactory.CreateClient(PaperlessClientFactory.HttpClientName),
            baseUrl,
            normalizedToken);
        await client.ValidateCurrentTokenAsync(cancellationToken);

        var protectedToken = _protector.Protect(normalizedToken);

        return new PreparedPaperlessToken(protectedToken);
    }

    public async Task SavePreparedAsync(
        PreparedPaperlessToken preparedToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparedToken);

        var technicalUserKey = GetTechnicalUserKey();

        string validationToken;

        try
        {
            validationToken = _protector.Unprotect(
                preparedToken.ProtectedToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Die vorbereitete Paperless-Verbindung ist nicht mehr gültig.",
                exception);
        }

        _ = ValidateTokenInput(validationToken);

        Directory.CreateDirectory(_tokenDirectory);
        SetDirectoryPermissionsWhenSupported(_tokenDirectory);

        var targetPath = GetTokenFilePath(technicalUserKey);
        var temporaryPath =
            $"{targetPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                preparedToken.ProtectedToken + Environment.NewLine,
                cancellationToken);

            SetFilePermissionsWhenSupported(temporaryPath);

            File.Move(
                temporaryPath,
                targetPath,
                overwrite: true);

            SetFilePermissionsWhenSupported(targetPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private string GetTechnicalUserKey()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "Für diesen Vorgang ist eine Anmeldung erforderlich.");
        }

        var technicalUserKey = user.FindFirstValue(
            OidcAuthenticationSettings.TechnicalUserKeyClaimType);

        if (string.IsNullOrWhiteSpace(technicalUserKey) ||
            !TechnicalUserKeyPattern().IsMatch(technicalUserKey))
        {
            throw new InvalidOperationException(
                "Die angemeldete Identität enthält keine gültige technische Benutzerkennung.");
        }

        return technicalUserKey;
    }

    private string GetTokenFilePath(
        string technicalUserKey)
    {
        return Path.Combine(
            _tokenDirectory,
            $"{technicalUserKey}.protected");
    }

    private string ReadAndValidateBaseUrl()
    {
        var configuredValue =
            ConfigurationFileValueReader.ReadRequiredValueOrFile(
                _configuration.GetSection("Paperless"),
                "BaseUrl",
                "BaseUrlFilePath",
                "Paperless-Basisadresse");

        var normalizedValue =
            configuredValue.Trim().TrimEnd('/');

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
                "Die konfigurierte Paperless-Basisadresse ist ungültig.");
        }

        return normalizedValue;
    }

    private static string ValidateTokenInput(
        string token)
    {
        var normalizedToken = token.Trim();

        if (string.IsNullOrWhiteSpace(normalizedToken) ||
            normalizedToken.Contains('\r') ||
            normalizedToken.Contains('\n') ||
            normalizedToken.Length > 4096)
        {
            throw new InvalidOperationException(
                "Das API-Token ist leer oder besitzt kein zulässiges einzeiliges Format.");
        }

        return normalizedToken;
    }

    private static void SetDirectoryPermissionsWhenSupported(
        string directoryPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            directoryPath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);
    }

    private static void SetFilePermissionsWhenSupported(
        string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            filePath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite);
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalUserKeyPattern();
}

