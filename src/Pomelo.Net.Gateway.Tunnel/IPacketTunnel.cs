using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface IPacketTunnel
    {
        Guid Id { get; }
        string Name { get; }
        int ExpectedForwardAppendHeaderLength { get; }
        int ExpectedBackwardAppendHeaderLength { get; }
        ValueTask ForwardAsync(
            PomeloUdpClient server,
            ArraySegment<byte> buffer,
            PacketTunnelContext context,
            CancellationToken cancellationToken = default);
        ValueTask BackwardAsync(
            PomeloUdpClient server,
            ArraySegment<byte> buffer,
            PacketTunnelContext context,
            CancellationToken cancellationToken = default);
    }
}
