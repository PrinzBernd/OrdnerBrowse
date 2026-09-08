using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace WebUI.Web.Services;

public static class ForwardedHeadersProxyTrust
{
    private const string DockerEnvironmentMarker = "/.dockerenv";
    private const string DockerDesktopGatewayHostName =
        "gateway.docker.internal";
    private const string LinuxRoutePath = "/proc/net/route";

    public static void ValidateConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.Equals(
                configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"]?.Trim(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true ist unzulässig, weil dadurch die ASP.NET-Core-Vertrauensgrenze für Forwarded Headers aufgeweicht würde.");
        }
    }

    public static IPAddress LoadRequiredDockerGateway()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new InvalidOperationException(
                "Die produktive Forwarded-Headers-Verarbeitung ist ausschließlich im vorgesehenen Linux-Containerbetrieb zulässig.");
        }

        if (!File.Exists(DockerEnvironmentMarker))
        {
            throw new InvalidOperationException(
                "Die produktive Forwarded-Headers-Verarbeitung erfordert den vorgesehenen Docker-Containerbetrieb.");
        }

        var dockerDesktopProxy =
            TryLoadDockerDesktopProxy();

        if (dockerDesktopProxy is not null)
        {
            return dockerDesktopProxy;
        }

        string[] routeLines;

        try
        {
            routeLines = File.ReadAllLines(LinuxRoutePath);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Die Linux-Routingtabelle konnte für die sichere Forwarded-Headers-Vertrauensprüfung nicht gelesen werden.",
                exception);
        }

        return ParseRequiredDefaultGateway(routeLines);
    }

    private static IPAddress? TryLoadDockerDesktopProxy()
    {
        IPAddress[] addresses;

        try
        {
            addresses =
                Dns.GetHostAddresses(
                    DockerDesktopGatewayHostName);
        }
        catch (SocketException)
        {
            return null;
        }

        var ipv4Addresses =
            addresses
                .Where(
                    address =>
                        address.AddressFamily ==
                        AddressFamily.InterNetwork)
                .Distinct()
                .ToArray();

        if (ipv4Addresses.Length == 0)
        {
            return null;
        }

        if (ipv4Addresses.Length != 1)
        {
            throw new InvalidOperationException(
                "gateway.docker.internal muss für die sichere Forwarded-Headers-Vertrauensprüfung eindeutig auf genau eine IPv4-Adresse auflösen.");
        }

        var proxy =
            ipv4Addresses[0];

        if (proxy.Equals(IPAddress.Any) ||
            IPAddress.IsLoopback(proxy))
        {
            throw new InvalidOperationException(
                "Die über gateway.docker.internal ermittelte Proxy-Adresse ist als vertrauenswürdiger Proxy-Hop unzulässig.");
        }

        return proxy;
    }

    public static IPAddress ParseRequiredDefaultGateway(
        IEnumerable<string> routeLines)
    {
        ArgumentNullException.ThrowIfNull(routeLines);

        var gateways = new HashSet<IPAddress>();

        foreach (var line in routeLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 8 ||
                string.Equals(
                    fields[0],
                    "Iface",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = fields[1];
            var gatewayHex = fields[2];
            var flagsHex = fields[3];

            if (!string.Equals(
                    destination,
                    "00000000",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!uint.TryParse(
                    flagsHex,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var flags) ||
                (flags & 0x3u) != 0x3u)
            {
                continue;
            }

            if (!uint.TryParse(
                    gatewayHex,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var gatewayValue))
            {
                throw new InvalidOperationException(
                    "Die Docker-Default-Gateway-Adresse in der Linux-Routingtabelle ist ungültig.");
            }

            var gateway = new IPAddress(
                new byte[]
                {
                    (byte)(gatewayValue & 0xffu),
                    (byte)((gatewayValue >> 8) & 0xffu),
                    (byte)((gatewayValue >> 16) & 0xffu),
                    (byte)((gatewayValue >> 24) & 0xffu)
                });

            if (gateway.Equals(IPAddress.Any) ||
                IPAddress.IsLoopback(gateway))
            {
                throw new InvalidOperationException(
                    "Die ermittelte Docker-Default-Gateway-Adresse ist als vertrauenswürdiger Proxy-Hop unzulässig.");
            }

            gateways.Add(gateway);
        }

        if (gateways.Count != 1)
        {
            throw new InvalidOperationException(
                "Es muss genau ein eindeutiges Docker-Default-Gateway für die sichere Forwarded-Headers-Vertrauensprüfung vorhanden sein.");
        }

        return gateways.Single();
    }
}
