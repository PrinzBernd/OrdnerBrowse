using System.Security.Cryptography;

namespace WebUI.Web.Services;

public sealed class ProxyGuardSettings : IDisposable
{
    public const string SectionName = "WebUi:ProxyGuard";
    public const string HeaderName = "X-WebUI-Proxy-Guard";
    public const int HeaderLength = 64;

    private readonly byte[] _expectedHeaderBytes;
    private bool _disposed;

    private ProxyGuardSettings(
        bool enabled,
        byte[] expectedHeaderBytes)
    {
        Enabled = enabled;
        _expectedHeaderBytes = expectedHeaderBytes;
    }

    public bool Enabled { get; }

    public static ProxyGuardSettings Load(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = configuration.GetValue<bool>(
            $"{SectionName}:Enabled");

        if (!enabled)
        {
            return new ProxyGuardSettings(
                false,
                []);
        }

        if (!string.IsNullOrWhiteSpace(
                configuration[$"{SectionName}:Secret"]))
        {
            throw new InvalidOperationException(
                "Ein direkter Proxy-Guard-Secretwert in der Konfiguration ist unzulässig; erforderlich ist ausschließlich eine Secretdatei.");
        }

        var configuredPath = configuration[
            $"{SectionName}:SecretFilePath"]?.Trim();

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "Bei aktiviertem Proxy-Guard fehlt der Proxy-Guard-Secretdateipfad.");
        }

        if (!Path.IsPathFullyQualified(configuredPath))
        {
            throw new InvalidOperationException(
                "Der Proxy-Guard-Secretdateipfad muss absolut sein.");
        }

        var secretBytes = ProxyGuardSecretFile.ReadRequired(
            Path.GetFullPath(configuredPath));

        return new ProxyGuardSettings(
            true,
            secretBytes);
    }

    internal bool Matches(ReadOnlySpan<byte> candidate)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(ProxyGuardSettings));
        }

        return candidate.Length == _expectedHeaderBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   candidate,
                   _expectedHeaderBytes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(
            _expectedHeaderBytes);
        _disposed = true;
    }
}

internal static class ProxyGuardSecretFile
{
    public static byte[] ReadRequired(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "Die konfigurierte Proxy-Guard-Secretdatei wurde nicht gefunden.");
        }

        byte[] rawBytes;

        try
        {
            rawBytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Die konfigurierte Proxy-Guard-Secretdatei konnte nicht gelesen werden.",
                exception);
        }

        try
        {
            var contentLength = rawBytes.Length;

            if (contentLength > 0 &&
                rawBytes[contentLength - 1] == (byte)'\n')
            {
                contentLength--;

                if (contentLength > 0 &&
                    rawBytes[contentLength - 1] == (byte)'\r')
                {
                    contentLength--;
                }
            }

            if (contentLength != ProxyGuardSettings.HeaderLength ||
                rawBytes.Length - contentLength > 2)
            {
                throw new InvalidOperationException(
                    "Die Proxy-Guard-Secretdatei muss genau 64 kleine Hex-Zeichen und optional einen abschließenden Zeilenumbruch enthalten.");
            }

            for (var index = 0; index < contentLength; index++)
            {
                var value = rawBytes[index];

                if (!IsLowerHex(value))
                {
                    throw new InvalidOperationException(
                        "Die Proxy-Guard-Secretdatei besitzt nicht das vorgeschriebene Format.");
                }
            }

            return rawBytes.AsSpan(
                0,
                contentLength).ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rawBytes);
        }
    }

    private static bool IsLowerHex(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9' or
               >= (byte)'a' and <= (byte)'f';
    }
}
