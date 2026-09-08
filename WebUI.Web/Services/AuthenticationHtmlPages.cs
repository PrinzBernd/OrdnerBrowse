using Microsoft.AspNetCore.Http;

namespace WebUI.Web.Services;

internal static class AuthenticationHtmlPages
{
    public static string CreateSignedOutPage()
    {
        var productName =
            System.Net.WebUtility.HtmlEncode(
                ApplicationDisplayInfo.FullProductName);
        var statusAndVersion =
            System.Net.WebUtility.HtmlEncode(
                ApplicationDisplayInfo.StatusAndVersion);

        return $$"""
            <!doctype html>
            <html lang="de">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Abgemeldet · {{productName}}</title>
                <style>
                    :root {
                        color-scheme: light;
                        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                    }

                    * {
                        box-sizing: border-box;
                    }

                    html,
                    body {
                        min-height: 100%;
                        margin: 0;
                    }

                    body {
                        background: #f5f6f8;
                        color: #1f2937;
                    }

                    .app-shell {
                        min-height: 100vh;
                        display: grid;
                        grid-template-rows: 54px minmax(0, 1fr);
                    }

                    .app-header {
                        display: flex;
                        align-items: center;
                        justify-content: space-between;
                        gap: 1rem;
                        min-width: 0;
                        padding: 0 1rem;
                        overflow: hidden;
                        color: white;
                        background: linear-gradient(90deg, #102d5c, #26306f);
                    }

                    .app-brand {
                        min-width: 0;
                        overflow: hidden;
                        font-size: 1.18rem;
                        line-height: 1;
                        text-overflow: ellipsis;
                        white-space: nowrap;
                    }

                    .app-session {
                        min-width: 0;
                        max-width: 62%;
                        display: flex;
                        flex-direction: column;
                        align-items: flex-end;
                        justify-content: center;
                        gap: .16rem;
                        text-align: right;
                    }

                    .app-build,
                    .app-signed-out {
                        max-width: 100%;
                        color: rgba(255, 255, 255, .78);
                        font-size: .76rem;
                        line-height: 1.05;
                        white-space: nowrap;
                    }

                    .app-signed-out {
                        color: white;
                        font-weight: 700;
                    }

                    main {
                        min-width: 0;
                        display: grid;
                        place-items: center;
                        padding: 2rem 1rem;
                    }

                    .signed-out-card {
                        width: min(100%, 680px);
                        padding: clamp(1.75rem, 5vw, 3rem);
                        border: 1px solid #d9dee8;
                        border-radius: 12px;
                        background: white;
                        box-shadow: 0 8px 24px rgba(16, 45, 92, .08);
                        text-align: center;
                    }

                    h1 {
                        margin: 0 0 1.25rem;
                        color: #102d5c;
                        font-size: clamp(1.65rem, 4vw, 2.15rem);
                    }

                    p {
                        margin: .8rem 0;
                        line-height: 1.55;
                    }

                    .browser-notice {
                        margin: 1.4rem 0;
                        padding: 1rem 1.1rem;
                        border: 4px solid #26306f;
                        border-radius: 6px;
                        background: #eef1f8;
                        color: #102d5c;
                        font-weight: 700;
                    }

                    .secondary-notice {
                        color: #5b6472;
                        font-size: .95rem;
                    }

                    .login-again-button {
                        display: inline-block;
                        margin: .25rem 0 1rem;
                        padding: .75rem 1.2rem;
                        border: 2px solid #26306f;
                        border-radius: 6px;
                        background: #26306f;
                        color: white;
                        font-weight: 700;
                        text-decoration: none;
                    }

                    .login-again-button:hover {
                        background: #102d5c;
                        border-color: #102d5c;
                    }

                    .login-again-button:focus-visible {
                        outline: 3px solid #8da2d8;
                        outline-offset: 3px;
                    }

                    @media (max-width: 680px) {
                        .app-header {
                            gap: .55rem;
                            padding-right: .7rem;
                            padding-left: .7rem;
                        }

                        .app-brand {
                            font-size: 1rem;
                        }

                        .app-session {
                            max-width: 66%;
                        }

                        .app-build,
                        .app-signed-out {
                            font-size: .68rem;
                        }
                    }
                </style>
            </head>
            <body>
                <div class="app-shell">
                    <header class="app-header">
                        <strong class="app-brand">{{productName}}</strong>
                        <div class="app-session" aria-label="Abmeldestatus">
                            <span class="app-build">{{statusAndVersion}}</span>
                            <span class="app-signed-out">Abgemeldet</span>
                        </div>
                    </header>
                    <main>
                        <section class="signed-out-card" aria-labelledby="signed-out-title">
                            <h1 id="signed-out-title">Abgemeldet</h1>
                            <p>Die lokale OrdnerBrowse-Sitzung wurde beendet.</p>
                            <p class="browser-notice">Zur vollständigen Beendigung der Anmeldung schließen Sie den Browser vollständig.</p>
                            <a class="login-again-button" href="/auth/login">Wieder anmelden</a>
                            <p class="secondary-notice">Andere bereits geöffnete OrdnerBrowse-Sitzungen bleiben weiterhin angemeldet.</p>
                        </section>
                    </main>
                </div>
            </body>
            </html>
            """;
    }

