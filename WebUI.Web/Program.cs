using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebUI.Web.Components;
using WebUI.Web.Services;

if (args.Length == 1 &&
    string.Equals(
        args[0],
        "--proxy-guard-healthcheck",
        StringComparison.Ordinal))
{
    Environment.ExitCode =
        await ProxyGuardHealthCheckRunner.RunAsync();
    return;
}

const string OidcDiagnosticOriginProperty =
    ".webui.oidc-diag-origin";
const string OidcDiagnosticInitialPkceProperty =
    ".webui.oidc-diag-initial-pkce";
const string OidcDiagnosticTransactionProperty =
    ".webui.oidc-diag-transaction";
const string OidcDiagnosticStateVerifierDigestItem =
    "webui.oidc-diag-state-verifier-digest";
const string OidcDiagnosticVerifierMatchItem =
    "webui.oidc-diag-verifier-match";
const string OidcDiagnosticCodeMatchItem =
    "webui.oidc-diag-code-match";

var builder =
    WebApplication.CreateBuilder(args);

var performanceDiagnosticsLogDirectory =
    builder.Configuration["WebUi:PerformanceDiagnostics:LogDirectory"]?.Trim();
var performanceDiagnosticsRetentionDays =
    builder.Configuration.GetValue<int?>("WebUi:PerformanceDiagnostics:RetentionDays") ?? 7;
WebUI.Infrastructure.PerformanceDiagnosticsLog.Configure(
    performanceDiagnosticsLogDirectory,
    performanceDiagnosticsRetentionDays,
    ApplicationDisplayInfo.FullVersion);

var oidcDiagnosticsFileSettings =
    OidcDiagnosticsFileLoggerSettings.Load(
        builder.Configuration);

if (oidcDiagnosticsFileSettings.Enabled)
{
    builder.Logging.AddProvider(
        new OidcDiagnosticsFileLoggerProvider(
            oidcDiagnosticsFileSettings));
}

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

ForwardedHeadersProxyTrust.ValidateConfiguration(
    builder.Configuration);

var oidcSettings =
    OidcAuthenticationSettings.Load(builder.Configuration);
var proxyGuardSettings =
    ProxyGuardSettings.Load(
        builder.Configuration);
var runtimeProfile =
    builder.Configuration["WebUi:RuntimeProfile"]?.Trim();
var isProfileB =
    string.Equals(
        runtimeProfile,
        "ProfileB",
        StringComparison.Ordinal);
var isDiskStationTest =
    string.Equals(
        runtimeProfile,
        ApplicationDisplayInfo.DiskStationTestProfileName,
        StringComparison.Ordinal);
var localMultiUserEnabled =
    LocalTestUserSettings.IsEnabled(
        builder.Environment,
        builder.Configuration,
        oidcSettings.Enabled);

if (localMultiUserEnabled)
{
    builder.WebHost.UseUrls(
        LocalTestModeSecurity.GetRequiredLoopbackUrl(
            builder.Configuration));
}

builder.Services.AddSingleton(oidcSettings);
builder.Services.AddSingleton(proxyGuardSettings);
builder.Services.AddSingleton<OidcChallengeGuard>();

IPAddress? forwardedHeadersKnownProxy = null;

if (!localMultiUserEnabled)
{
    var knownProxy =
        ForwardedHeadersProxyTrust.LoadRequiredDockerGateway();
    forwardedHeadersKnownProxy = knownProxy;

    builder.Services.Configure<ForwardedHeadersOptions>(
        options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownProxies.Add(
                knownProxy);
        });
}

