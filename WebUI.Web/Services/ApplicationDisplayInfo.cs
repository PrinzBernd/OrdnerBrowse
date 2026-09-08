namespace WebUI.Web.Services;

public static class ApplicationDisplayInfo
{
    public const string ProductName = "OrdnerBrowse";
    public const string ProductDescription = "eine WebUI für paperless-ngx";
    public const string FullProductName = ProductName + " – " + ProductDescription;
    public const string Version = "09.91.1";
    public const string PreRelease = "";
    public const string DiskStationTestProfileName = "DiskStationTest";

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
        bool isProfileB,
        bool isDiskStationTest)
    {
        _statusText = isDevelopment
            ? "Entwicklungsumgebung"
            : isProfileB
                ? "ARM64-Referenzumgebung"
                : isDiskStationTest
                    ? "AMD64-Referenzumgebung"
                    : "Produktionsbetrieb";
    }
}