    public static string CreateOidcErrorPage(
        IQueryCollection query)
    {
        var diagnostic = ReadOidcFailureDiagnostic(query);
        var diagnosticBlock = diagnostic is null
            ? string.Empty
            : CreateOidcDiagnosticHtml(diagnostic);

        return $$"""
            <!doctype html>
            <html lang="de">
            <head><meta charset="utf-8"><title>Anmeldung nicht möglich</title></head>
            <body><main><h1>Anmeldung nicht möglich</h1><p>Die Anmeldung konnte nicht sicher abgeschlossen werden.</p>{{diagnosticBlock}}<p><a href="/auth/login">Erneut versuchen</a></p></main></body>
            </html>
            """;
    }

    private static OidcFailureDiagnostic? ReadOidcFailureDiagnostic(
        IQueryCollection query)
    {
        var eventId = query["event"].ToString();
        var category = query["category"].ToString();
        var exceptionType = query["exception"].ToString();
        var localCookieStatus = query["localCookie"].ToString();
        var localCookieException = query["localCookieException"].ToString();

        if (!IsValidOidcEventId(eventId) ||
            !IsAllowedOidcCategory(category) ||
            !IsSafeDiagnosticLabel(exceptionType, 100) ||
            !IsAllowedLocalCookieStatus(localCookieStatus) ||
            !IsSafeDiagnosticLabel(localCookieException, 100) ||
            !TryReadFlag(query["code"], out var hasCode) ||
            !TryReadFlag(query["state"], out var hasState) ||
            !TryReadFlag(query["remoteError"], out var hasRemoteError) ||
            !TryReadCount(query["correlation"], out var correlationCount) ||
            !TryReadCount(query["nonce"], out var nonceCount) ||
            !TryReadFlag(query["currentUser"], out var currentUser) ||
            !TryReadFlag(query["https"], out var isHttps) ||
            !TryReadFlag(query["forwardedProto"], out var forwardedProto) ||
            !TryReadFlag(query["forwardedHost"], out var forwardedHost) ||
            !TryReadFlag(query["responseStarted"], out var responseStarted))
        {
            return null;
        }

        return new(
            eventId,
            category,
            exceptionType,
            hasCode,
            hasState,
            hasRemoteError,
            correlationCount,
            nonceCount,
            currentUser,
            localCookieStatus,
            localCookieException,
            isHttps,
            forwardedProto,
            forwardedHost,
            responseStarted);
    }

