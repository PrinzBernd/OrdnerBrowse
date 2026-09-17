using Microsoft.AspNetCore.DataProtection;
using WebUI.Infrastructure;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebUI.Web.Services;

public sealed record LocalTestUserCredential(
    string Token,
    string TechnicalUserKey);

public sealed record MacDesktopConnectionInfo(
    string Username,
    string BaseUrl,
    bool IsConfigured);

public sealed record PreparedMacDesktopConnection(
    string ProtectedPayload);

public sealed class LocalKeychainPaperlessSettings : IPaperlessConnectionTokenStore
{
    private const string BaseUrlService =
        "webui.local.paperless-base-url";
    private const string BaseUrlAccount =
        "paperless-base-url";
    private const string PaperlessUserIdService =
        "webui.local.paperless-user-id";
    private const string TokenService =
        "webui.local.paperless-api-token";
    private const string TokenFingerprintService =
        "webui.local.paperless-token-fingerprint";

    private const string MacDesktopBaseUrlService =
        "webui.macdesktop.paperless-base-url";
    private const string MacDesktopPaperlessUserIdService =
        "webui.macdesktop.paperless-user-id";
    private const string MacDesktopTokenService =
        "webui.macdesktop.paperless-api-token";
    private const string MacDesktopTokenFingerprintService =
        "webui.macdesktop.paperless-token-fingerprint";

    private const string ProtectorPurpose =
        "webui.local-paperless-token-change.v1";
    private const string MacDesktopProtectorPurpose =
        "webui.macdesktop-paperless-connection-change.v1";

    public const string UsernameTokenMismatchMessage =
        "Der eingegebene Paperless-Benutzername stimmt nicht mit dem verwendeten API-Token überein.";
    public const string PaperlessUserIdMismatchMessage =
        "Die gespeicherte Paperless-Benutzer-ID stimmt nicht mit der aktuell von Paperless gemeldeten Identität überein.";

    private const int ErrSecSuccess = 0;
    private const int ErrSecItemNotFound = -25300;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly IDataProtector _macDesktopProtector;

    public LocalKeychainPaperlessSettings(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _macDesktopProtector =
            dataProtectionProvider.CreateProtector(MacDesktopProtectorPurpose);

        IsMacDesktop = string.Equals(
            configuration["WebUi:RuntimeProfile"]?.Trim(),
            RuntimeProfile.MacDesktop,
            StringComparison.Ordinal);
    }

    public bool IsMacDesktop { get; }

