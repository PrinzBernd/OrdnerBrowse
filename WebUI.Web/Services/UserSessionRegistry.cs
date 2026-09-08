using System.Collections.Concurrent;

namespace WebUI.Web.Services;

public sealed class UserSessionRegistry
{
    private readonly ConcurrentDictionary<string, byte> _revokedSessions = new(StringComparer.Ordinal);

    public bool IsActive(string? sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId) &&
               !_revokedSessions.ContainsKey(sessionId);
    }

    public void Revoke(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _revokedSessions.TryAdd(sessionId, 0);
        }
    }
}