if (oidcSettings.Enabled)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme =
                CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme =
                OidcChallengeAuthenticationHandler.SchemeName;
            options.DefaultSignOutScheme =
                CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name =
                "__Host-webui";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy =
                CookieSecurePolicy.Always;
            options.Cookie.SameSite =
                SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan =
                TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        })
        .AddScheme<
            AuthenticationSchemeOptions,
            OidcChallengeAuthenticationHandler>(
                OidcChallengeAuthenticationHandler.SchemeName,
                _ =>
                {
                })
        .AddOpenIdConnect(options =>
        {
            options.Authority =
                oidcSettings.Authority;
            options.MetadataAddress =
                oidcSettings.MetadataAddress;
            options.ClientId =
                oidcSettings.ClientId;
            options.ClientSecret =
                oidcSettings.ClientSecret;
            options.SignInScheme =
                CookieAuthenticationDefaults.AuthenticationScheme;
            options.ResponseType =
                OpenIdConnectResponseType.Code;
            options.ResponseMode =
                OpenIdConnectResponseMode.Query;
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.CallbackPath =
                oidcSettings.CallbackPath;
            options.SignedOutCallbackPath =
                oidcSettings.SignedOutCallbackPath;

            options.Scope.Clear();
            options.Scope.Add("openid");

            options.TokenValidationParameters.NameClaimType =
                "username";
            options.TokenValidationParameters.RoleClaimType =
                "groups";

            options.Events.OnRedirectToIdentityProvider = context =>
            {
                var origin =
                    string.Equals(
                        context.Request.Path.Value,
                        "/auth/login",
                        StringComparison.Ordinal)
                        ? "EXPLICIT_LOGIN"
                        : "GESCHUETZTER_ENDPOINT";

                context.Properties.Items[OidcDiagnosticOriginProperty] =
                    origin;

                var transactionId =
                    ("TX-" + Guid.NewGuid().ToString("N")[..8])
                        .ToUpperInvariant();
                context.Properties.Items[OidcDiagnosticTransactionProperty] =
                    transactionId;

                var hasVerifier =
                    context.Properties.Items.TryGetValue(
                        OAuthConstants.CodeVerifierKey,
                        out var codeVerifier) &&
                    !string.IsNullOrWhiteSpace(codeVerifier);
                var codeChallenge =
                    context.ProtocolMessage.GetParameter(
                        OAuthConstants.CodeChallengeKey);
                var hasChallenge =
                    !string.IsNullOrWhiteSpace(codeChallenge);
                var challengeMethod =
                    context.ProtocolMessage.GetParameter(
                        OAuthConstants.CodeChallengeMethodKey);
                var usesS256 =
                    string.Equals(
                        challengeMethod,
                        OAuthConstants.CodeChallengeMethodS256,
                        StringComparison.Ordinal);
                var initialPkceConsistent =
                    "NICHT_PRUEFBAR";

                if (hasVerifier &&
                    hasChallenge &&
                    usesS256)
                {
                    var challengeBytes =
                        SHA256.HashData(
                            Encoding.UTF8.GetBytes(codeVerifier!));
                    var calculatedChallenge =
                        WebEncoders.Base64UrlEncode(challengeBytes);

                    initialPkceConsistent =
                        ToDiagnosticComparison(
                            calculatedChallenge,
                            codeChallenge);
                }

                context.Properties.Items[OidcDiagnosticInitialPkceProperty] =
                    initialPkceConsistent;

                var logger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(OidcDiagnosticsFileLoggerProvider.LoggerCategory);

                logger.LogInformation(
                    "OIDC-DIAG Challenge; Transaktion: {TransactionId}; Ursprung: {Origin}; Verifier vorhanden: {HasVerifier}; Challenge vorhanden: {HasChallenge}; S256: {UsesS256}; PKCE intern konsistent: {InitialPkceConsistent}",
                    transactionId,
                    origin,
                    ToDiagnosticFlag(hasVerifier),
                    ToDiagnosticFlag(hasChallenge),
                    ToDiagnosticFlag(usesS256),
                    initialPkceConsistent);

                return Task.CompletedTask;
            };

            options.Events.OnMessageReceived = context =>
            {
                var properties =
                    context.Properties;
                var stateRestored =
                    properties is not null;
                var origin =
                    GetDiagnosticProperty(
                        properties,
                        OidcDiagnosticOriginProperty);
                var transactionId =
                    GetDiagnosticProperty(
                        properties,
                        OidcDiagnosticTransactionProperty);
                var initialPkceConsistent =
                    GetDiagnosticProperty(
                        properties,
                        OidcDiagnosticInitialPkceProperty);
                string? stateVerifier = null;
                var hasVerifier =
                    properties is not null &&
                    properties.Items.TryGetValue(
                        OAuthConstants.CodeVerifierKey,
                        out stateVerifier) &&
                    !string.IsNullOrWhiteSpace(stateVerifier);
                var hasAuthorizationCode =
                    !string.IsNullOrWhiteSpace(
                        context.ProtocolMessage.Code);

                if (hasVerifier)
                {
                    context.HttpContext.Items[OidcDiagnosticStateVerifierDigestItem] =
                        CreateDiagnosticDigest(stateVerifier!);
                }

                var logger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(OidcDiagnosticsFileLoggerProvider.LoggerCategory);

                logger.LogInformation(
                    "OIDC-DIAG Callback; Transaktion: {TransactionId}; Ursprung: {Origin}; State wiederhergestellt: {StateRestored}; Verifier im State vorhanden: {HasVerifier}; Autorisierungscode vorhanden: {HasAuthorizationCode}; Initiale PKCE-Konsistenz: {InitialPkceConsistent}",
                    transactionId,
                    origin,
                    ToDiagnosticFlag(stateRestored),
                    ToDiagnosticFlag(hasVerifier),
                    ToDiagnosticFlag(hasAuthorizationCode),
                    initialPkceConsistent);

                return Task.CompletedTask;
            };

            options.Events.OnAuthorizationCodeReceived = context =>
            {
                var origin =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticOriginProperty);
                var transactionId =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticTransactionProperty);
                var initialPkceConsistent =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticInitialPkceProperty);
                var stateVerifierDigest =
                    GetDiagnosticHttpContextDigest(
                        context.HttpContext,
                        OidcDiagnosticStateVerifierDigestItem);
                var tokenRequestVerifier =
                    context.TokenEndpointRequest?.GetParameter(
                        OAuthConstants.CodeVerifierKey);
                var tokenRequestCode =
                    context.TokenEndpointRequest?.Code;
                var verifierMatch =
                    ToDiagnosticDigestComparison(
                        stateVerifierDigest,
                        tokenRequestVerifier);
                var codeMatch =
                    ToDiagnosticComparison(
                        context.ProtocolMessage.Code,
                        tokenRequestCode);

                context.HttpContext.Items[OidcDiagnosticVerifierMatchItem] =
                    verifierMatch;
                context.HttpContext.Items[OidcDiagnosticCodeMatchItem] =
                    codeMatch;
                context.HttpContext.Items.Remove(
                    OidcDiagnosticStateVerifierDigestItem);

                var logger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(OidcDiagnosticsFileLoggerProvider.LoggerCategory);

                logger.LogInformation(
                    "OIDC-DIAG TokenRequest; Transaktion: {TransactionId}; Ursprung: {Origin}; Initiale PKCE-Konsistenz: {InitialPkceConsistent}; State-Verifier zu TokenRequest: {VerifierMatch}; Callback-Code zu TokenRequest: {CodeMatch}; TokenRequest-Verifier vorhanden: {TokenRequestVerifierPresent}; TokenRequest-Code vorhanden: {TokenRequestCodePresent}",
                    transactionId,
                    origin,
                    initialPkceConsistent,
                    verifierMatch,
                    codeMatch,
                    ToDiagnosticFlag(
                        !string.IsNullOrWhiteSpace(tokenRequestVerifier)),
                    ToDiagnosticFlag(
                        !string.IsNullOrWhiteSpace(tokenRequestCode)));

                return Task.CompletedTask;
            };

            options.Events.OnTokenResponseReceived = context =>
            {
                var origin =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticOriginProperty);
                var transactionId =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticTransactionProperty);
                var initialPkceConsistent =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticInitialPkceProperty);
                var verifierMatch =
                    GetDiagnosticHttpContextClassification(
                        context.HttpContext,
                        OidcDiagnosticVerifierMatchItem);
                var codeMatch =
                    GetDiagnosticHttpContextClassification(
                        context.HttpContext,
                        OidcDiagnosticCodeMatchItem);
                var logger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(OidcDiagnosticsFileLoggerProvider.LoggerCategory);

                logger.LogInformation(
                    "OIDC-DIAG TokenResponse; Transaktion: {TransactionId}; Ursprung: {Origin}; Initiale PKCE-Konsistenz: {InitialPkceConsistent}; State-Verifier zu TokenRequest: {VerifierMatch}; Callback-Code zu TokenRequest: {CodeMatch}; Tokenantwort erreicht: JA",
                    transactionId,
                    origin,
                    initialPkceConsistent,
                    verifierMatch,
                    codeMatch);

                return Task.CompletedTask;
            };

            options.Events.OnTokenValidated = context =>
            {
                var challengeGuard =
                    context.HttpContext.RequestServices
                        .GetRequiredService<OidcChallengeGuard>();

                if (challengeGuard.Release(
                        context.HttpContext.Request))
                {
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(
                            OidcDiagnosticsFileLoggerProvider.LoggerCategory)
                        .LogInformation(
                            "OIDC-DIAG ChallengeGuardReleased; Grund: TokenValidated");
                }

                if (context.Principal?.Identity is not ClaimsIdentity identity)
                {
                    context.Fail(
                        "Die OIDC-Identitaet konnte nicht verarbeitet werden.");
                    return Task.CompletedTask;
                }

                var issuer =
                    context.SecurityToken?.Issuer;
                var subject =
                    context.Principal.FindFirst("sub")?.Value;

                if (string.IsNullOrWhiteSpace(issuer) ||
                    string.IsNullOrWhiteSpace(subject))
                {
                    context.Fail(
                        "Die OIDC-Antwort enthaelt keine dauerhafte Benutzerkennung.");
                    return Task.CompletedTask;
                }

                var technicalUserKey =
                    CreateTechnicalUserKey(issuer, subject);

                identity.AddClaim(
                    new Claim(
                        OidcAuthenticationSettings.TechnicalUserKeyClaimType,
                        technicalUserKey));

                identity.AddClaim(
                    new Claim(
                        OidcAuthenticationSettings.SessionIdClaimType,
                        Guid.NewGuid().ToString("N")));

                return Task.CompletedTask;
            };

            options.Events.OnRemoteFailure = async context =>
            {
                var challengeGuard =
                    context.HttpContext.RequestServices
                        .GetRequiredService<OidcChallengeGuard>();
                var diagnosticLogger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(OidcDiagnosticsFileLoggerProvider.LoggerCategory);
                if (challengeGuard.Release(
                        context.HttpContext.Request))
                {
                    diagnosticLogger.LogInformation(
                        "OIDC-DIAG ChallengeGuardReleased; Grund: RemoteFailure");
                }

                var diagnosticOrigin =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticOriginProperty);
                var diagnosticTransactionId =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticTransactionProperty);
                var diagnosticInitialPkce =
                    GetDiagnosticProperty(
                        context.Properties,
                        OidcDiagnosticInitialPkceProperty);
                var diagnosticVerifierMatch =
                    GetDiagnosticHttpContextClassification(
                        context.HttpContext,
                        OidcDiagnosticVerifierMatchItem);
                var diagnosticCodeMatch =
                    GetDiagnosticHttpContextClassification(
                        context.HttpContext,
                        OidcDiagnosticCodeMatchItem);

                context.HttpContext.Items.Remove(
                    OidcDiagnosticStateVerifierDigestItem);

                diagnosticLogger.LogWarning(
                    "OIDC-DIAG RemoteFailure; Transaktion: {TransactionId}; Ursprung: {Origin}; Initiale PKCE-Konsistenz: {InitialPkceConsistent}; State-Verifier zu TokenRequest: {VerifierMatch}; Callback-Code zu TokenRequest: {CodeMatch}; Tokenantwort erreicht: NEIN",
                    diagnosticTransactionId,
                    diagnosticOrigin,
                    diagnosticInitialPkce,
                    diagnosticVerifierMatch,
                    diagnosticCodeMatch);

                var eventId =
                    $"OIDC-{Guid.NewGuid():N}"[..17]
                        .ToUpperInvariant();
                var category =
                    ClassifyOidcRemoteFailure(
                        context.Failure);
                var exceptionType =
                    GetOidcExceptionType(
                        context.Failure);
                var request = context.HttpContext.Request;
                var cookieNames = request.Cookies.Keys;
                var correlationCookieCount =
                    CountCookiesByPrefix(
                        cookieNames,
                        ".AspNetCore.Correlation.");
                var nonceCookieCount =
                    CountCookiesByPrefix(
                        cookieNames,
                        ".AspNetCore.OpenIdConnect.Nonce.");
                var hasAuthorizationCode =
                    request.Query.ContainsKey("code");
                var hasState =
                    request.Query.ContainsKey("state");
                var hasRemoteError =
                    request.Query.ContainsKey("error");
                var hasForwardedProto =
                    request.Headers.ContainsKey("X-Forwarded-Proto");
                var hasForwardedHost =
                    request.Headers.ContainsKey("X-Forwarded-Host");
                var currentUserAuthenticated =
                    context.HttpContext.User.Identity?.IsAuthenticated == true;
                var localCookieResult =
                    await GetLocalCookieAuthenticationResultAsync(
                        context.HttpContext);
                var responseHasStarted =
                    context.HttpContext.Response.HasStarted;

                diagnosticLogger.LogWarning(
                    "OIDC-Ruecksprung fehlgeschlagen. Transaktion: {TransactionId}; Ereigniskennung: {EventId}; Kategorie: {Category}; Ausnahmetyp: {ExceptionType}; Autorisierungscode vorhanden: {HasAuthorizationCode}; State vorhanden: {HasState}; SSO-Fehlerparameter vorhanden: {HasRemoteError}; Korrelationscookies: {CorrelationCookieCount}; Nonce-Cookies: {NonceCookieCount}; Benutzer im aktuellen HTTP-Kontext authentifiziert: {CurrentUserAuthenticated}; Lokales Cookie-Ergebnis: {LocalCookieStatus}; Lokaler Cookie-Ausnahmetyp: {LocalCookieExceptionType}; HTTPS-Anfrage: {IsHttps}; X-Forwarded-Proto vorhanden: {HasForwardedProto}; X-Forwarded-Host vorhanden: {HasForwardedHost}; Antwort bereits begonnen: {ResponseHasStarted}",
                    diagnosticTransactionId,
                    eventId,
                    category,
                    exceptionType,
                    hasAuthorizationCode,
                    hasState,
                    hasRemoteError,
                    correlationCookieCount,
                    nonceCookieCount,
                    currentUserAuthenticated,
                    localCookieResult.Status,
                    localCookieResult.ExceptionType,
                    request.IsHttps,
                    hasForwardedProto,
                    hasForwardedHost,
                    responseHasStarted);

                context.HandleResponse();
                context.Response.Redirect(
                    CreateOidcErrorRedirectUrl(
                        eventId,
                        category,
                        exceptionType,
                        hasAuthorizationCode,
                        hasState,
                        hasRemoteError,
                        correlationCookieCount,
                        nonceCookieCount,
                        currentUserAuthenticated,
                        localCookieResult,
                        request.IsHttps,
                        hasForwardedProto,
                        hasForwardedHost,
                        responseHasStarted));
            };
        });

    builder.Services.AddAuthorization();
}
else if (localMultiUserEnabled)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme =
                LocalTestUserSettings.AuthenticationScheme;
            options.DefaultChallengeScheme =
                LocalTestUserSettings.AuthenticationScheme;
            options.DefaultSignOutScheme =
                LocalTestUserSettings.AuthenticationScheme;
        })
        .AddCookie(
            LocalTestUserSettings.AuthenticationScheme,
            options =>
            {
                options.Cookie.Name =
                    "webui.local-test";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy =
                    CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite =
                    SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan =
                    TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.LoginPath = "/local-login";
                options.AccessDeniedPath = "/local-login";
            });

    builder.Services.AddAuthorization();
}

