using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface ITunnelCreationNotifier
    {
        ValueTask NotifyStreamTunnelCreationAsync(string userIdentifier, Guid connectionId, IPEndPoint from, CancellationToken cancellationToken = default);
        ValueTask NotifyPacketTunnelCreationAsync(string userIdentifier, Guid connectionId, IPEndPoint from, CancellationToken cancellationToken = default);
    }
}
