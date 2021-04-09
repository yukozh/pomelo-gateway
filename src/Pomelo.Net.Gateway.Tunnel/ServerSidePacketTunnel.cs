using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Token;
using Pomelo.Net.Gateway.Association.Udp;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class ServerSidePacketTunnel : IPacketTunnel
    {
        private IUdpAssociator udpAssociator;
        private ITokenValidator tokenValidator;
        private ILogger<ServerSidePacketTunnel> logger;
        private PacketTunnelContextFactory packetTunnelContextFactory;

        public ServerSidePacketTunnel(
            IUdpAssociator udpAssociator,
            PacketTunnelContextFactory PacketTunnelContextFactory,
            ITokenValidator tokenValidator,
            ILogger<ServerSidePacketTunnel> logger)
        {
            this.udpAssociator = udpAssociator;
            this.packetTunnelContextFactory = PacketTunnelContextFactory;
            this.tokenValidator = tokenValidator;
            this.logger = logger;
        }

        public Guid Id => Guid.Parse("9ae9a7ca-f724-4aca-b612-737ee7e9be46");

        public string Name => "Server-side Packet Tunnel";

        public int ExpectedBackwardAppendHeaderLength => 25;
        public int ExpectedForwardAppendHeaderLength => 36;

        public async ValueTask BackwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, ReceiveResult from, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-----------------+-------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Token (8 bytes) | Packet Body |
            // +-----------------+--------------------------+-----------------+-------------+

            var connectionId = new Guid(buffer.AsMemory().Slice(1, 16).Span);
            context = packetTunnelContextFactory.GetContextByConnectionId(connectionId);
            var token = BitConverter.ToInt64(buffer.Slice(17, 8));
            if (!await tokenValidator.ValidateAsync(token, context.Identifier))
            {
                logger.LogInformation($"Token from {connectionId} is invalid");
                return;
            }
            await server.SendAsync(buffer.Slice(ExpectedBackwardAppendHeaderLength), context.LeftEndpoint);
            context.LastActionTimeUtc = DateTime.UtcNow;
        }

        public async ValueTask ForwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, ReceiveResult from, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-------------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Is IPv6? (1 byte) |
            // +-----------------+--------------------------+-------------+-----+
            // | Server Address (16 bytes, for IPv4 has 12 bytes padding) |
            // +----------------+-------------+---------------------------+
            // | Port (2 bytes) | Packet body |
            // +----------------+-------------+

            buffer[0] = (byte)PacketTunnelOpCode.TunnelToAgent;
            context.EntryEndpoint.Address.TryWriteBytes(new ArraySegment<byte>(buffer.Array!, 18, 16), out var count);
            buffer[17] = count == 16 ? (byte)0x01 : (byte)0x00;
            context.ConnectionId.TryWriteBytes(buffer.AsMemory().Slice(1, 16).Span);
            BitConverter.TryWriteBytes(buffer.AsMemory().Slice(34, 2).Span, (ushort)context.EntryEndpoint.Port);
            var endpoint = udpAssociator.FindEndpointByIdentifier(context.Identifier);
            await server.SendAsync(buffer, endpoint);
            context.LastActionTimeUtc = DateTime.UtcNow;
        }
    }
}
