using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PublicPacketTunnel : IPacketTunnel
    { 
        public PublicPacketTunnel()
        {
        }

        public Guid Id => Guid.Parse("18ed72e7-2cd3-43a3-ab4b-d8b2c8a2a44c");

        public string Name => "Public Packet Tunnel";

        public int ExpectedForwardAppendHeaderLength => 0;
        public int ExpectedBackwardAppendHeaderLength => 0;

        public async ValueTask BackwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, ReceiveResult from, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            await server.SendAsync(buffer, context.LeftEndpoint);
            if (context != null)
            {
                context.LastActionTimeUtc = DateTime.UtcNow;
            }
        }

        public async ValueTask ForwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, ReceiveResult from, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            await server.SendAsync(buffer, context.RightEndpoint);
            if (context != null)
            {
                context.LastActionTimeUtc = DateTime.UtcNow;
            }
        }
    }
}