var dataProtectionKeysDirectory =
    builder.Configuration["WebUi:DataProtectionKeysDirectory"];

if (!string.IsNullOrWhiteSpace(dataProtectionKeysDirectory))
{
    var normalizedDirectory =
        dataProtectionKeysDirectory.Trim();

    if (!Path.IsPathFullyQualified(normalizedDirectory))
    {
        throw new InvalidOperationException(
            "Der konfigurierte Datenschutzschluessel-Pfad muss absolut sein.");
    }

    var fullDirectory =
        Path.GetFullPath(normalizedDirectory);

    Directory.CreateDirectory(fullDirectory);

    var dataProtectionBuilder = builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(
            new DirectoryInfo(fullDirectory))
        .SetApplicationName(
            "webui");

    var certificatePath =
        builder.Configuration["WebUi:DataProtectionCertificatePath"];

    if (!string.IsNullOrWhiteSpace(certificatePath))
    {
        var normalizedCertificatePath = certificatePath.Trim();

        if (!Path.IsPathFullyQualified(normalizedCertificatePath))
        {
            throw new InvalidOperationException(
                "Der konfigurierte Zertifikatspfad muss absolut sein.");
        }

        var certificatePasswordFilePath =
            builder.Configuration["WebUi:DataProtectionCertificatePasswordFilePath"];

        string? certificatePassword = null;

        if (!string.IsNullOrWhiteSpace(certificatePasswordFilePath))
        {
            var normalizedPasswordPath = certificatePasswordFilePath.Trim();

            if (!Path.IsPathFullyQualified(normalizedPasswordPath))
            {
                throw new InvalidOperationException(
                    "Der konfigurierte Zertifikat-Kennwortpfad muss absolut sein.");
            }

            certificatePassword = File.ReadAllText(normalizedPasswordPath).TrimEnd();
        }

        var certificateKeyStorageFlags = OperatingSystem.IsMacOS()
            ? X509KeyStorageFlags.DefaultKeySet
            : X509KeyStorageFlags.EphemeralKeySet;

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            normalizedCertificatePath,
            certificatePassword,
            certificateKeyStorageFlags);

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "Das konfigurierte Datenschutz-Zertifikat enthaelt keinen privaten Schluessel.");
        }

        dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
    }
}

