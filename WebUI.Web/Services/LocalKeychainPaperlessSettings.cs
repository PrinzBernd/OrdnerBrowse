using Microsoft.AspNetCore.DataProtection;
using WebUI.Infrastructure;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace WebUI.Web.Services;

public sealed record LocalTestUserCredential(
    string Token,
    string TechnicalUserKey);

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
    private const string ProtectorPurpose =
        "webui.local-paperless-token-change.v1";
    private const int ErrSecSuccess = 0;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;

    public LocalKeychainPaperlessSettings(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public async Task<string> GetBaseUrlAsync(
        CancellationToken cancellationToken = default)
    {
        var value = await ReadSecretAsync(
            BaseUrlService,
            BaseUrlAccount,
            "Die lokale Paperless-Basisadresse ist im macOS-Schlüsselbund nicht verfügbar.",
            cancellationToken);

        var normalized = value.Trim().TrimEnd('/');

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "Die lokale Paperless-Basisadresse im macOS-Schlüsselbund ist ungültig.");
        }

        return normalized;
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
            user,
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

    public async Task<PaperlessConnectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var user = GetCurrentLocalUser();
        _ = await GetCredentialAsync(user, cancellationToken);

        return new PaperlessConnectionStatus(true);
    }

    public async Task<PreparedPaperlessToken> PrepareAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        _ = GetCurrentLocalUser();

        var normalizedToken = ValidateTokenInput(token);
        var baseUrl = await GetBaseUrlAsync(cancellationToken);
        var client = new PaperlessApiClient(
            _httpClientFactory.CreateClient(PaperlessClientFactory.HttpClientName),
            baseUrl,
            normalizedToken);

        await client.ValidateCurrentTokenAsync(cancellationToken);

        return new PreparedPaperlessToken(
            _protector.Protect(normalizedToken));
    }

    public async Task SavePreparedAsync(
        PreparedPaperlessToken preparedToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparedToken);

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
            user,
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
        LocalTestUserDefinition user,
        string token,
        string fingerprintValue)
    {
        if (!TryParseSha256Fingerprint(
                fingerprintValue,
                out var expectedFingerprint))
        {
            throw new InvalidOperationException(
                $"Die lokale Token-Zuordnungsprüfung für {user.DisplayName} ist ungültig.");
        }

        var actualFingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        if (!CryptographicOperations.FixedTimeEquals(
                expectedFingerprint,
                actualFingerprint))
        {
            throw new InvalidOperationException(
                $"Die lokale Schlüsselbundzuordnung für {user.DisplayName} ist nicht konsistent.");
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

    private static void EnsureMacOS()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new InvalidOperationException(
                "Der lokale Mehrbenutzertest ist nur unter macOS verfügbar.");
        }
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
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attrList,
        uint length,
        byte[] data);

    [DllImport(
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        ExactSpelling = true)]
    private static extern void CFRelease(
        IntPtr cf);
}
