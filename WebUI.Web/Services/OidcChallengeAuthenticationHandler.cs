using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace WebUI.Web.Services;

public sealed class OidcChallengeAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName =
        "WebUiOidcChallengeGuard";

    public OidcChallengeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(
            options,
            logger,
            encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        return Task.FromResult(
            AuthenticateResult.NoResult());
    }

    protected override async Task HandleChallengeAsync(
        AuthenticationProperties properties)
    {
        var guard =
            Context.RequestServices
                .GetRequiredService<OidcChallengeGuard>();
        var diagnosticLogger =
            Context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(
                    OidcDiagnosticsFileLoggerProvider.LoggerCategory);

        if (!guard.TryGetBrowserContextKey(
                Request,
                out var browserContextKey))
        {
            browserContextKey =
                guard.EnsureBrowserContextCookie(
                    Request,
                    Response);
        }

        var now =
            DateTimeOffset.UtcNow;
        var acquireResult =
            guard.TryAcquire(
                browserContextKey,
                now);

        if (acquireResult ==
            OidcChallengeAcquireResult.Blocked)
        {
            var correlationState =
                guard.Inspect(
                    Request);

            diagnosticLogger.LogWarning(
                "OIDC-DIAG ChallengeBlockedParallel; Aktive Korrelationscookies: {CorrelationCookieCount}",
                correlationState.CorrelationCookieCount);

            await WriteBlockedResponseAsync(
                StatusCodes.Status409Conflict,
                "Anmeldung läuft bereits",
                "Für diesen Browser ist bereits eine sichere Anmeldung aktiv. Bitte schließen Sie zuerst den bereits begonnenen Anmeldevorgang ab.");
            return;
        }

        if (acquireResult ==
            OidcChallengeAcquireResult.ExpiredReplaced)
        {
            diagnosticLogger.LogInformation(
                "OIDC-DIAG ChallengeGuardExpired; Neue Challenge-Lease übernommen");
        }

        diagnosticLogger.LogInformation(
            "OIDC-DIAG ChallengeGuardAcquired");

        try
        {
            await Context.ChallengeAsync(
                OpenIdConnectDefaults.AuthenticationScheme,
                properties);
        }
        catch
        {
            if (guard.Release(
                    browserContextKey))
            {
                diagnosticLogger.LogInformation(
                    "OIDC-DIAG ChallengeGuardReleased; Grund: ChallengeStartFehler");
            }

            throw;
        }
    }

    private async Task WriteBlockedResponseAsync(
        int statusCode,
        string heading,
        string message)
    {
        Response.StatusCode =
            statusCode;
        Response.ContentType =
            "text/html; charset=utf-8";

        await Response.WriteAsync(
            $$"""
            <!doctype html>
            <html lang="de">
            <head><meta charset="utf-8"><title>{{heading}}</title></head>
            <body><main><h1>{{heading}}</h1><p>{{message}}</p><p><a href="/">Zur Startseite</a></p></main></body>
            </html>
            """);
    }
}