builder.Services
    .AddHttpClient(PaperlessClientFactory.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(
        static () => new SocketsHttpHandler
        {
            UseCookies = false
        });

if (oidcSettings.Enabled)
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<
        IPaperlessTokenProvider,
        ProtectedUserPaperlessTokenProvider>();
    builder.Services.AddScoped<
        ProtectedUserPaperlessTokenStore>();
    builder.Services.AddScoped<
        IPaperlessConnectionTokenStore>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    ProtectedUserPaperlessTokenStore>());
}
else if (localMultiUserEnabled)
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<
        LocalKeychainPaperlessSettings>();
    builder.Services.AddScoped<
        IPaperlessTokenProvider,
        LocalTestUserKeychainTokenProvider>();
    builder.Services.AddScoped<
        IPaperlessConnectionTokenStore>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    LocalKeychainPaperlessSettings>());
}
else
{
    throw new InvalidOperationException(
        "Es ist weder OIDC noch der lokale Mehrbenutzer-Testmodus aktiviert. Der alte Einzelbenutzer-Tokenzugriff wird nicht mehr unterstützt.");
}

builder.Services.AddSingleton<UserSessionRegistry>();
builder.Services.AddScoped<CircuitConnectionState>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler>(
    serviceProvider => serviceProvider.GetRequiredService<CircuitConnectionState>());