    private static string CreateOidcDiagnosticHtml(
        OidcFailureDiagnostic diagnostic)
    {
        static string Encode(string value) =>
            System.Net.WebUtility.HtmlEncode(value);
        static string YesNo(bool value) => value ? "Ja" : "Nein";

        return $$"""
            <section><h2>Technische Diagnose</h2><dl>
            <dt>Ereigniskennung</dt><dd>{{Encode(diagnostic.EventId)}}</dd>
            <dt>Kategorie</dt><dd>{{Encode(diagnostic.Category)}}</dd>
            <dt>Ausnahmetyp</dt><dd>{{Encode(diagnostic.ExceptionType)}}</dd>
            <dt>Autorisierungscode vorhanden</dt><dd>{{YesNo(diagnostic.HasAuthorizationCode)}}</dd>
            <dt>State vorhanden</dt><dd>{{YesNo(diagnostic.HasState)}}</dd>
            <dt>SSO-Fehlerparameter vorhanden</dt><dd>{{YesNo(diagnostic.HasRemoteError)}}</dd>
            <dt>Korrelationscookies</dt><dd>{{diagnostic.CorrelationCookieCount}}</dd>
            <dt>Nonce-Cookies</dt><dd>{{diagnostic.NonceCookieCount}}</dd>
            <dt>Benutzer im aktuellen HTTP-Kontext authentifiziert</dt><dd>{{YesNo(diagnostic.CurrentUserAuthenticated)}}</dd>
            <dt>Lokales Cookie-Ergebnis</dt><dd>{{Encode(diagnostic.LocalCookieStatus)}}</dd>
            <dt>Lokaler Cookie-Ausnahmetyp</dt><dd>{{Encode(diagnostic.LocalCookieExceptionType)}}</dd>
            <dt>HTTPS-Anfrage</dt><dd>{{YesNo(diagnostic.IsHttps)}}</dd>
            <dt>X-Forwarded-Proto vorhanden</dt><dd>{{YesNo(diagnostic.HasForwardedProto)}}</dd>
            <dt>X-Forwarded-Host vorhanden</dt><dd>{{YesNo(diagnostic.HasForwardedHost)}}</dd>
            <dt>Antwort bereits begonnen</dt><dd>{{YesNo(diagnostic.ResponseHasStarted)}}</dd>
            </dl></section>
            """;
    }

    private static bool TryReadFlag(
        Microsoft.Extensions.Primitives.StringValues value,
        out bool result)
    {
        if (value.Count == 1 && value[0] == "1")
        {
            result = true;
            return true;
        }

        if (value.Count == 1 && value[0] == "0")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static bool TryReadCount(
        Microsoft.Extensions.Primitives.StringValues value,
        out int result)
    {
        result = 0;

        return value.Count == 1 &&
            int.TryParse(
                value[0],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out result) &&
            result is >= 0 and <= 20;
    }

    private static bool IsAllowedOidcCategory(string value) =>
        value is "Korrelations-/Statusfehler" or
            "Zeitüberschreitung" or
            "Abbruch" or
            "Tokenprüfung" or
            "Protokollfehler" or
            "Unbekannter OIDC-Fehler";

    private static bool IsAllowedLocalCookieStatus(string value) =>
        value is "Erfolgreich" or
            "Fehlgeschlagen" or
            "KeinErgebnis" or
            "TechnischerFehler";

    private static bool IsSafeDiagnosticLabel(
        string? value,
        int maximumLength)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= maximumLength &&
            value.All(character =>
                char.IsLetterOrDigit(character) ||
                character is '.' or '_' or '-' or '+');
    }

    private static bool IsValidOidcEventId(
        string? eventId)
    {
        return !string.IsNullOrWhiteSpace(eventId) &&
            eventId.Length == 17 &&
            eventId.StartsWith("OIDC-", StringComparison.Ordinal) &&
            eventId[5..].All(character =>
                character is >= '0' and <= '9' ||
                character is >= 'A' and <= 'F');
    }

    private sealed record OidcFailureDiagnostic(
        string EventId,
        string Category,
        string ExceptionType,
        bool HasAuthorizationCode,
        bool HasState,
        bool HasRemoteError,
        int CorrelationCookieCount,
        int NonceCookieCount,
        bool CurrentUserAuthenticated,
        string LocalCookieStatus,
        string LocalCookieExceptionType,
        bool IsHttps,
        bool HasForwardedProto,
        bool HasForwardedHost,
        bool ResponseHasStarted);
}
