using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using WebUI.Infrastructure;
using System.Security.Claims;

namespace WebUI.Web.Services;

public static class LocalDevelopmentAuthenticationEndpoints
{
    public static void MapLocalDevelopmentAuthenticationEndpoints(
        this WebApplication app)
    {
        app.MapGet(
            "/local-login",
            (HttpContext httpContext) =>
            {
                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    return Results.Redirect("/");
                }

                return Results.Content(
                    CreateLocalLoginPage(
                        httpContext.Request.Query["status"].ToString()),
                    "text/html; charset=utf-8");
            })
            .AllowAnonymous();

        app.MapGet(
            "/local-auth/login/{alias}",
            async (
                string alias,
                HttpContext httpContext,
                LocalKeychainPaperlessSettings keychainSettings,
                IHttpClientFactory httpClientFactory,
                CancellationToken cancellationToken) =>
            {
                if (!LocalTestUserSettings.TryGetUser(alias, out var localUser))
                {
                    return Results.Redirect("/local-login?status=unknown");
                }

                try
                {
                    var baseUrl = await keychainSettings.GetBaseUrlAsync(
                        cancellationToken);
                    var credential = await keychainSettings.GetCredentialAsync(
                        localUser,
                        cancellationToken);
                    var client = new PaperlessApiClient(
                        httpClientFactory.CreateClient(PaperlessClientFactory.HttpClientName),
                        baseUrl,
                        credential.Token);

                    await client.ValidateCurrentTokenAsync(cancellationToken);

                    var identity = new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.Name, localUser.DisplayName),
                            new Claim(LocalTestUserSettings.AliasClaimType, localUser.Alias),
                            new Claim(
                                LocalTestUserSettings.TechnicalUserKeyClaimType,
                                credential.TechnicalUserKey),
                            new Claim(
                                LocalTestUserSettings.SessionIdClaimType,
                                Guid.NewGuid().ToString("N"))
                        },
                        LocalTestUserSettings.AuthenticationScheme);

                    await httpContext.SignInAsync(
                        LocalTestUserSettings.AuthenticationScheme,
                        new ClaimsPrincipal(identity),
                        new AuthenticationProperties
                        {
                            IsPersistent = false,
                            RedirectUri = "/"
                        });

                    return Results.Redirect("/");
                }
                catch
                {
                    return Results.Redirect("/local-login?status=failed");
                }
            })
            .AllowAnonymous();

        app.MapPost(
            "/local-auth/logout",
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
                        LocalTestUserSettings.SessionIdClaimType));

                await httpContext.SignOutAsync(
                    LocalTestUserSettings.AuthenticationScheme);

                return Results.Redirect("/local-login");
            })
            .RequireAuthorization();
    }

    private static string CreateLocalLoginPage(
        string? status)
    {
        var productName = System.Net.WebUtility.HtmlEncode(
            ApplicationDisplayInfo.FullProductName);
        var statusAndVersion = System.Net.WebUtility.HtmlEncode(
            ApplicationDisplayInfo.StatusAndVersion);
        var buttons = string.Join(
            Environment.NewLine,
            LocalTestUserSettings.GetUsers().Select(user =>
                $"<a class=\"login-button\" href=\"/local-auth/login/{user.Alias}\">{System.Net.WebUtility.HtmlEncode(user.DisplayName)}</a>"));
        var statusMessage = status switch
        {
            "unknown" => "Die gewählte lokale Testidentität ist nicht verfügbar.",
            "failed" => "Die lokale Schlüsselbundzuordnung fehlt, ist nicht konsistent oder das persönliche Token wurde von Paperless-ngx abgelehnt.",
            _ => null
        };
        var statusBlock = string.IsNullOrWhiteSpace(statusMessage)
            ? string.Empty
            : $"<p class=\"status-message\" role=\"alert\">{System.Net.WebUtility.HtmlEncode(statusMessage)}</p>";

        return $$"""
            <!doctype html>
            <html lang="de">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Lokale Testanmeldung · {{productName}}</title>
                <style>
                    :root { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; color-scheme: light; }
                    * { box-sizing: border-box; }
                    body { margin: 0; min-height: 100vh; background: #f5f6f8; color: #1f2937; }
                    .app-shell { min-height: 100vh; display: grid; grid-template-rows: 54px minmax(0, 1fr); }
                    .app-header { display: flex; align-items: center; justify-content: space-between; height: 54px; gap: 1rem; padding: 0 1rem; overflow: hidden; color: white; background: linear-gradient(90deg, #102d5c, #26306f); }
                    .app-brand { min-width: 0; overflow: hidden; font-size: 1.18rem; line-height: 1; text-overflow: ellipsis; white-space: nowrap; }
                    .app-build { color: rgba(255,255,255,.78); font-size: .76rem; line-height: 1.05; white-space: nowrap; }
                    .page-area { display: grid; place-items: center; min-height: 0; padding: 1rem; }
                    main { width: min(100%, 620px); padding: 2rem; border: 1px solid #d9dee8; border-radius: 12px; background: white; box-shadow: 0 8px 24px rgba(16,45,92,.08); }
                    h1 { margin: 0 0 .55rem; color: #102d5c; text-align: center; }
                    .subtitle { margin: 0 0 1.25rem; color: #5b6472; text-align: center; }
                    .status-message { margin: 0 0 1rem; padding: .8rem 1rem; border: 1px solid #9b2c2c; border-radius: 8px; background: #fff5f5; color: #742a2a; }
                    .buttons { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .8rem; }
                    .login-button { display: block; padding: 1rem; border-radius: 8px; background: #102d5c; color: white; text-align: center; text-decoration: none; font-weight: 700; }
                    .login-button:hover { background: #26306f; }
                    .notice { margin: 1.25rem 0 0; padding: 1rem; border: 1px solid #26306f; border-radius: 8px; background: #eef1f8; }
                    @media (max-width: 520px) { .buttons { grid-template-columns: 1fr; } }
                    @media (max-width: 680px) { .app-header { gap: .55rem; padding: 0 .7rem; } .app-brand { font-size: 1rem; } .app-build { font-size: .68rem; } }
                </style>
            </head>
            <body>
                <div class="app-shell">
                    <header class="app-header">
                        <strong class="app-brand">{{productName}}</strong>
                        <span class="app-build">{{statusAndVersion}}</span>
                    </header>
                    <div class="page-area">
                        <main>
                            <h1>Lokale Testanmeldung</h1>
                            <p class="subtitle">Wählen Sie eine lokale Testidentität für diese Sitzung.</p>
                            {{statusBlock}}
                            <div class="buttons">{{buttons}}</div>
                            <p class="notice">Diese Auswahl ist ausschließlich für den lokalen Entwicklungsbetrieb bestimmt. Das persönliche API-Token bleibt im macOS-Schlüsselbund.</p>
                        </main>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

}
