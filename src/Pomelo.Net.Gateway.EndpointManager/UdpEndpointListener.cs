using System;
using System.Buffers;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class UdpEndpointListener : IDisposable
    {
        private PomeloUdpClient server;
        private PacketTunnelContextFactory packetTunnelContextFactory;
        private IPacketRouter router;
        private IPacketTunnel tunnel;
        private ILogger<UdpEndpointListener> logger;
        private PacketTunnelServer tunnelServer;
        private UdpEndpointManager manager;

        public IPEndPoint Endpoint { get; private set; }

        public UdpEndpointListener(
            IPEndPoint endpoint,
            Guid RouterId,
            Guid TunnelId,
            IServiceProvider services)
        {
            Endpoint = endpoint;
            packetTunnelContextFactory = services.GetRequiredService<PacketTunnelContextFactory>();
            logger = services.GetRequiredService<ILogger<UdpEndpointListener>>();
            tunnelServer = services.GetRequiredService<PacketTunnelServer>();
            manager = services.GetRequiredService<UdpEndpointManager>();
            server = new PomeloUdpClient(endpoint);
        }

        private async ValueTask StartAsync()
        {
            var buffer = new byte[PomeloUdpClient.MaxUDPSize];
            while (true)
            {
                try
                {
                    var info = await server.ReceiveAsync(new ArraySegment<byte>(buffer, tunnel.ExpectedForwardAppendHeaderLength, buffer.Length - tunnel.ExpectedForwardAppendHeaderLength));
                    var identifier = await router.DetermineIdentifierAsync(new ArraySegment<byte>(buffer, tunnel.ExpectedForwardAppendHeaderLength, info.ReceivedBytes), Endpoint);
                    if (identifier == null)
                    {
                        logger.LogWarning($"No available destination found for UDP Listener {Endpoint}");
                        continue;
                    }

                    var context = packetTunnelContextFactory.GetOrCreateContext(identifier, info.RemoteEndPoint);
                    context.LeftEndpoint = info.RemoteEndPoint;
                    var user = await manager.GetEndpointUserByIdentifierAsync(identifier);

                    if (user.Type == EndpointCollection.EndpointUserType.NonPublic)
                    {
                        await tunnel.ForwardAsync(
                            tunnelServer.Server,
                            new ArraySegment<byte>(buffer, 0, info.ReceivedBytes + tunnel.ExpectedForwardAppendHeaderLength),
                            context);
                    }
                    else
                    {
                        var preCreateEndpoint = await manager.GetPreCreateEndpointByIdentifierAsync(identifier);
                        context.Client = new PomeloUdpClient();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
            }
        }

        public void Dispose()
        {
            server?.Dispose();
            server = null;
        }
    }
}
