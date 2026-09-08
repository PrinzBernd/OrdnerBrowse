using Microsoft.AspNetCore.Components.Server.Circuits;

namespace WebUI.Web.Services;

public sealed class CircuitConnectionState : CircuitHandler
{
    private int _isConnected = 1;

    public bool IsConnected => Volatile.Read(ref _isConnected) == 1;

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Volatile.Write(ref _isConnected, 1);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Volatile.Write(ref _isConnected, 0);
        return Task.CompletedTask;
    }
}
