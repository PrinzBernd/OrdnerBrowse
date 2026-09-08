using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace WebUI.Web.Services;

public sealed partial class ProtectedUserPaperlessTokenProvider : IPaperlessTokenProvider
{
    private const string ProtectorPurpose =
        "webui.user-paperless-token.v1";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;
    private readonly string _tokenDirectory;

    public ProtectedUserPaperlessTokenProvider(
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

        var configuredDirectory = configuration["Paperless:UserTokenDirectory"];

        if (string.IsNullOrWhiteSpace(configuredDirectory) ||
            !Path.IsPathFullyQualified(configuredDirectory.Trim()))
        {
            throw new InvalidOperationException(
                "Für den OIDC-Mehrbenutzerbetrieb muss 'Paperless:UserTokenDirectory' als absoluter serverseitiger Pfad konfiguriert sein.");
        }

        _tokenDirectory = Path.GetFullPath(configuredDirectory.Trim());
    }

    public async Task<PaperlessTokenContext> GetTokenContextAsync(
        CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "Für diesen Zugriff ist eine Anmeldung erforderlich.");
        }

        var technicalUserKey = user.FindFirstValue(
            OidcAuthenticationSettings.TechnicalUserKeyClaimType);

        var sessionId = user.FindFirstValue(
            OidcAuthenticationSettings.SessionIdClaimType);

        if (string.IsNullOrWhiteSpace(technicalUserKey) ||
            !TechnicalUserKeyPattern().IsMatch(technicalUserKey))
        {
            throw new InvalidOperationException(
                "Die angemeldete Identität enthält keine gültige technische Benutzerkennung.");
        }

        if (string.IsNullOrWhiteSpace(sessionId) ||
            !Guid.TryParseExact(sessionId, "N", out _))
        {
            throw new InvalidOperationException(
                "Die angemeldete Sitzung enthält keine gültige technische Sitzungskennung.");
        }

        var tokenFilePath = Path.Combine(
            _tokenDirectory,
            $"{technicalUserKey}.protected");

        if (!File.Exists(tokenFilePath))
        {
            throw new InvalidOperationException(
                "Für den angemeldeten Benutzer ist noch kein persönlicher Paperless-Zugang hinterlegt.");
        }

        string protectedToken;

        try
        {
            protectedToken = (await File.ReadAllTextAsync(
                tokenFilePath,
                cancellationToken)).Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Der verschlüsselte persönliche Paperless-Zugang konnte nicht gelesen werden.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(protectedToken) ||
            protectedToken.Contains('\r') ||
            protectedToken.Contains('\n'))
        {
            throw new InvalidOperationException(
                "Die persönliche Paperless-Zuordnung ist beschädigt.");
        }

        string token;

        try
        {
            token = _protector.Unprotect(protectedToken).Trim();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Die persönliche Paperless-Zuordnung konnte nicht entschlüsselt werden.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(token) ||
            token.Contains('\r') ||
            token.Contains('\n'))
        {
            throw new InvalidOperationException(
                "Die persönliche Paperless-Zuordnung enthält kein gültiges einzeiliges Token.");
        }

        return new PaperlessTokenContext(
            token,
            technicalUserKey,
            sessionId);
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalUserKeyPattern();
}
