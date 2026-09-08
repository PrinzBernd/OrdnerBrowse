using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace WebUI.Web.Services;

public sealed class OidcChallengeGuard
{
    public const string BrowserContextCookieName =
        "__Host-webui.oidc-guard";
    public const string BrowserContextItemKey =
        "webui.oidc-browser-context";
    public const string CorrelationCookiePrefix =
        ".AspNetCore.Correlation.";

    // Entwurfswert für die AP03-Korrektur:
    // kurzlebige serverseitige Lease als Rückfallsicherung.
    public static readonly TimeSpan LeaseLifetime =
        TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<
        string,
        DateTimeOffset> _activeLeases =
            new(StringComparer.Ordinal);

    public bool TryGetBrowserContextKey(
        HttpRequest request,
        out string browserContextKey)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cookies.TryGetValue(
                BrowserContextCookieName,
                out var candidate) &&
            Guid.TryParseExact(
                candidate,
                "N",
                out _))
        {
            browserContextKey = candidate;
            return true;
        }

        browserContextKey = string.Empty;
        return false;
    }

    public string EnsureBrowserContextCookie(
        HttpRequest request,
        HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        if (TryGetBrowserContextKey(
                request,
                out var existingBrowserContextKey))
        {
            request.HttpContext.Items[
                BrowserContextItemKey] =
                    existingBrowserContextKey;
            return existingBrowserContextKey;
        }

        if (request.HttpContext.Items.TryGetValue(
                BrowserContextItemKey,
                out var preparedBrowserContextValue) &&
            preparedBrowserContextValue is string preparedBrowserContextKey &&
            Guid.TryParseExact(
                preparedBrowserContextKey,
                "N",
                out _))
        {
            return preparedBrowserContextKey;
        }

        var newBrowserContextKey =
            Guid.NewGuid().ToString("N");

        response.Cookies.Append(
            BrowserContextCookieName,
            newBrowserContextKey,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true
            });

        request.HttpContext.Items[
            BrowserContextItemKey] =
                newBrowserContextKey;

        return newBrowserContextKey;
    }

    public OidcChallengeAcquireResult TryAcquire(
        string browserContextKey,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            browserContextKey);

        var newExpiry =
            now.Add(
                LeaseLifetime);

        while (true)
        {
            if (_activeLeases.TryAdd(
                    browserContextKey,
                    newExpiry))
            {
                return OidcChallengeAcquireResult.Acquired;
            }

            if (!_activeLeases.TryGetValue(
                    browserContextKey,
                    out var existingExpiry))
            {
                continue;
            }

            if (existingExpiry > now)
            {
                return OidcChallengeAcquireResult.Blocked;
            }

            if (_activeLeases.TryUpdate(
                    browserContextKey,
                    newExpiry,
                    existingExpiry))
            {
                return OidcChallengeAcquireResult.ExpiredReplaced;
            }
        }
    }

    public bool Release(
        string browserContextKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            browserContextKey);

        return _activeLeases.TryRemove(
            browserContextKey,
            out _);
    }

    public bool Release(
        HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryGetBrowserContextKey(
                request,
                out var browserContextKey) &&
            Release(
                browserContextKey);
    }

    public OidcChallengeGuardState Inspect(
        HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationCookieCount =
            request.Cookies.Keys.Count(
                cookieName =>
                    cookieName.StartsWith(
                        CorrelationCookiePrefix,
                        StringComparison.Ordinal));

        return new OidcChallengeGuardState(
            correlationCookieCount > 0,
            correlationCookieCount);
    }
}

public enum OidcChallengeAcquireResult
{
    Acquired,
    Blocked,
    ExpiredReplaced
}

public readonly record struct OidcChallengeGuardState(
    bool HasActiveChallenge,
    int CorrelationCookieCount);
