using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpEndPointListener : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private IServiceScope scope;
        private IStreamRouter router;
        private IStreamTunnel tunnel;
        private ITunnelCreationNotifier notifier;
        private IEndPointProvider endPointProvider;
        private IStaticRuleProvider staticRuleProvider;
        private ILogger<TcpEndPointListener> logger;
        private TcpEndPointManager manager;
        private IPEndPoint listenerEndPoint;
        private EndpointCollection.EndPoint EndPointInfo;
        private StaticRule StaticRule;

        public TcpEndPointListener(IPEndPoint endPoint, IServiceProvider services)
        {
            this.scope = services.CreateScope();
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.logger = services.GetRequiredService<ILogger<TcpEndPointListener>>();
            this.notifier = services.GetRequiredService<ITunnelCreationNotifier>();
            this.manager = services.GetRequiredService<TcpEndPointManager>();
            this.endPointProvider = services.GetRequiredService<IEndPointProvider>();
            this.staticRuleProvider = services.GetRequiredService<IStaticRuleProvider>();
            this.listenerEndPoint = endPoint;
            _ = StartAsync();
        }

        private async ValueTask StartAsync()
        {
            var EndPointInfo = await endPointProvider.GetActiveEndPointAsync(Protocol.TCP, listenerEndPoint);
            if (EndPointInfo == null)
            {
                logger.LogError("The endpoint info has not been found");
                throw new InvalidOperationException("The endpoint info has not been found");
            }

            if (EndPointInfo.Type == EndpointType.Static)
            {
                StaticRule = await staticRuleProvider.GetStaticRuleByListenerEndPointAsync(Protocol.TCP, listenerEndPoint);
            }

            this.router = scope.ServiceProvider.GetServices<IStreamRouter>().SingleOrDefault(x => x.Id == EndPointInfo.RouterId);
            if (this.router == null)
            {
                logger.LogError($"The router {EndPointInfo.RouterId} has not been registered");
                throw new DllNotFoundException($"The router {EndPointInfo.RouterId} has not been registered");
            }

            this.tunnel = scope.ServiceProvider.GetServices<IStreamTunnel>().SingleOrDefault(x => x.Id == EndPointInfo.TunnelId);
            if (this.tunnel == null)
            {
                logger.LogError($"The tunnel {EndPointInfo.TunnelId} has not been registered");
                throw new DllNotFoundException($"The tunnel {EndPointInfo.TunnelId} has not been registered");
            }

            server = new TcpListener(listenerEndPoint);
            server.Server.ReceiveTimeout = 1000 * 30;
            server.Server.SendTimeout = 1000 * 30;
            server.Start();
            logger.LogInformation($"TCP Endpoint Listener is listening on {listenerEndPoint}...");
            
            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                logger.LogInformation($"TCP Endpoint Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} connected...");
                _ = HandleClientAcceptAsync(client);
            }
        }

        private async ValueTask HandleClientAcceptAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = MemoryPool<byte>.Shared.Rent(router.ExpectedBufferSize);
            logger.LogInformation($"TCP Endpoint Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} routing...");
            var result = await router.RouteAsync(stream, buffer.Memory, (IPEndPoint)server.LocalEndpoint, client.Client.RemoteEndPoint as IPEndPoint);
            if (!result.IsSucceeded)
            {
                logger.LogWarning($"TCP Endpoint Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} route failed.");
                buffer.Dispose();
                client.Close();
                client.Dispose();
            }
            logger.LogInformation($"TCP Endpoint Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} destination is '{result.UserId}'...");
            var tunnelContext = streamTunnelContextFactory.Create(buffer, result.UserId, router, tunnel);
            tunnelContext.HeaderLength = result.HeaderLength;
            logger.LogInformation($"TCP Endpoint Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} creating tunnel, connection id = {tunnelContext.ConnectionId}");
            tunnelContext.RightClient = client;
            if (EndPointInfo.Type == EndpointType.Bridge) // Agent based connection
            {
                logger.LogInformation($"TCP Endpoint Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} notifying '{result.UserId}'...");
                await notifier.NotifyStreamTunnelCreationAsync(result.UserId, tunnelContext.ConnectionId, server.LocalEndpoint as IPEndPoint);
            }
            else // Static connection
            {
                try
                {
                    tunnelContext.LeftClient = new TcpClient();
                    tunnelContext.LeftClient.ReceiveTimeout = 1000 * 30;
                    tunnelContext.LeftClient.SendTimeout = 1000 * 30;
                    // Right: Responser
                    // Left: Requester
                    var destEndpoint = await AddressHelper.ParseAddressAsync(StaticRule.DestinationEndpoint.ToString(), 0);
                    await tunnelContext.LeftClient.ConnectAsync(destEndpoint.Address, destEndpoint.Port);

                    // Start forwarding
                    var concatStream = new ConcatStream();
                    concatStream.Join(tunnelContext.GetHeaderStream(), tunnelContext.RightClient.GetStream());
                    Stream leftStream = tunnelContext.LeftClient.GetStream();
                    if (StaticRule.UnwrapSsl)
                    {
                        var baseStream = tunnelContext.LeftClient.GetStream();
                        var sslStream = new SslStream(baseStream);
                        sslStream.ReadTimeout = baseStream.ReadTimeout;
                        sslStream.WriteTimeout = baseStream.WriteTimeout;
                        await sslStream.AuthenticateAsClientAsync(AddressHelper.TrimPort(StaticRule.DestinationEndpoint.ToString()));
                        leftStream = sslStream;
                    }
                    await Task.WhenAll(new[]
                    {
                        tunnelContext.Tunnel.BackwardAsync(leftStream, tunnelContext.RightClient.GetStream(), tunnelContext).AsTask(),
                        tunnelContext.Tunnel.ForwardAsync(concatStream, leftStream, tunnelContext).AsTask()
                    });
                    
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
                finally
                {
                    tunnelContext.Dispose();
                }
            }
        }

        public void Dispose()
        {
            logger.LogInformation($"TCP Endpoint Listener<{server.LocalEndpoint}>: Stopping...");
            try
            {
                server?.Stop();
            }
            catch { }
            server = null;
            scope?.Dispose();
            scope = null;
        }
    }
}
