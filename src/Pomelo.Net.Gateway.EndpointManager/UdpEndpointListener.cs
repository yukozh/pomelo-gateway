using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class UdpEndPointListener : IDisposable
    {
        private IServiceProvider services;
        private PomeloUdpClient server;
        private PacketTunnelContextFactory packetTunnelContextFactory;
        private IPacketRouter router;
        private IPacketTunnel tunnel;
        private ILogger<UdpEndPointListener> logger;
        private PacketTunnelServer tunnelServer;
        private UdpEndPointManager manager;
        private IEndPointProvider endPointProvider;
        private IStaticRuleProvider staticRuleProvider;
        private EndpointCollection.EndPoint endPointInfo;
        private StaticRule staticRule;

        public IPEndPoint Endpoint { get; private set; }
        public PomeloUdpClient Server => server;

        public UdpEndPointListener(
            IPEndPoint endpoint,
            Guid routerId,
            Guid tunnelId,
            IServiceProvider services)
        {
            Endpoint = endpoint;
            this.services = services;
            packetTunnelContextFactory = services.GetRequiredService<PacketTunnelContextFactory>();
            logger = services.GetRequiredService<ILogger<UdpEndPointListener>>();
            tunnelServer = services.GetRequiredService<PacketTunnelServer>();
            manager = services.GetRequiredService<UdpEndPointManager>();
            server = new PomeloUdpClient(endpoint);
            router = FindRouterById(routerId);
            tunnel = FindTunnelById(tunnelId);
            endPointProvider = services.GetRequiredService<IEndPointProvider>();
            staticRuleProvider = services.GetRequiredService<IStaticRuleProvider>();
            _ = StartAsync();
            logger.LogInformation($"UDP Endpoint {endpoint} started");
        }

        private IPacketRouter FindRouterById(Guid id)
            => services.GetServices<IPacketRouter>()
                .SingleOrDefault(x => x.Id == id);

        private IPacketTunnel FindTunnelById(Guid id)
            => services.GetServices<IPacketTunnel>()
                .SingleOrDefault(x => x.Id == id);

        private async ValueTask StartAsync()
        {
            var buffer = new byte[PomeloUdpClient.MaxUDPSize];
            try
            {
                endPointInfo = await endPointProvider.GetActiveEndPointAsync(Protocol.UDP, Endpoint);
                if (endPointInfo.Type == EndpointType.Static)
                {
                    staticRule = await staticRuleProvider.GetStaticRuleByListenerEndPointAsync(Protocol.UDP, Endpoint);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }

            while (true)
            {
                try
                {
                    //logger.LogInformation("Waiting for client packet...");
                    var info = await server.ReceiveAsync(new ArraySegment<byte>(buffer, tunnel.ExpectedForwardAppendHeaderLength, buffer.Length - tunnel.ExpectedForwardAppendHeaderLength));

                    //logger.LogInformation("Received for client packet...");
                    var _buffer = new ArraySegment<byte>(buffer, tunnel.ExpectedForwardAppendHeaderLength, info.ReceivedBytes);
                    var identifier = "admin";
                    if (identifier == null)
                    {
                        logger.LogWarning($"No available destination found for UDP Listener {Endpoint}");
                        //continue;
                    }

                    var context = packetTunnelContextFactory.GetOrCreateContext(identifier, info.RemoteEndPoint, tunnel.Id);
                    context.LeftEndpoint = info.RemoteEndPoint;
                    context.RightEndpoint = Endpoint;
                    context.EntryEndpoint = Endpoint;

                    if (endPointInfo.Type == EndpointType.Bridge)
                    {
                        logger.LogInformation("Forwarding packet...");
                        // No need to notify
                        await tunnel.ForwardAsync(
                            tunnelServer.Server,
                            new ArraySegment<byte>(buffer, 0, info.ReceivedBytes + tunnel.ExpectedForwardAppendHeaderLength),
                            info,
                            context);
                    }
                    else
                    {
                        if (context.Client == null)
                        {
                            logger.LogInformation($"The context is new created, preparing pair udp socket for {context.ConnectionId}");
                            context.RightEndpoint = await AddressHelper.ParseAddressAsync(staticRule.DestinationEndpoint.ToString(), 0);
                            logger.LogInformation($"The destination of {context.ConnectionId} is {context.RightEndpoint}");
                            if (OperatingSystem.IsWindows())
                            {
                                context.Client = new PomeloUdpClient();
                            }
                            else
                            {
                                var destinationEndpoint = await AddressHelper.ParseAddressAsync(staticRule.DestinationEndpoint.ToString(), 0);
                                context.Client = new PomeloUdpClient(destinationEndpoint.AddressFamily);
                            }
                            _ = Task.Run(async ()=>
                            {
                                logger.LogInformation($"Created UDP client for {context.ConnectionId}");
                                var buffer = ArrayPool<byte>.Shared.Rent(PomeloUdpClient.MaxUDPSize);
                                while (true)
                                {
                                    try
                                    {
                                        var info = await context.Client.ReceiveAsync(new ArraySegment<byte>(buffer));
                                        await tunnel.BackwardAsync(
                                            tunnelServer.Server, new ArraySegment<byte>(buffer, tunnel.ExpectedBackwardAppendHeaderLength, tunnel.ExpectedBackwardAppendHeaderLength + info.ReceivedBytes),
                                            info,
                                            context);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex.ToString());
                                        packetTunnelContextFactory.DestroyContext(context.ConnectionId);
                                        ArrayPool<byte>.Shared.Return(buffer);
                                        throw;
                                    }
                                }
                            });
                        }
                        await tunnel.ForwardAsync(context.Client, _buffer, info, context);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                    throw;
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
