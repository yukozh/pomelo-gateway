using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Association.Udp;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class ServerSidePacketTunnel : IPacketTunnel
    {
        private IUdpAssociator udpAssociator;
        private PacketTunnelContextFactory packetTunnelContextFactory;

        public ServerSidePacketTunnel(IUdpAssociator udpAssociator, PacketTunnelContextFactory PacketTunnelContextFactory)
        {
            this.udpAssociator = udpAssociator;
            this.packetTunnelContextFactory = PacketTunnelContextFactory;
        }

        public Guid Id => Guid.Parse("9ae9a7ca-f724-4aca-b612-737ee7e9be46");

        public string Name => "Server-side Packet Tunnel";

        public int ExpectedBackwardAppendHeaderLength => 17;
        public int ExpectedForwardAppendHeaderLength => 36;

        public async ValueTask BackwardAsync(PomeloUdpClient leftServer, PomeloUdpClient rightServer, ArraySegment<byte> buffer, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Packet Body |
            // +-----------------+--------------------------+-------------+

            var connectionId = new Guid(buffer.AsMemory().Slice(1, 16).Span);
            context = packetTunnelContextFactory.GetContextByConnectionId(connectionId);
            context.LastActionTimeUtc = DateTime.UtcNow;
            await leftServer.SendAsync(buffer.Slice(ExpectedBackwardAppendHeaderLength), context.LeftEndpoint);
        }

        public async ValueTask ForwardAsync(PomeloUdpClient leftServer, PomeloUdpClient rightServer, ArraySegment<byte> buffer, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-------------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Is IPv6? (1 byte) |
            // +-----------------+--------------------------+-------------+-----+
            // | Server Address (16 bytes, for IPv4 has 12 bytes padding) |
            // +----------------+-------------+---------------------------+
            // | Port (2 bytes) | Packet body |
            // +----------------+-------------+

            buffer[0] = (byte)PacketTunnelOpCode.TunnelToAgent;
            context.LeftEndpoint.Address.TryWriteBytes(new ArraySegment<byte>(buffer.Array!, 18, 16), out var count);
            buffer[17] = count == 16 ? (byte)0x01 : (byte)0x00;
            context.ConnectionId.TryWriteBytes(buffer.AsMemory().Slice(1, 16).Span);
            BitConverter.TryWriteBytes(buffer.AsMemory().Slice(34, 2).Span, (ushort)context.LeftEndpoint.Port);
            var endpoint = udpAssociator.FindEndpointByIdentifier(context.Identifier);
            await rightServer.SendAsync(buffer, endpoint);
            context.LastActionTimeUtc = DateTime.UtcNow;
        }
    }
}
