using Microsoft.AspNetCore.Http;

namespace WebUI.Web.Services;

public sealed record OidcAuthenticationSettings(
    bool Enabled,
    string Authority,
    string MetadataAddress,
    string ClientId,
    string ClientSecret,
    PathString CallbackPath,
    PathString SignedOutCallbackPath)
{
    public const string SectionName = "Authentication:Oidc";
    public const string TechnicalUserKeyClaimType = "webui/technical-user-key";
    public const string SessionIdClaimType = "webui/session-id";

    public static OidcAuthenticationSettings Load(
        IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration[$"{SectionName}:ClientSecret"]))
        {
            throw new InvalidOperationException(
                "Ein direkter OIDC-Client-Secretwert in der Konfiguration ist unzulässig; erforderlich ist ausschließlich eine Secretdatei.");
        }

        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var enabled = section.GetValue<bool>("Enabled");

        if (!enabled)
        {
            return new OidcAuthenticationSettings(
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new PathString("/signin-oidc"),
                new PathString("/signout-callback-oidc"));
        }

        var authority = ConfigurationFileValueReader
            .ReadRequiredValueOrFile(
                section,
                "Authority",
                "AuthorityFilePath",
                "OIDC-Authority");

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
            !string.Equals(
                authorityUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(authorityUri.UserInfo) ||
            !string.IsNullOrEmpty(authorityUri.Query) ||
            !string.IsNullOrEmpty(authorityUri.Fragment))
        {
            throw new InvalidOperationException(
                "Die OIDC-Authority muss eine absolute HTTPS-Adresse ohne Benutzerinformationen, Abfrage oder Fragment sein.");
        }

        var metadataAddress = ConfigurationFileValueReader
            .ReadRequiredValueOrFile(
                section,
                "MetadataAddress",
                "MetadataAddressFilePath",
                "OIDC-Metadatenadresse");

        if (!Uri.TryCreate(
                metadataAddress,
                UriKind.Absolute,
                out var metadataAddressUri) ||
            !string.Equals(
                metadataAddressUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(metadataAddressUri.UserInfo) ||
            !string.IsNullOrEmpty(metadataAddressUri.Query) ||
            !string.IsNullOrEmpty(metadataAddressUri.Fragment))
        {
            throw new InvalidOperationException(
                "Die OIDC-Metadatenadresse muss eine absolute HTTPS-Adresse ohne Benutzerinformationen, Abfrage oder Fragment sein.");
        }

        var clientId = ConfigurationFileValueReader
            .ReadRequiredValueOrFile(
                section,
                "ClientId",
                "ClientIdFilePath",
                "OIDC-Client-ID");
        var clientSecretFilePath = RequireValue(
            section,
            "ClientSecretFilePath");

        if (!Path.IsPathFullyQualified(clientSecretFilePath))
        {
            throw new InvalidOperationException(
                "Der OIDC-Client-Secret-Pfad muss absolut sein.");
        }

        var fullClientSecretFilePath = Path.GetFullPath(
            clientSecretFilePath);

        if (!File.Exists(fullClientSecretFilePath))
        {
            throw new InvalidOperationException(
                "Die konfigurierte OIDC-Client-Secret-Datei wurde nicht gefunden.");
        }

        var clientSecret = File.ReadAllText(
            fullClientSecretFilePath)
            .TrimEnd('\r', '\n');

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Die konfigurierte OIDC-Client-Secret-Datei ist leer.");
        }

        return new OidcAuthenticationSettings(
            true,
            authority.TrimEnd('/'),
            metadataAddress,
            clientId,
            clientSecret,
            ReadPath(section, "CallbackPath", "/signin-oidc"),
            ReadPath(
                section,
                "SignedOutCallbackPath",
                "/signout-callback-oidc"));
    }

    private static string RequireValue(
        IConfiguration section,
        string key)
    {
        var value = section[key]?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Die OIDC-Einstellung '{key}' fehlt.");
        }

        return value;
    }

    private static PathString ReadPath(
        IConfiguration section,
        string key,
        string defaultValue)
    {
        var value = section[key]?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            value = defaultValue;
        }

        if (!value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains('?') ||
            value.Contains('#'))
        {
            throw new InvalidOperationException(
                $"Die OIDC-Einstellung '{key}' muss ein lokaler Pfad ohne Abfrage oder Fragment sein.");
        }

        return new PathString(value);
    }
}