builder.Services.AddSingleton<
    NavigationCacheService>();

builder.Services.AddSingleton<
    CentralNavigationSyncService>();

builder.Services.AddSingleton<DisplayPrefixService>();

builder.Services.AddScoped<
    PaperlessClientFactory>();

var app = builder.Build();

ApplicationDisplayInfo.Configure(
    app.Environment.IsDevelopment(),
    isProfileB,
    isDiskStationTest);

if (proxyGuardSettings.Enabled)
{
    app.UseMiddleware<ProxyGuardMiddleware>();
}

if (forwardedHeadersKnownProxy is not null)
{
    app.UseForwardedHeaders();
}

if (oidcSettings.Enabled)
{
    app.Use(
        async (context, next) =>
        {
            context.RequestServices
                .GetRequiredService<OidcChallengeGuard>()
                .EnsureBrowserContextCookie(
                    context.Request,
                    context.Response);

            await next();
        });
}

if (localMultiUserEnabled)
{
    app.Use(
        async (context, next) =>
        {
            if (!LocalTestModeSecurity.IsLoopbackRequest(context))
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;
                context.Response.ContentType =
                    "text/plain; charset=utf-8";

                await context.Response.WriteAsync(
                    "Der lokale Mehrbenutzertest ist ausschließlich über localhost beziehungsweise eine Loopback-Adresse erreichbar.");

                return;
            }

            await next();
        });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseStatusCodePages();

