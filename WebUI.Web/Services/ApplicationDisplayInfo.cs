namespace WebUI.Web.Services;

public static class ApplicationDisplayInfo
{
    public const string ProductName = "OrdnerBrowse";
    public const string ProductDescription = "eine WebUI für paperless-ngx";
    public const string FullProductName = ProductName + " – " + ProductDescription;
    public const string Version = "09.92.0";
    public const string PreRelease = "";

    private static string _statusText = "Produktionsbetrieb";

    public static string StatusText => _statusText;

    public static string FullVersion =>
        PreRelease.Length == 0
            ? $"v{Version}"
            : $"v{Version}-{PreRelease}";

    public static string StatusAndVersion =>
        $"{StatusText} · {FullVersion}";

    public static void Configure(
        bool isDevelopment,
        bool isMacDesktop,
        bool isArm64Reference,
        bool isAmd64Reference)
    {
        _statusText = isDevelopment
            ? "Entwicklungsumgebung"
            : isMacDesktop
                ? "Mac-Desktop"
                : isArm64Reference
                    ? "ARM64-Referenzumgebung"
                    : isAmd64Reference
                        ? "AMD64-Referenzumgebung"
                        : "Produktionsbetrieb";
    }
}