    public async Task<string> GetBaseUrlAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsMacDesktop)
        {
            return await GetMacDesktopBaseUrlAsync(
                cancellationToken);
        }

        var value = await ReadSecretAsync(
            BaseUrlService,
            BaseUrlAccount,
            "Die lokale Paperless-Basisadresse ist im macOS-Schlüsselbund nicht verfügbar.",
            cancellationToken);

        return NormalizeBaseUrl(
            value,
            "Die lokale Paperless-Basisadresse im macOS-Schlüsselbund ist ungültig.");
    }

    public async Task<LocalTestUserCredential> GetCredentialAsync(
        LocalTestUserDefinition user,
        CancellationToken cancellationToken = default)
    {
        var paperlessUserId = await GetPaperlessUserIdAsync(
            user,
            cancellationToken);

        var token = await ReadSecretAsync(
            TokenService,
            GetTokenAccount(paperlessUserId),
            $"Für {user.DisplayName} ist kein persönlicher Paperless-Zugang im macOS-Schlüsselbund verfügbar.",
            cancellationToken);

        var expectedFingerprintValue = await ReadSecretAsync(
            TokenFingerprintService,
            user.KeychainAccount,
            $"Für {user.DisplayName} fehlt die lokale Token-Zuordnungsprüfung im macOS-Schlüsselbund.",
            cancellationToken);

        ValidateFingerprint(
            user.DisplayName,
            token,
            expectedFingerprintValue);

        var technicalUserKeyInput = Encoding.UTF8.GetBytes(
            $"webui/local-paperless-user-id/{paperlessUserId}");
        var technicalUserKey = Convert.ToHexString(
                SHA256.HashData(technicalUserKeyInput))
            .ToLowerInvariant();

        return new LocalTestUserCredential(
            token,
            technicalUserKey);
    }

    public async Task<LocalTestUserCredential> GetMacDesktopCredentialAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        EnsureMacDesktop();
        var normalizedUsername = NormalizeMacDesktopUsername(username);
        var snapshot = await ReadMacDesktopUserSnapshotAsync(
            normalizedUsername,
            cancellationToken);

        if (!snapshot.Exists)
        {
            throw new InvalidOperationException(
                "Für diesen Paperless-Benutzernamen ist noch keine MacDesktop-Verbindung eingerichtet.");
        }

        return new LocalTestUserCredential(
            snapshot.Token!,
            CreateMacDesktopTechnicalUserKey(snapshot.PaperlessUserId!.Value));
    }

    public async Task<string> GetMacDesktopBaseUrlAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureMacDesktop();
        var baseUrl = await ReadOptionalSecretAsync(
            MacDesktopBaseUrlService,
            BaseUrlAccount,
            cancellationToken);

        if (baseUrl is null)
        {
            throw new InvalidOperationException(
                "Die MacDesktop-Paperless-Basisadresse ist noch nicht eingerichtet.");
        }

        return NormalizeBaseUrl(
            baseUrl,
            "Die MacDesktop-Paperless-Basisadresse im macOS-Schlüsselbund ist ungültig.");
    }

    public async Task<PaperlessCurrentUserIdentity> GetMacDesktopIdentityAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        EnsureMacDesktop();
        var normalizedUsername = NormalizeMacDesktopUsername(username);
        var snapshot = await ReadMacDesktopUserSnapshotAsync(
            normalizedUsername,
            cancellationToken);

        if (!snapshot.Exists)
        {
            throw new InvalidOperationException(
                "Für diesen Paperless-Benutzernamen ist noch keine MacDesktop-Verbindung eingerichtet.");
        }

        var baseUrl = await GetMacDesktopBaseUrlAsync(cancellationToken);
        var identity = await GetCurrentUserIdentityAsync(
            baseUrl,
            snapshot.Token!,
            cancellationToken);

        if (identity.Id != snapshot.PaperlessUserId)
        {
            throw new InvalidOperationException(
                PaperlessUserIdMismatchMessage);
        }

        return identity;
    }

    public async Task<PaperlessConnectionStatus> GetMacDesktopStatusAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        EnsureMacDesktop();
        var normalizedUsername = NormalizeMacDesktopUsername(username);
        var baseUrl = await ReadOptionalSecretAsync(
            MacDesktopBaseUrlService,
            BaseUrlAccount,
            cancellationToken);
        var snapshot = await ReadMacDesktopUserSnapshotAsync(
            normalizedUsername,
            cancellationToken);

        if (!snapshot.Exists)
        {
            if (baseUrl is not null)
            {
                _ = NormalizeBaseUrl(
                    baseUrl,
                    "Die MacDesktop-Paperless-Basisadresse im macOS-Schlüsselbund ist ungültig.");
            }

            return new PaperlessConnectionStatus(false);
        }

        if (baseUrl is null)
        {
            throw new InvalidOperationException(
                "Die MacDesktop-Paperless-Verbindung ist im macOS-Schlüsselbund unvollständig und wird aus Sicherheitsgründen nicht automatisch überschrieben.");
        }

        _ = NormalizeBaseUrl(
            baseUrl,
            "Die MacDesktop-Paperless-Basisadresse im macOS-Schlüsselbund ist ungültig.");

        return new PaperlessConnectionStatus(true);
    }

    public async Task<MacDesktopConnectionInfo> GetMacDesktopConnectionInfoAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureMacDesktop();
        var username = GetCurrentMacDesktopUsername();
        var baseUrlValue = await ReadOptionalSecretAsync(
            MacDesktopBaseUrlService,
            BaseUrlAccount,
            cancellationToken);
        var snapshot = await ReadMacDesktopUserSnapshotAsync(
            username,
            cancellationToken);

        if (snapshot.Exists && baseUrlValue is null)
        {
            throw new InvalidOperationException(
                "Die MacDesktop-Paperless-Verbindung ist im macOS-Schlüsselbund unvollständig und wird aus Sicherheitsgründen nicht automatisch überschrieben.");
        }

        var baseUrl = baseUrlValue is null
            ? string.Empty
            : NormalizeBaseUrl(
                baseUrlValue,
                "Die MacDesktop-Paperless-Basisadresse im macOS-Schlüsselbund ist ungültig.");

        return new MacDesktopConnectionInfo(
            username,
            baseUrl,
            snapshot.Exists && baseUrlValue is not null);
    }

    public async Task<PaperlessConnectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsMacDesktop)
        {
            return await GetMacDesktopStatusAsync(
                GetCurrentMacDesktopUsername(),
                cancellationToken);
        }

        var user = GetCurrentLocalUser();
        _ = await GetCredentialAsync(user, cancellationToken);

        return new PaperlessConnectionStatus(true);
    }

    public async Task<PreparedPaperlessToken> PrepareAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (IsMacDesktop)
        {
            var username = GetCurrentMacDesktopUsername();
            var normalizedToken = ValidateTokenInput(token);
            var baseUrl = await GetMacDesktopBaseUrlAsync(
                cancellationToken);
            var snapshot = await ReadMacDesktopUserSnapshotAsync(
                username,
                cancellationToken);

            if (!snapshot.Exists)
            {
                throw new InvalidOperationException(
                    "Eine noch nicht eingerichtete MacDesktop-Verbindung muss vollständig mit Basisadresse, Benutzername und API-Token angelegt werden.");
            }

            var identity = await GetCurrentUserIdentityAsync(
                baseUrl,
                normalizedToken,
                cancellationToken);

            if (identity.Id != snapshot.PaperlessUserId)
            {
                throw new InvalidOperationException(
                    PaperlessUserIdMismatchMessage);
            }

            if (!string.Equals(
                    identity.Username,
                    username,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    UsernameTokenMismatchMessage);
            }

            return new PreparedPaperlessToken(
                _protector.Protect(normalizedToken));
        }

        _ = GetCurrentLocalUser();

        var developmentToken = ValidateTokenInput(token);
        var developmentBaseUrl = await GetBaseUrlAsync(cancellationToken);

        await ValidateConnectionAsync(
            developmentBaseUrl,
            developmentToken,
            cancellationToken);

        return new PreparedPaperlessToken(
            _protector.Protect(developmentToken));
    }

    public async Task SavePreparedAsync(
        PreparedPaperlessToken preparedToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparedToken);

        if (IsMacDesktop)
        {
            var username = GetCurrentMacDesktopUsername();
            var macDesktopToken = UnprotectAndValidate(preparedToken);
            var snapshot = await ReadMacDesktopUserSnapshotAsync(
                username,
                cancellationToken);

            if (!snapshot.Exists)
            {
                throw new InvalidOperationException(
                    "Eine noch nicht eingerichtete MacDesktop-Verbindung muss vollständig mit Basisadresse, Benutzername und API-Token angelegt werden.");
            }

            var baseUrl = await GetMacDesktopBaseUrlAsync(
                cancellationToken);
            var identity = await GetCurrentUserIdentityAsync(
                baseUrl,
                macDesktopToken,
                cancellationToken);

            if (identity.Id != snapshot.PaperlessUserId)
            {
                throw new InvalidOperationException(
                    PaperlessUserIdMismatchMessage);
            }

            if (!string.Equals(
                    identity.Username,
                    username,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    UsernameTokenMismatchMessage);
            }

            var preparedConnection = ProtectMacDesktopPayload(
                new MacDesktopPreparedPayload(
                    username,
                    username,
                    baseUrl,
                    macDesktopToken,
                    snapshot.PaperlessUserId!.Value));

            await SavePreparedMacDesktopConnectionAsync(
                preparedConnection,
                cancellationToken);
            return;
        }

        var user = GetCurrentLocalUser();
        var normalizedToken = UnprotectAndValidate(preparedToken);
        var paperlessUserId = await GetPaperlessUserIdAsync(
            user,
            cancellationToken);
        var tokenAccount = GetTokenAccount(paperlessUserId);

        var previousToken = await ReadSecretAsync(
            TokenService,
            tokenAccount,
            $"Für {user.DisplayName} ist kein persönlicher Paperless-Zugang im macOS-Schlüsselbund verfügbar.",
            cancellationToken);
        var previousFingerprint = await ReadSecretAsync(
            TokenFingerprintService,
            user.KeychainAccount,
            $"Für {user.DisplayName} fehlt die lokale Token-Zuordnungsprüfung im macOS-Schlüsselbund.",
            cancellationToken);

        ValidateFingerprint(
            user.DisplayName,
            previousToken,
            previousFingerprint);

        var newFingerprint = CreateSha256Fingerprint(normalizedToken);
        var tokenChanged = false;
        var fingerprintChanged = false;

        try
        {
            WriteExistingSecret(
                TokenService,
                tokenAccount,
                normalizedToken);
            tokenChanged = true;

            WriteExistingSecret(
                TokenFingerprintService,
                user.KeychainAccount,
                newFingerprint);
            fingerprintChanged = true;

            var verifiedCredential = await GetCredentialAsync(
                user,
                cancellationToken);

            if (!string.Equals(
                    verifiedCredential.Token,
                    normalizedToken,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Der neue lokale Paperless-Zugang konnte nach dem Speichern nicht bestätigt werden.");
            }
        }
        catch (Exception exception)
        {
            try
            {
                if (tokenChanged)
                {
                    WriteExistingSecret(
                        TokenService,
                        tokenAccount,
                        previousToken);
                }

                if (fingerprintChanged)
                {
                    WriteExistingSecret(
                        TokenFingerprintService,
                        user.KeychainAccount,
                        previousFingerprint);
                }

                _ = await GetCredentialAsync(
                    user,
                    CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Die lokale Paperless-Verbindung konnte nicht sicher gespeichert und der vorherige Schlüsselbundzustand nicht vollständig bestätigt werden.",
                    new AggregateException(exception, rollbackException));
            }

            throw;
        }
    }

    public async Task<PreparedMacDesktopConnection> PrepareMacDesktopConnectionAsync(
        string username,
        string baseUrl,
        string? token,
        CancellationToken cancellationToken = default)
    {
        EnsureMacDesktop();

        var currentUsername = GetCurrentMacDesktopUsername();
        var normalizedUsername = NormalizeMacDesktopUsername(username);
        var normalizedBaseUrl = NormalizeBaseUrl(
            baseUrl,
            "Bitte geben Sie eine gültige Paperless-Basisadresse ein.");
        var currentSnapshot = await ReadMacDesktopUserSnapshotAsync(
            currentUsername,
            cancellationToken);

        string normalizedToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            if (!currentSnapshot.Exists)
            {
                throw new InvalidOperationException(
                    "Bei der ersten MacDesktop-Einrichtung muss das API-Token zweimal eingegeben werden.");
            }

            normalizedToken = currentSnapshot.Token!;
        }
        else
        {
            normalizedToken = ValidateTokenInput(token);
        }

        var identity = await GetCurrentUserIdentityAsync(
            normalizedBaseUrl,
            normalizedToken,
            cancellationToken);

        if (!string.Equals(
                identity.Username,
                normalizedUsername,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                UsernameTokenMismatchMessage);
        }

        if (currentSnapshot.Exists &&
            identity.Id != currentSnapshot.PaperlessUserId)
        {
            throw new InvalidOperationException(
                PaperlessUserIdMismatchMessage);
        }

        if (!string.Equals(
                currentUsername,
                normalizedUsername,
                StringComparison.Ordinal))
        {
            var targetSnapshot = await ReadMacDesktopUserSnapshotAsync(
                normalizedUsername,
                cancellationToken);

            if (targetSnapshot.Exists)
            {
                throw new InvalidOperationException(
                    "Für den neuen Paperless-Benutzernamen existiert bereits eine MacDesktop-Verbindung.");
            }
        }

        if (!currentSnapshot.Exists)
        {
            var existingToken = await ReadOptionalSecretAsync(
                MacDesktopTokenService,
                GetTokenAccount(identity.Id),
                cancellationToken);

            if (existingToken is not null)
            {
                throw new InvalidOperationException(
                    "Für diese Paperless-Benutzer-ID existiert bereits eine lokale MacDesktop-Zuordnung. Bitte melden Sie sich mit dem bisher verwendeten Paperless-Benutzernamen an.");
            }
        }

        return ProtectMacDesktopPayload(
            new MacDesktopPreparedPayload(
                currentUsername,
                normalizedUsername,
                normalizedBaseUrl,
                normalizedToken,
                identity.Id));
    }

    public async Task SavePreparedMacDesktopConnectionAsync(
        PreparedMacDesktopConnection preparedConnection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparedConnection);
        EnsureMacDesktop();

        var payload = UnprotectMacDesktopPayload(preparedConnection);
        var currentSessionUsername = GetCurrentMacDesktopUsername();

        if (!string.Equals(
                payload.CurrentUsername,
                currentSessionUsername,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die vorbereitete MacDesktop-Verbindung gehört nicht mehr zur aktuellen Anmeldung.");
        }

        var currentSnapshot = await ReadMacDesktopUserSnapshotAsync(
            payload.CurrentUsername,
            cancellationToken);
        var targetSnapshot = string.Equals(
                payload.CurrentUsername,
                payload.NewUsername,
                StringComparison.Ordinal)
            ? currentSnapshot
            : await ReadMacDesktopUserSnapshotAsync(
                payload.NewUsername,
                cancellationToken);

        if (currentSnapshot.Exists &&
            currentSnapshot.PaperlessUserId != payload.PaperlessUserId)
        {
            throw new InvalidOperationException(
                PaperlessUserIdMismatchMessage);
        }

        if (!string.Equals(
                payload.CurrentUsername,
                payload.NewUsername,
                StringComparison.Ordinal) &&
            targetSnapshot.Exists)
        {
            throw new InvalidOperationException(
                "Für den neuen Paperless-Benutzernamen existiert bereits eine MacDesktop-Verbindung.");
        }

        var previousBaseUrl = await ReadOptionalSecretAsync(
            MacDesktopBaseUrlService,
            BaseUrlAccount,
            cancellationToken);
        var tokenAccount = GetTokenAccount(payload.PaperlessUserId);
        var previousToken = await ReadOptionalSecretAsync(
            MacDesktopTokenService,
            tokenAccount,
            cancellationToken);
        var fingerprint = CreateSha256Fingerprint(payload.Token);

        try
        {
            UpsertSecret(
                MacDesktopBaseUrlService,
                BaseUrlAccount,
                payload.BaseUrl);
            UpsertSecret(
                MacDesktopPaperlessUserIdService,
                payload.NewUsername,
                payload.PaperlessUserId.ToString(CultureInfo.InvariantCulture));

            if (!string.Equals(
                    previousToken,
                    payload.Token,
                    StringComparison.Ordinal))
            {
                UpsertSecret(
                    MacDesktopTokenService,
                    tokenAccount,
                    payload.Token);
            }

            UpsertSecret(
                MacDesktopTokenFingerprintService,
                payload.NewUsername,
                fingerprint);

            await ValidateMacDesktopSavedStateAsync(
                payload.NewUsername,
                payload.BaseUrl,
                payload.PaperlessUserId,
                payload.Token,
                cancellationToken);

            if (!string.Equals(
                    payload.CurrentUsername,
                    payload.NewUsername,
                    StringComparison.Ordinal) &&
                currentSnapshot.Exists)
            {
                DeleteMacDesktopUserBinding(payload.CurrentUsername);

                var removed = await ReadMacDesktopUserBindingAsync(
                    payload.CurrentUsername,
                    cancellationToken);
                if (removed.Exists)
                {
                    throw new InvalidOperationException(
                        "Die alte MacDesktop-Benutzerzuordnung konnte nach der Umbenennung nicht vollständig entfernt werden.");
                }
            }
        }
        catch (Exception exception)
        {
            try
            {
                RestoreOptionalSecret(
                    MacDesktopBaseUrlService,
                    BaseUrlAccount,
                    previousBaseUrl);
                RestoreOptionalSecret(
                    MacDesktopTokenService,
                    tokenAccount,
                    previousToken);
                RestoreMacDesktopUserBinding(
                    payload.CurrentUsername,
                    currentSnapshot);

                if (!string.Equals(
                        payload.CurrentUsername,
                        payload.NewUsername,
                        StringComparison.Ordinal))
                {
                    RestoreMacDesktopUserBinding(
                        payload.NewUsername,
                        targetSnapshot);
                }
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Die MacDesktop-Paperless-Verbindung konnte nicht sicher gespeichert und der vorherige Schlüsselbundzustand nicht vollständig wiederhergestellt werden.",
                    new AggregateException(exception, rollbackException));
            }

            throw;
        }
    }

    public static string NormalizeMacDesktopUsername(string username)
    {
        var normalized = username?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Contains('\r') ||
            normalized.Contains('\n') ||
            normalized.Length > 256)
        {
            throw new InvalidOperationException(
                "Bitte geben Sie einen gültigen einzeiligen Paperless-Benutzernamen ein.");
        }

        return normalized;
    }

    public static string CreateMacDesktopTechnicalUserKey(int paperlessUserId)
    {
        if (paperlessUserId <= 0)
        {
            throw new InvalidOperationException(
                "Die Paperless-Benutzer-ID für die MacDesktop-Sitzung ist ungültig.");
        }

        var input = Encoding.UTF8.GetBytes(
            $"webui/macdesktop-paperless-user-id/{paperlessUserId}");

        return Convert.ToHexString(SHA256.HashData(input))
            .ToLowerInvariant();
    }

    public static string CreateMacDesktopProvisioningTechnicalUserKey(string username)
    {
        var normalizedUsername = NormalizeMacDesktopUsername(username);
        var input = Encoding.UTF8.GetBytes(
            $"webui/macdesktop-provisioning-username/{normalizedUsername}");

        return Convert.ToHexString(SHA256.HashData(input))
            .ToLowerInvariant();
    }

    private LocalTestUserDefinition GetCurrentLocalUser()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "Für die lokale Tokenverwaltung ist eine Testanmeldung erforderlich.");
        }

        var alias = principal.FindFirstValue(
            LocalTestUserSettings.AliasClaimType);

        if (!LocalTestUserSettings.TryGetUser(
                alias,
                out var user))
        {
            throw new InvalidOperationException(
                "Die lokale Testanmeldung enthält keine gültige Testidentität.");
        }

        return user;
    }

    private string GetCurrentMacDesktopUsername()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "Für die MacDesktop-Verbindungsverwaltung ist eine lokale Anmeldung erforderlich.");
        }

        return NormalizeMacDesktopUsername(
            principal.FindFirstValue(LocalTestUserSettings.AliasClaimType) ??
            string.Empty);
    }

    private async Task<int> GetPaperlessUserIdAsync(
        LocalTestUserDefinition user,
        CancellationToken cancellationToken)
    {
        var paperlessUserIdValue = await ReadSecretAsync(
            PaperlessUserIdService,
            user.KeychainAccount,
            $"Für {user.DisplayName} fehlt die lokale Paperless-Benutzerzuordnung im macOS-Schlüsselbund.",
            cancellationToken);

        if (!int.TryParse(
                paperlessUserIdValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var paperlessUserId) ||
            paperlessUserId <= 0)
        {
            throw new InvalidOperationException(
                $"Die lokale Paperless-Benutzerzuordnung für {user.DisplayName} ist ungültig.");
        }

        return paperlessUserId;
    }

    private async Task<MacDesktopUserSnapshot> ReadMacDesktopUserSnapshotAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var binding = await ReadMacDesktopUserBindingAsync(
            username,
            cancellationToken);

        if (!binding.Exists)
        {
            return MacDesktopUserSnapshot.Empty;
        }

        var token = await ReadOptionalSecretAsync(
            MacDesktopTokenService,
            GetTokenAccount(binding.PaperlessUserId!.Value),
            cancellationToken);

        if (token is null)
        {
            throw new InvalidOperationException(
                "Die MacDesktop-Paperless-Verbindung ist im macOS-Schlüsselbund unvollständig und wird aus Sicherheitsgründen nicht automatisch überschrieben.");
        }

        ValidateFingerprint(
            "den MacDesktop-Benutzer",
            token,
            binding.Fingerprint!);

        return new MacDesktopUserSnapshot(
            true,
            binding.PaperlessUserId,
            token,
            binding.Fingerprint);
    }

    private static async Task<MacDesktopUserBindingSnapshot> ReadMacDesktopUserBindingAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var paperlessUserIdValue = await ReadOptionalSecretAsync(
            MacDesktopPaperlessUserIdService,
            username,
            cancellationToken);
        var fingerprint = await ReadOptionalSecretAsync(
            MacDesktopTokenFingerprintService,
            username,
            cancellationToken);

        if (paperlessUserIdValue is null && fingerprint is null)
        {
            return MacDesktopUserBindingSnapshot.Empty;
        }

        if (paperlessUserIdValue is null || fingerprint is null ||
            !int.TryParse(
                paperlessUserIdValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var paperlessUserId) ||
            paperlessUserId <= 0)
        {
            throw new InvalidOperationException(
                "Die MacDesktop-Paperless-Verbindung ist im macOS-Schlüsselbund unvollständig und wird aus Sicherheitsgründen nicht automatisch überschrieben.");
        }

        return new MacDesktopUserBindingSnapshot(
            true,
            paperlessUserId,
            fingerprint);
    }

    private static void DeleteMacDesktopUserBinding(string username)
    {
        DeleteSecretIfExists(
            MacDesktopPaperlessUserIdService,
            username);
        DeleteSecretIfExists(
            MacDesktopTokenFingerprintService,
            username);
    }

    private static void RestoreMacDesktopUserBinding(
        string username,
        MacDesktopUserSnapshot snapshot)
    {
        if (snapshot.Exists)
        {
            UpsertSecret(
                MacDesktopPaperlessUserIdService,
                username,
                snapshot.PaperlessUserId!.Value.ToString(CultureInfo.InvariantCulture));
            UpsertSecret(
                MacDesktopTokenFingerprintService,
                username,
                snapshot.Fingerprint!);
        }
        else
        {
            DeleteMacDesktopUserBinding(username);
        }
    }

    private static void RestoreOptionalSecret(
        string service,
        string account,
        string? value)
    {
        if (value is null)
        {
            DeleteSecretIfExists(service, account);
            return;
        }

        UpsertSecret(service, account, value);
    }

    private async Task ValidateMacDesktopSavedStateAsync(
        string username,
        string expectedBaseUrl,
        int expectedPaperlessUserId,
        string expectedToken,
        CancellationToken cancellationToken)
    {
        var baseUrl = await GetMacDesktopBaseUrlAsync(cancellationToken);
        var snapshot = await ReadMacDesktopUserSnapshotAsync(
            username,
            cancellationToken);

        if (!snapshot.Exists ||
            snapshot.PaperlessUserId != expectedPaperlessUserId ||
            !string.Equals(baseUrl, expectedBaseUrl, StringComparison.Ordinal) ||
            !string.Equals(snapshot.Token, expectedToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die neue MacDesktop-Paperless-Verbindung konnte nach dem Speichern nicht vollständig bestätigt werden.");
        }
    }

    private PreparedMacDesktopConnection ProtectMacDesktopPayload(
        MacDesktopPreparedPayload payload) =>
        new(
            _macDesktopProtector.Protect(
                JsonSerializer.Serialize(payload)));

    private MacDesktopPreparedPayload UnprotectMacDesktopPayload(
        PreparedMacDesktopConnection preparedConnection)
    {
        try
        {
            var json = _macDesktopProtector.Unprotect(
                preparedConnection.ProtectedPayload);
            var payload = JsonSerializer.Deserialize<MacDesktopPreparedPayload>(json)
                ?? throw new InvalidOperationException(
                    "Die vorbereitete MacDesktop-Verbindung ist leer.");

            if (payload.PaperlessUserId <= 0)
            {
                throw new InvalidOperationException(
                    "Die vorbereitete MacDesktop-Paperless-Benutzer-ID ist ungültig.");
            }

            return new MacDesktopPreparedPayload(
                NormalizeMacDesktopUsername(payload.CurrentUsername),
                NormalizeMacDesktopUsername(payload.NewUsername),
                NormalizeBaseUrl(
                    payload.BaseUrl,
                    "Die vorbereitete MacDesktop-Basisadresse ist ungültig."),
                ValidateTokenInput(payload.Token),
                payload.PaperlessUserId);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Die vorbereitete MacDesktop-Verbindung ist nicht mehr gültig.",
                exception);
        }
    }

    private string UnprotectAndValidate(
        PreparedPaperlessToken preparedToken)
    {
        string token;

        try
        {
            token = _protector.Unprotect(
                preparedToken.ProtectedToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Die vorbereitete lokale Paperless-Verbindung ist nicht mehr gültig.",
                exception);
        }

        return ValidateTokenInput(token);
    }

    private async Task ValidateConnectionAsync(
        string baseUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var client = new PaperlessApiClient(
            _httpClientFactory.CreateClient(PaperlessClientFactory.HttpClientName),
            baseUrl,
            token);

        await client.ValidateCurrentTokenAsync(cancellationToken);
    }

    private async Task<PaperlessCurrentUserIdentity> GetCurrentUserIdentityAsync(
        string baseUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var client = new PaperlessApiClient(
            _httpClientFactory.CreateClient(PaperlessClientFactory.HttpClientName),
            baseUrl,
            token);

        return await client.GetCurrentUserIdentityAsync(cancellationToken);
    }

    private static string NormalizeBaseUrl(
        string value,
        string errorMessage)
    {
        var normalized = value.Trim().TrimEnd('/');

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static string ValidateTokenInput(string token)
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

    private static string GetTokenAccount(
        int paperlessUserId) =>
        $"paperless-user-id-{paperlessUserId}";

    private static string CreateSha256Fingerprint(
        string token) =>
        Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static void ValidateFingerprint(
        string displayName,
        string token,
        string fingerprintValue)
    {
        if (!TryParseSha256Fingerprint(
                fingerprintValue,
                out var expectedFingerprint))
        {
            throw new InvalidOperationException(
                $"Die lokale Token-Zuordnungsprüfung für {displayName} ist ungültig.");
        }

        var actualFingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        if (!CryptographicOperations.FixedTimeEquals(
                expectedFingerprint,
                actualFingerprint))
        {
            throw new InvalidOperationException(
                $"Die lokale Schlüsselbundzuordnung für {displayName} ist nicht konsistent.");
        }
    }

    private static bool TryParseSha256Fingerprint(
        string value,
        out byte[] fingerprint)
    {
        var normalized = value.Trim();

        if (normalized.Length != 64 ||
            normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            fingerprint = [];
            return false;
        }

        try
        {
            fingerprint = Convert.FromHexString(normalized);
            return fingerprint.Length == 32;
        }
        catch (FormatException)
        {
            fingerprint = [];
            return false;
        }
    }

    private static async Task<string?> ReadOptionalSecretAsync(
        string service,
        string account,
        CancellationToken cancellationToken)
    {
        if (!SecretExists(service, account))
        {
            return null;
        }

        return await ReadSecretAsync(
            service,
            account,
            "Ein erwarteter MacDesktop-Schlüsselbundeintrag konnte nicht gelesen werden.",
            cancellationToken);
    }

    private static async Task<string> ReadSecretAsync(
        string service,
        string account,
        string missingMessage,
        CancellationToken cancellationToken)
    {
        EnsureMacOS();

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("find-generic-password");
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add(account);
        startInfo.ArgumentList.Add("-s");
        startInfo.ArgumentList.Add(service);
        startInfo.ArgumentList.Add("-w");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Der macOS-Schlüsselbund konnte nicht geöffnet werden.");

        var outputTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var value = (await outputTask).Trim();
        _ = await errorTask;

        if (process.ExitCode != 0 ||
            string.IsNullOrWhiteSpace(value) ||
            value.Contains('\r') ||
            value.Contains('\n'))
        {
            throw new InvalidOperationException(missingMessage);
        }

        return value;
    }

    private static bool SecretExists(
        string service,
        string account)
    {
        EnsureMacOS();

        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        IntPtr itemRef = IntPtr.Zero;

        try
        {
            var status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceBytes.Length),
                serviceBytes,
                checked((uint)accountBytes.Length),
                accountBytes,
                IntPtr.Zero,
                IntPtr.Zero,
                out itemRef);

            if (status == ErrSecSuccess)
            {
                return true;
            }

            if (status == ErrSecItemNotFound)
            {
                return false;
            }

            throw new InvalidOperationException(
                "Der macOS-Schlüsselbundzustand konnte nicht sicher bestimmt werden.");
        }
        finally
        {
            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    private static void UpsertSecret(
        string service,
        string account,
        string value)
    {
        EnsureMacOS();

        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        IntPtr itemRef = IntPtr.Zero;

        try
        {
            var findStatus = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceBytes.Length),
                serviceBytes,
                checked((uint)accountBytes.Length),
                accountBytes,
                IntPtr.Zero,
                IntPtr.Zero,
                out itemRef);

            if (findStatus == ErrSecSuccess && itemRef != IntPtr.Zero)
            {
                var updateStatus = SecKeychainItemModifyAttributesAndData(
                    itemRef,
                    IntPtr.Zero,
                    checked((uint)valueBytes.Length),
                    valueBytes);

                if (updateStatus != ErrSecSuccess)
                {
                    throw new InvalidOperationException(
                        "Der vorhandene macOS-Schlüsselbundeintrag konnte nicht sicher ersetzt werden.");
                }

                return;
            }

            if (findStatus != ErrSecItemNotFound)
            {
                throw new InvalidOperationException(
                    "Der macOS-Schlüsselbundeintrag konnte nicht sicher geöffnet werden.");
            }

            var addStatus = SecKeychainAddGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceBytes.Length),
                serviceBytes,
                checked((uint)accountBytes.Length),
                accountBytes,
                checked((uint)valueBytes.Length),
                valueBytes,
                out itemRef);

            if (addStatus != ErrSecSuccess)
            {
                throw new InvalidOperationException(
                    "Der neue macOS-Schlüsselbundeintrag konnte nicht sicher angelegt werden.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(valueBytes);

            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    private static void DeleteSecretIfExists(
        string service,
        string account)
    {
        EnsureMacOS();

        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        IntPtr itemRef = IntPtr.Zero;

        try
        {
            var findStatus = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceBytes.Length),
                serviceBytes,
                checked((uint)accountBytes.Length),
                accountBytes,
                IntPtr.Zero,
                IntPtr.Zero,
                out itemRef);

            if (findStatus == ErrSecItemNotFound)
            {
                return;
            }

            if (findStatus != ErrSecSuccess || itemRef == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Der macOS-Schlüsselbundeintrag konnte zum Entfernen nicht sicher geöffnet werden.");
            }

            if (SecKeychainItemDelete(itemRef) != ErrSecSuccess)
            {
                throw new InvalidOperationException(
                    "Der macOS-Schlüsselbundeintrag konnte nicht sicher entfernt werden.");
            }
        }
        finally
        {
            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    private static void WriteExistingSecret(
        string service,
        string account,
        string value)
    {
        EnsureMacOS();

        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        IntPtr itemRef = IntPtr.Zero;

        try
        {
            var findStatus = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceBytes.Length),
                serviceBytes,
                checked((uint)accountBytes.Length),
                accountBytes,
                IntPtr.Zero,
                IntPtr.Zero,
                out itemRef);

            if (findStatus != ErrSecSuccess ||
                itemRef == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Der vorhandene macOS-Schlüsselbundeintrag konnte nicht zum Ersetzen geöffnet werden.");
            }

            var updateStatus = SecKeychainItemModifyAttributesAndData(
                itemRef,
                IntPtr.Zero,
                checked((uint)valueBytes.Length),
                valueBytes);

            if (updateStatus != ErrSecSuccess)
            {
                throw new InvalidOperationException(
                    "Der vorhandene macOS-Schlüsselbundeintrag konnte nicht sicher ersetzt werden.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(valueBytes);

            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    private void EnsureMacDesktop()
    {
        if (!IsMacDesktop)
        {
            throw new InvalidOperationException(
                "Diese Schlüsselbundoperation ist ausschließlich für MacDesktop vorgesehen.");
        }

        EnsureMacOS();
    }

    private static void EnsureMacOS()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new InvalidOperationException(
                "Der lokale Mehrbenutzerbetrieb ist nur unter macOS verfügbar.");
        }
    }

    private sealed record MacDesktopPreparedPayload(
        string CurrentUsername,
        string NewUsername,
        string BaseUrl,
        string Token,
        int PaperlessUserId);

    private sealed record MacDesktopUserBindingSnapshot(
        bool Exists,
        int? PaperlessUserId,
        string? Fingerprint)
    {
        public static MacDesktopUserBindingSnapshot Empty { get; } =
            new(false, null, null);
    }

    private sealed record MacDesktopUserSnapshot(
        bool Exists,
        int? PaperlessUserId,
        string? Token,
        string? Fingerprint)
    {
        public static MacDesktopUserSnapshot Empty { get; } =
            new(false, null, null, null);
    }

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        ExactSpelling = true)]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        IntPtr passwordLength,
        IntPtr passwordData,
        out IntPtr itemRef);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        ExactSpelling = true)]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemRef);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        ExactSpelling = true)]
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attrList,
        uint length,
        byte[] data);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        ExactSpelling = true)]
    private static extern int SecKeychainItemDelete(
        IntPtr itemRef);

    [DllImport(
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        ExactSpelling = true)]
    private static extern void CFRelease(
        IntPtr cf);
}
