using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface ITunnelCreationNotifier
    {
        ValueTask NotifyStreamTunnelCreationAsync(string userIdentifier, Guid connectionId, CancellationToken cancellationToken = default);
        ValueTask NotifyPacketTunnelCreationAsync(string userIdentifier, Guid connectionId, CancellationToken cancellationToken = default);
    }
}
