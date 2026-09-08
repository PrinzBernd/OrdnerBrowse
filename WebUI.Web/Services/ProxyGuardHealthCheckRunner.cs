using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace WebUI.Web.Services;

internal static class ProxyGuardHealthCheckRunner
{
    private const string SecretFilePathEnvironmentName =
        "WebUi__ProxyGuard__SecretFilePath";

    public static async Task<int> RunAsync()
    {
        byte[]? secretBytes = null;

        try
        {
            var configuredPath = Environment.GetEnvironmentVariable(
                SecretFilePathEnvironmentName)?.Trim();

            if (string.IsNullOrWhiteSpace(configuredPath) ||
                !Path.IsPathFullyQualified(configuredPath))
            {
                return 2;
            }

            secretBytes = ProxyGuardSecretFile.ReadRequired(
                Path.GetFullPath(configuredPath));
            var headerValue = Encoding.ASCII.GetString(secretBytes);

            using var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "http://127.0.0.1:8080/healthz");

            request.Headers.TryAddWithoutValidation(
                ProxyGuardSettings.HeaderName,
                headerValue);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            return response.StatusCode == HttpStatusCode.OK
                ? 0
                : 1;
        }
        catch
        {
            return 2;
        }
        finally
        {
            if (secretBytes is not null)
            {
                CryptographicOperations.ZeroMemory(secretBytes);
            }
        }
    }
}
