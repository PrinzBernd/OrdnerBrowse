using System.Security.Claims;

namespace WebUI.Web.Services;

public sealed class LocalTestUserKeychainTokenProvider : IPaperlessTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LocalKeychainPaperlessSettings _keychainSettings;

    public LocalTestUserKeychainTokenProvider(
        IHttpContextAccessor httpContextAccessor,
        LocalKeychainPaperlessSettings keychainSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _keychainSettings = keychainSettings;
    }

    public async Task<PaperlessTokenContext> GetTokenContextAsync(
        CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "Für den lokalen Mehrbenutzerbetrieb ist eine Anmeldung erforderlich.");
        }

        var alias =
            user.FindFirstValue(LocalTestUserSettings.AliasClaimType);
        var technicalUserKey =
            user.FindFirstValue(
                LocalTestUserSettings.TechnicalUserKeyClaimType);
        var sessionId =
            user.FindFirstValue(
                LocalTestUserSettings.SessionIdClaimType);

        if (string.IsNullOrWhiteSpace(technicalUserKey) ||
            string.IsNullOrWhiteSpace(sessionId) ||
            !Guid.TryParseExact(sessionId, "N", out _))
        {
            throw new InvalidOperationException(
                "Die lokale Anmeldung enthält keine gültige technische Benutzer- und Sitzungszuordnung.");
        }

        LocalTestUserCredential credential;

        if (_keychainSettings.IsMacDesktop)
        {
            var username =
                LocalKeychainPaperlessSettings.NormalizeMacDesktopUsername(
                    alias ?? string.Empty);
            credential = await _keychainSettings.GetMacDesktopCredentialAsync(
                username,
                cancellationToken);
        }
        else
        {
            if (!LocalTestUserSettings.TryGetUser(alias, out var localUser))
            {
                throw new InvalidOperationException(
                    "Die lokale Testanmeldung enthält keine gültige Testidentität.");
            }

            credential = await _keychainSettings.GetCredentialAsync(
                localUser,
                cancellationToken);
        }

        if (!string.Equals(
                technicalUserKey,
                credential.TechnicalUserKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die lokale Anmeldung stimmt nicht mehr mit der aktuellen Schlüsselbundzuordnung überein.");
        }

        return new PaperlessTokenContext(
            credential.Token,
            credential.TechnicalUserKey,
            sessionId);
    }
}