if (builder.Configuration.GetValue(
        "WebUi:UseHttpsRedirection",
        app.Environment.IsDevelopment()))
{
    app.UseHttpsRedirection();
}

if (oidcSettings.Enabled || localMultiUserEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet(
    "/healthz",
    () => Results.Ok(
        new
        {
            status = "healthy"
        }))
    .AllowAnonymous();

if (oidcSettings.Enabled)
{
    app.MapGet(
        "/auth/login",
        () => Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = "/"
            }))
        .AllowAnonymous();

    app.MapGet(
        "/auth/status",
        (ClaimsPrincipal user) => Results.Ok(
            new
            {
                authenticated =
                    user.Identity?.IsAuthenticated == true,
                technicalUserKeyPresent =
                    user.HasClaim(
                        claim =>
                            claim.Type ==
                            OidcAuthenticationSettings.TechnicalUserKeyClaimType)
            }))
        .RequireAuthorization();

    app.MapPost(
        "/auth/logout",
        async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            UserSessionRegistry userSessions) =>
        {
            if (!await antiforgery.IsRequestValidAsync(httpContext))
            {
                return Results.BadRequest();
            }

            userSessions.Revoke(
                httpContext.User.FindFirstValue(
                    OidcAuthenticationSettings.SessionIdClaimType));

            return Results.SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = "/auth/signed-out"
                },
                new[]
                {
                    CookieAuthenticationDefaults.AuthenticationScheme
                });
        })
        .RequireAuthorization();

    app.MapGet(
        "/auth/signed-out",
        () => Results.Content(
            AuthenticationHtmlPages.CreateSignedOutPage(),
            "text/html; charset=utf-8"))
        .AllowAnonymous();

    app.MapGet(
        "/auth/error",
        (HttpRequest request) => Results.Content(
            AuthenticationHtmlPages.CreateOidcErrorPage(
                request.Query),
            "text/html; charset=utf-8"))
        .AllowAnonymous();


}

if (localMultiUserEnabled)
{
    app.MapLocalDevelopmentAuthenticationEndpoints();
}

