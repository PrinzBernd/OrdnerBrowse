namespace WebUI.Web.Services;

public sealed record LocalTestUserDefinition(
    string Alias)
{
    public string DisplayName => $"Testbenutzer {Alias}";

    public string KeychainAccount =>
        $"local-test-user-{Alias.ToLowerInvariant()}";
}

public sealed class LocalTestUserSettings
{
    public const string AuthenticationScheme = "LocalTestCookie";
    public const string AliasClaimType = "webui/local-test-alias";
    public const string TechnicalUserKeyClaimType = "webui/local-technical-user-key";
    public const string SessionIdClaimType = "webui/local-session-id";

    private static readonly IReadOnlyDictionary<string, LocalTestUserDefinition> Users =
        new Dictionary<string, LocalTestUserDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new("A"),
            ["B"] = new("B"),
            ["C"] = new("C"),
            ["D"] = new("D")
        };

    public static bool IsEnabled(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        bool oidcEnabled) =>
        !oidcEnabled &&
        environment.IsDevelopment() &&
        configuration.GetValue<bool>("LocalMultiUser:Enabled");

    public static IReadOnlyCollection<LocalTestUserDefinition> GetUsers() =>
        Users.Values.OrderBy(user => user.Alias).ToArray();

    public static bool TryGetUser(
        string? alias,
        out LocalTestUserDefinition user)
    {
        if (!string.IsNullOrWhiteSpace(alias) &&
            Users.TryGetValue(alias.Trim(), out var found))
        {
            user = found;
            return true;
        }

        user = null!;
        return false;
    }
}
