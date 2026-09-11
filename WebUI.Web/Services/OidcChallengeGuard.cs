using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace WebUI.Web.Services;

public sealed class OidcChallengeGuard
{
    public const string BrowserContextCookieName =
        "__Host-webui.oidc-guard";
    public const string BrowserContextItemKey =
        "webui.oidc-browser-context";
    public const string ChallengeIdProperty =
        ".webui.oidc-guard-challenge-id";
    public const string CorrelationCookiePrefix =
        ".AspNetCore.Correlation.";

    // Schutzfenster gegen die historisch beobachteten Doppelstarts.
    public static readonly TimeSpan LeaseLifetime =
        TimeSpan.FromSeconds(15);

    // Sobald ein Callback eindeutig zugeordnet wurde, bleibt dessen
    // Tokenaustausch länger gegen einen Parallelstart geschützt.
    public static readonly TimeSpan CallbackLeaseLifetime =
        TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<
        string,
        OidcChallengeLease> _activeLeases =
            new(StringComparer.Ordinal);

    public bool TryGetBrowserContextKey(
        HttpRequest request,
        out string browserContextKey)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HttpContext.Items.TryGetValue(
                BrowserContextItemKey,
                out var preparedBrowserContextValue) &&
            preparedBrowserContextValue is string preparedBrowserContextKey &&
            Guid.TryParseExact(
                preparedBrowserContextKey,
                "N",
                out _))
        {
            browserContextKey = preparedBrowserContextKey;
            return true;
        }

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

        return SetNewBrowserContextCookie(
            request,
            response);
    }

    public string RotateBrowserContextCookie(
        HttpRequest request,
        HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        if (TryGetBrowserContextKey(
                request,
                out var existingBrowserContextKey))
        {
            ReleaseBrowserContext(
                existingBrowserContextKey);
        }

        return SetNewBrowserContextCookie(
            request,
            response);
    }

    public OidcChallengeAcquireResult TryAcquire(
        string browserContextKey,
        string challengeId,
        DateTimeOffset now)
    {
        ValidateBrowserContextKey(
            browserContextKey);
        ValidateChallengeId(
            challengeId);

        var newLease =
            new OidcChallengeLease(
                challengeId,
                now.Add(LeaseLifetime),
                CallbackStarted: false);

        while (true)
        {
            if (_activeLeases.TryAdd(
                    browserContextKey,
                    newLease))
            {
                return OidcChallengeAcquireResult.Acquired;
            }

            if (!_activeLeases.TryGetValue(
                    browserContextKey,
                    out var existingLease))
            {
                continue;
            }

            if (existingLease.ExpiresAt > now)
            {
                return OidcChallengeAcquireResult.Blocked;
            }

            if (_activeLeases.TryUpdate(
                    browserContextKey,
                    newLease,
                    existingLease))
            {
                return OidcChallengeAcquireResult.ExpiredReplaced;
            }
        }
    }

    public OidcChallengeCallbackResult TryBeginCallback(
        string browserContextKey,
        string challengeId,
        DateTimeOffset now)
    {
        ValidateBrowserContextKey(
            browserContextKey);
        ValidateChallengeId(
            challengeId);

        while (true)
        {
            if (!_activeLeases.TryGetValue(
                    browserContextKey,
                    out var existingLease) ||
                !string.Equals(
                    existingLease.ChallengeId,
                    challengeId,
                    StringComparison.Ordinal))
            {
                return OidcChallengeCallbackResult.StaleOrMissing;
            }

            if (existingLease.CallbackStarted)
            {
                return OidcChallengeCallbackResult.Duplicate;
            }

            var callbackLease =
                existingLease with
                {
                    ExpiresAt =
                        now.Add(
                            CallbackLeaseLifetime),
                    CallbackStarted = true
                };

            if (_activeLeases.TryUpdate(
                    browserContextKey,
                    callbackLease,
                    existingLease))
            {
                return OidcChallengeCallbackResult.Started;
            }
        }
    }

    public bool IsCurrent(
        string browserContextKey,
        string challengeId)
    {
        ValidateBrowserContextKey(
            browserContextKey);
        ValidateChallengeId(
            challengeId);

        return _activeLeases.TryGetValue(
                browserContextKey,
                out var existingLease) &&
            string.Equals(
                existingLease.ChallengeId,
                challengeId,
                StringComparison.Ordinal);
    }

    public bool IsCurrent(
        HttpRequest request,
        string challengeId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryGetBrowserContextKey(
                request,
                out var browserContextKey) &&
            IsCurrent(
                browserContextKey,
                challengeId);
    }

    public bool Release(
        string browserContextKey,
        string challengeId)
    {
        ValidateBrowserContextKey(
            browserContextKey);
        ValidateChallengeId(
            challengeId);

        while (_activeLeases.TryGetValue(
                   browserContextKey,
                   out var existingLease))
        {
            if (!string.Equals(
                    existingLease.ChallengeId,
                    challengeId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var pair =
                new KeyValuePair<string, OidcChallengeLease>(
                    browserContextKey,
                    existingLease);

            if (_activeLeases.TryRemove(
                    pair))
            {
                return true;
            }
        }

        return false;
    }

    public bool Release(
        HttpRequest request,
        string challengeId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryGetBrowserContextKey(
                request,
                out var browserContextKey) &&
            Release(
                browserContextKey,
                challengeId);
    }

    public bool ReleaseBrowserContext(
        string browserContextKey)
    {
        ValidateBrowserContextKey(
            browserContextKey);

        return _activeLeases.TryRemove(
            browserContextKey,
            out _);
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

    private string SetNewBrowserContextCookie(
        HttpRequest request,
        HttpResponse response)
    {
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

    private static void ValidateBrowserContextKey(
        string browserContextKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            browserContextKey);
    }

    private static void ValidateChallengeId(
        string challengeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            challengeId);

        if (!Guid.TryParseExact(
                challengeId,
                "N",
                out _))
        {
            throw new ArgumentException(
                "Die OIDC-Challenge-ID ist ungültig.",
                nameof(challengeId));
        }
    }

    private sealed record OidcChallengeLease(
        string ChallengeId,
        DateTimeOffset ExpiresAt,
        bool CallbackStarted);
}

public enum OidcChallengeAcquireResult
{
    Acquired,
    Blocked,
    ExpiredReplaced
}

public enum OidcChallengeCallbackResult
{
    Started,
    Duplicate,
    StaleOrMissing
}

public readonly record struct OidcChallengeGuardState(
    bool HasActiveChallenge,
    int CorrelationCookieCount);