var previewEndpoint = app.MapGet(
    "/preview/documents/{documentId:int}/thumbnail",
    async (
        int documentId,
        PaperlessClientFactory clientFactory,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        if (documentId <= 0)
        {
            return Results.BadRequest();
        }

        try
        {
            var client = await clientFactory.CreateAsync(cancellationToken);
            var thumbnail = await client.GetDocumentThumbnailAsync(
                documentId,
                cancellationToken);

            httpContext.Response.Headers.CacheControl =
                "private, no-store, max-age=0";

            return Results.File(
                thumbnail.Content,
                thumbnail.ContentType);
        }
        catch (Exception exception)
        {
            var error = PaperlessErrorClassifier.Classify(
                exception,
                cancellationToken,
                fallbackMessage: "Die Dokumentvorschau konnte nicht geladen werden.");

            if (error.IsControlledCancellation)
            {
                return Results.StatusCode(499);
            }

            var logger = loggerFactory.CreateLogger("PaperlessDocumentPreview");
            logger.LogError(
                "Paperless-Aktion fehlgeschlagen. Ereigniskennung: {EventId}; Aktion: {Action}; Kategorie: {Category}; HTTP-Status: {StatusCode}; Ausnahmetyp: {ExceptionType}; Zeitüberschreitung: {IsTimeout}",
                error.EventId,
                "Dokumentvorschau laden",
                error.Category,
                error.StatusCode,
                error.ExceptionType,
                error.IsTimeout);

            var responseStatus = error.StatusCode switch
            {
                401 => 401,
                403 => 403,
                404 => 404,
                408 => 408,
                429 => 429,
                >= 500 and <= 599 => error.StatusCode.Value,
                _ when error.IsTimeout => 504,
                _ => 502
            };

            return Results.StatusCode(responseStatus);
        }
    });

var documentPreviewEndpoint = app.MapGet(
    "/preview/documents/{documentId:int}/document",
    async (
        int documentId,
        PaperlessClientFactory clientFactory,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        if (documentId <= 0)
        {
            return Results.BadRequest();
        }

        try
        {
            var client = await clientFactory.CreateAsync(cancellationToken);
            var preview = await client.GetDocumentPreviewAsync(
                documentId,
                cancellationToken);

            httpContext.Response.Headers.CacheControl =
                "private, no-store, max-age=0";

            return Results.File(
                preview.Content,
                preview.ContentType,
                enableRangeProcessing: true);
        }
        catch (Exception exception)
        {
            var error = PaperlessErrorClassifier.Classify(
                exception,
                cancellationToken,
                fallbackMessage: "Die vollständige Dokumentvorschau konnte nicht geladen werden.");

            if (error.IsControlledCancellation)
            {
                return Results.StatusCode(499);
            }

            var logger = loggerFactory.CreateLogger("PaperlessDocumentPreview");
            logger.LogError(
                "Paperless-Aktion fehlgeschlagen. Ereigniskennung: {EventId}; Aktion: {Action}; Kategorie: {Category}; HTTP-Status: {StatusCode}; Ausnahmetyp: {ExceptionType}; Zeitüberschreitung: {IsTimeout}",
                error.EventId,
                "Vollständige Dokumentvorschau laden",
                error.Category,
                error.StatusCode,
                error.ExceptionType,
                error.IsTimeout);

            var responseStatus = error.StatusCode switch
            {
                401 => 401,
                403 => 403,
                404 => 404,
                408 => 408,
                429 => 429,
                >= 500 and <= 599 => error.StatusCode.Value,
                _ when error.IsTimeout => 504,
                _ => 502
            };

            return Results.StatusCode(responseStatus);
        }
    });

if (oidcSettings.Enabled || localMultiUserEnabled)
{
    previewEndpoint.RequireAuthorization();
    documentPreviewEndpoint.RequireAuthorization();
}

var razorComponents = app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (oidcSettings.Enabled || localMultiUserEnabled)
{
    razorComponents.RequireAuthorization();
}

app.Run();

static string ToDiagnosticFlag(
    bool value)
{
    return value
        ? "JA"
        : "NEIN";
}

static string ToDiagnosticComparison(
    string? left,
    string? right)
{
    if (string.IsNullOrWhiteSpace(left) ||
        string.IsNullOrWhiteSpace(right))
    {
        return "NICHT_PRUEFBAR";
    }

    return string.Equals(
        left,
        right,
        StringComparison.Ordinal)
        ? "JA"
        : "NEIN";
}

static byte[] CreateDiagnosticDigest(
    string value)
{
    return SHA256.HashData(
        Encoding.UTF8.GetBytes(value));
}

