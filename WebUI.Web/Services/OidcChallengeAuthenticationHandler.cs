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

    private const int BlockedRetryDelaySeconds = 3;

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

        var challengeId =
            Guid.NewGuid().ToString("N");
        properties.Items[
            OidcChallengeGuard.ChallengeIdProperty] =
                challengeId;

        var now =
            DateTimeOffset.UtcNow;
        var acquireResult =
            guard.TryAcquire(
                browserContextKey,
                challengeId,
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
                "Für diesen Browser ist bereits eine sichere Anmeldung aktiv. Der Status wird automatisch erneut geprüft.");
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
                    browserContextKey,
                    challengeId))
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
        Response.Headers["Cache-Control"] =
            "no-store, no-cache, max-age=0";
        Response.Headers["Retry-After"] =
            BlockedRetryDelaySeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        await Response.WriteAsync(
            $$"""
            <!doctype html>
            <html lang="de">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta http-equiv="refresh" content="{{BlockedRetryDelaySeconds}};url=/auth/login">
                <title>{{heading}}</title>
            </head>
            <body>
                <main>
                    <h1>{{heading}}</h1>
                    <p>{{message}}</p>
                    <p><a href="/auth/login">Jetzt erneut prüfen</a></p>
                </main>
            </body>
            </html>
            """);
    }
}
