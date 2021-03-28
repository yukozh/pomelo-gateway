using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface IStreamTunnel
    {
        Guid Id { get; }
        string Name { get; }
        ValueTask ForwardAsync(Stream leftToTunnelStream, Stream tunnelToRightStream, CancellationToken cancellationToken = default);
        ValueTask BackwardAsync(Stream rightToTunnelStream, Stream tunnelToLeftStream, CancellationToken cancellationToken = default);
    }
}
