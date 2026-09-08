using System.Net;

namespace WebUI.Web.Services;

public static class LocalTestModeSecurity
{
    public static string GetRequiredLoopbackUrl(
        IConfiguration configuration)
    {
        var configuredValue =
            configuration["LocalMultiUser:LoopbackUrl"];

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new InvalidOperationException(
                "Für den lokalen Mehrbenutzertest fehlt die verbindliche Loopback-Adresse.");
        }

        var normalized = configuredValue.Trim();

        if (normalized.Contains(';') ||
            !Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !IsLoopbackHost(uri.Host) ||
            uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException(
                "Der lokale Mehrbenutzertest darf ausschließlich an eine einzelne localhost- oder Loopback-Adresse gebunden werden.");
        }

        return uri.GetComponents(
            UriComponents.SchemeAndServer,
            UriFormat.UriEscaped);
    }

    public static bool IsLoopbackRequest(
        HttpContext context)
    {
        return IsLoopbackAddress(
                   context.Connection.RemoteIpAddress) &&
               IsLoopbackAddress(
                   context.Connection.LocalIpAddress) &&
               IsLoopbackHost(
                   context.Request.Host.Host);
    }

    private static bool IsLoopbackHost(
        string host)
    {
        if (string.Equals(
                host,
                "localhost",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) &&
               IsLoopbackAddress(address);
    }

    private static bool IsLoopbackAddress(
        IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        var normalized = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;

        return IPAddress.IsLoopback(normalized);
    }
}
