namespace WebUI.Web.Services;

public interface IPaperlessConnectionTokenStore
{
    Task<PaperlessConnectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<PreparedPaperlessToken> PrepareAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task SavePreparedAsync(
        PreparedPaperlessToken preparedToken,
        CancellationToken cancellationToken = default);
}

public sealed record PaperlessConnectionStatus(
    bool IsConfigured);

public sealed record PreparedPaperlessToken(
    string ProtectedToken);
