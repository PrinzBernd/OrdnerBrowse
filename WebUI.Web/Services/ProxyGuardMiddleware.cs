using System.Security.Cryptography;

namespace WebUI.Web.Services;

public sealed class ProxyGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ProxyGuardSettings _settings;

    public ProxyGuardMiddleware(
        RequestDelegate next,
        ProxyGuardSettings settings)
    {
        _next = next;
        _settings = settings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(
                ProxyGuardSettings.HeaderName,
                out var values) ||
            values.Count != 1)
        {
            Reject(context);
            return;
        }

        var candidate = values[0] ?? string.Empty;

        if (candidate.Length != ProxyGuardSettings.HeaderLength ||
            candidate.IndexOf(',') >= 0)
        {
            Reject(context);
            return;
        }

        var candidateBytes = GC.AllocateUninitializedArray<byte>(
            ProxyGuardSettings.HeaderLength);

        try
        {
            for (var index = 0; index < candidate.Length; index++)
            {
                var character = candidate[index];

                if (!IsLowerHex(character))
                {
                    Reject(context);
                    return;
                }

                candidateBytes[index] = (byte)character;
            }

            if (!_settings.Matches(candidateBytes))
            {
                Reject(context);
                return;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateBytes);
        }

        context.Request.Headers.Remove(
            ProxyGuardSettings.HeaderName);

        await _next(context);
    }

    private static void Reject(HttpContext context)
    {
        context.Response.StatusCode =
            StatusCodes.Status403Forbidden;
        context.Response.ContentLength = 0;
        context.Response.ContentType = null;
        context.Response.Headers["Cache-Control"] = "no-store";
    }

    private static bool IsLowerHex(char value)
    {
        return value is >= '0' and <= '9' or
               >= 'a' and <= 'f';
    }
}
