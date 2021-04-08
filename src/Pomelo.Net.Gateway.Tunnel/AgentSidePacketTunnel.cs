using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class AgentSidePacketTunnel : IPacketTunnel
    {
        private PacketTunnelContextFactory packetTunnelContextFactory;
        private IMappingRuleProvider mappingRuleProvider;

        public AgentSidePacketTunnel(
            PacketTunnelContextFactory PacketTunnelContextFactory,
            IMappingRuleProvider mappingRuleProvider)
        {
            this.packetTunnelContextFactory = PacketTunnelContextFactory;
            this.mappingRuleProvider = mappingRuleProvider;
        }

        public Guid Id => Guid.Parse("9ae9a7ca-f724-4aca-b612-737ee7e9be47");

        public string Name => "Agent-side Packet Tunnel";

        public int ExpectedBackwardAppendHeaderLength => 17;
        public int ExpectedForwardAppendHeaderLength => 36;

        public async ValueTask BackwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Packet Body |
            // +-----------------+--------------------------+-------------+

            buffer[0] = (byte)PacketTunnelOpCode.AgentToTunnel;
            context.ConnectionId.TryWriteBytes(buffer.Slice(1, 16));
            await server.SendAsync(buffer, context.RightEndpoint);
            if (context != null)
            {
                context.LastActionTimeUtc = DateTime.UtcNow;
            }
        }

        public async ValueTask ForwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-------------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Is IPv6? (1 byte) |
            // +-----------------+--------------------------+-------------+-----+
            // | Server Address (16 bytes, for IPv4 has 12 bytes padding) |
            // +----------------+-------------+---------------------------+
            // | Port (2 bytes) | Packet body |
            // +----------------+-------------+

            var connectionId = new Guid(buffer.AsMemory().Slice(1, 16).Span);
            var isIPv6 = buffer[17] == 0x01;
            var serverEndpoint = new IPEndPoint(
                new IPAddress(buffer.Slice(18, isIPv6 ? 16 : 4).AsSpan()),
                BitConverter.ToUInt16(buffer.Slice(34, 2).AsSpan()));
            var rule = mappingRuleProvider.Rules.SingleOrDefault(x => x.RemoteEndpoint.Equals(serverEndpoint));
            if (rule == null)
            {
                // TODO: logging
                return;
            }
            await server.SendAsync(buffer.Slice(ExpectedForwardAppendHeaderLength), rule.LocalEndpoint);
            if (context != null)
            {
                context.LastActionTimeUtc = DateTime.UtcNow;
            }
        }
    }
}
