using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Association.Token;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class AgentSidePacketTunnel : IPacketTunnel
    {
        private PacketTunnelContextFactory packetTunnelContextFactory;
        private IMappingRuleProvider mappingRuleProvider;
        private ITokenProvider tokenProvider;
        private IPacketTunnelServerAddressProvider packetTunnelServerAddressProvider;
        private ILogger<AgentSidePacketTunnel> logger;
        private PacketTunnelClient packetTunnelClient;

        public AgentSidePacketTunnel(
            PacketTunnelContextFactory PacketTunnelContextFactory,
            IMappingRuleProvider mappingRuleProvider,
            ITokenProvider tokenProvider,
            IPacketTunnelServerAddressProvider packetTunnelServerAddressProvider,
            PacketTunnelClient packetTunnelClient,
            ILogger<AgentSidePacketTunnel> logger)
        {
            this.packetTunnelContextFactory = PacketTunnelContextFactory;
            this.mappingRuleProvider = mappingRuleProvider;
            this.tokenProvider = tokenProvider;
            this.packetTunnelServerAddressProvider = packetTunnelServerAddressProvider;
            this.packetTunnelClient = packetTunnelClient;
            this.logger = logger;
        }

        public Guid Id => Guid.Parse("9ae9a7ca-f724-4aca-b612-737ee7e9be47");

        public string Name => "Agent-side Packet Tunnel";

        public int ExpectedBackwardAppendHeaderLength => 25;
        public int ExpectedForwardAppendHeaderLength => 36;

        public async ValueTask BackwardAsync(PomeloUdpClient server, ArraySegment<byte> buffer, ReceiveResult from, PacketTunnelContext context, CancellationToken cancellationToken = default)
        {
            // +-----------------+--------------------------+-----------------+-------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Token (8 bytes) | Packet Body |
            // +-----------------+--------------------------+-----------------+-------------+

            buffer[0] = (byte)PacketTunnelOpCode.AgentToTunnel;
            context.ConnectionId.TryWriteBytes(buffer.Slice(1, 16));
            BitConverter.TryWriteBytes(buffer.Slice(17, 8), tokenProvider.Token);
            await server.SendAsync(buffer, context.RightEndpoint);
            if (context != null)
            {
                context.LastActionTimeUtc = DateTime.UtcNow;
            }
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

            var connectionId = new Guid(buffer.Slice(1, 16));
            var isIPv6 = buffer[17] == 0x01;
            var serverEndpoint = new IPEndPoint(
                new IPAddress(buffer.Slice(18, isIPv6 ? 16 : 4).AsSpan()),
                BitConverter.ToUInt16(buffer.Slice(34, 2).AsSpan()));
            var rule = mappingRuleProvider.Rules.SingleOrDefault(x => x.RemoteEndpoint.Equals(serverEndpoint));
            if (rule == null)
            {
                logger.LogWarning($"Packet router has not found the destination from server {serverEndpoint}");
                return;
            }
            context = packetTunnelContextFactory.GetOrCreateContext(tokenProvider.UserIdentifier, serverEndpoint);
            if (context.Client == null)
            {
                if (OperatingSystem.IsWindows())
                {
                    context.Client = new PomeloUdpClient();
                }
                else
                {
                    context.Client = new PomeloUdpClient(rule.RemoteEndpoint.AddressFamily);
                }
                server = context.Client;
                context.RightEndpoint = packetTunnelServerAddressProvider.PacketTunnelServerEndpoint;
                context.LeftEndpoint = rule.LocalEndpoint;
                Task.Run(async ()=>
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(PomeloUdpClient.MaxUDPSize);
                    while (true)
                    {
                        try
                        {
                            var info = await context.Client.ReceiveAsync(new ArraySegment<byte>(
                                buffer,
                                ExpectedBackwardAppendHeaderLength,
                                PomeloUdpClient.MaxUDPSize - ExpectedBackwardAppendHeaderLength));
                            await this.BackwardAsync(packetTunnelClient.Client, new ArraySegment<byte>(buffer, 0, info.ReceivedBytes + ExpectedBackwardAppendHeaderLength), info, context);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex.ToString());
                            packetTunnelContextFactory.DestroyContext(context.ConnectionId);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                });
            }
            await server.SendAsync(buffer.Slice(ExpectedForwardAppendHeaderLength), context.LeftEndpoint);
            if (context != null)
            {
                context.LastActionTimeUtc = DateTime.UtcNow;
            }
        }
    }
}