static string ToDiagnosticDigestComparison(
    byte[]? leftDigest,
    string? right)
{
    if (leftDigest is null ||
        string.IsNullOrWhiteSpace(right))
    {
        return "NICHT_PRUEFBAR";
    }

    var rightDigest =
        CreateDiagnosticDigest(right);

    return CryptographicOperations.FixedTimeEquals(
        leftDigest,
        rightDigest)
        ? "JA"
        : "NEIN";
}

static string GetDiagnosticProperty(
    AuthenticationProperties? properties,
    string key)
{
    if (properties?.Items.TryGetValue(
            key,
            out var value) == true &&
        !string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    return "NICHT_BEKANNT";
}

static byte[]? GetDiagnosticHttpContextDigest(
    HttpContext httpContext,
    string key)
{
    return httpContext.Items.TryGetValue(
            key,
            out var value)
        ? value as byte[]
        : null;
}

static string GetDiagnosticHttpContextClassification(
    HttpContext httpContext,
    string key)
{
    if (httpContext.Items.TryGetValue(
            key,
            out var value) &&
        value is string classification &&
        !string.IsNullOrWhiteSpace(classification))
    {
        return classification;
    }

    return "NICHT_BEKANNT";
}

static string ClassifyOidcRemoteFailure(
    Exception? failure)
{
    for (var current = failure;
         current is not null;
         current = current.InnerException)
    {
        var typeName = current.GetType().Name;

        if (typeName.Contains(
                "Correlation",
                StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains(
                "State",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Korrelations-/Statusfehler";
        }

        if (current is TimeoutException)
        {
            return "Zeitüberschreitung";
        }

        if (current is OperationCanceledException)
        {
            return "Abbruch";
        }

        if (typeName.Contains(
                "SecurityToken",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Tokenprüfung";
        }

        if (typeName.Contains(
                "OpenIdConnectProtocol",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Protokollfehler";
        }
    }

    return "Unbekannter OIDC-Fehler";
}

static string GetOidcExceptionType(
    Exception? failure)
{
    return failure?.GetType().Name
        ?? "Kein Ausnahmetyp";
}

static int CountCookiesByPrefix(
    IEnumerable<string> cookieNames,
    string prefix)
{
    return cookieNames.Count(
        cookieName =>
            cookieName.StartsWith(
                prefix,
                StringComparison.Ordinal));
}

static async Task<LocalCookieAuthenticationResult>
    GetLocalCookieAuthenticationResultAsync(
        HttpContext httpContext)
{
    try
    {
        var result =
            await httpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

        if (result.Succeeded)
        {
            return new("Erfolgreich", "KeinAusnahmetyp");
        }

        if (result.Failure is not null)
        {
            return new(
                "Fehlgeschlagen",
                result.Failure.GetType().Name);
        }

        return new("KeinErgebnis", "KeinAusnahmetyp");
    }
    catch (Exception exception)
    {
        return new(
            "TechnischerFehler",
            exception.GetType().Name);
    }
}

static string CreateOidcErrorRedirectUrl(
    string eventId,
    string category,
    string exceptionType,
    bool hasAuthorizationCode,
    bool hasState,
    bool hasRemoteError,
    int correlationCookieCount,
    int nonceCookieCount,
    bool currentUserAuthenticated,
    LocalCookieAuthenticationResult localCookieResult,
    bool isHttps,
    bool hasForwardedProto,
    bool hasForwardedHost,
    bool responseHasStarted)
{
    return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
        "/auth/error",
        new Dictionary<string, string?>
        {
            ["event"] = eventId,
            ["category"] = category,
            ["exception"] = exceptionType,
            ["code"] = ToFlag(hasAuthorizationCode),
            ["state"] = ToFlag(hasState),
            ["remoteError"] = ToFlag(hasRemoteError),
            ["correlation"] = correlationCookieCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["nonce"] = nonceCookieCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["currentUser"] = ToFlag(currentUserAuthenticated),
            ["localCookie"] = localCookieResult.Status,
            ["localCookieException"] = localCookieResult.ExceptionType,
            ["https"] = ToFlag(isHttps),
            ["forwardedProto"] = ToFlag(hasForwardedProto),
            ["forwardedHost"] = ToFlag(hasForwardedHost),
            ["responseStarted"] = ToFlag(responseHasStarted)
        });
}

static string ToFlag(bool value) => value ? "1" : "0";

static string CreateTechnicalUserKey(
    string issuer,
    string subject)
{
    var input = Encoding.UTF8.GetBytes(
        $"{issuer}\n{subject}");

    var hash = SHA256.HashData(input);

    return Convert.ToHexString(hash)
        .ToLowerInvariant();
}

sealed record LocalCookieAuthenticationResult(
    string Status,
    string ExceptionType);

