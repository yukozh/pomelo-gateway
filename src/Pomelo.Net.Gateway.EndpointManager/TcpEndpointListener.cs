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
    public class TcpEndpointListener : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private IServiceScope scope;
        private IStreamRouter router;
        private IStreamTunnel tunnel;
        private ITunnelCreationNotifier notifier;
        private ILogger<TcpEndpointListener> logger;
        private TcpEndpointManager manager;

        public TcpEndpointListener(IPEndPoint endpoint, IServiceProvider services)
        {
            this.scope = services.CreateScope();
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.logger = services.GetRequiredService<ILogger<TcpEndpointListener>>();
            this.notifier = services.GetRequiredService<ITunnelCreationNotifier>();
            this.manager = services.GetRequiredService<TcpEndpointManager>();
            var ruleContext = scope.ServiceProvider.GetRequiredService<EndpointContext>();
            var _endpoint = ruleContext.Endpoints.SingleOrDefault(x => x.Address == endpoint.Address.ToString() && x.Protocol == Protocol.TCP && x.Port == endpoint.Port);
            if (_endpoint == null)
            {
                logger.LogError("The endpoint info has not been found");
                throw new InvalidOperationException("The endpoint info has not been found");
            }

            this.router = services.GetServices<IStreamRouter>().SingleOrDefault(x => x.Id == _endpoint.RouterId);
            if (this.router == null)
            {
                logger.LogError($"The router {_endpoint.RouterId} has not been registered");
                throw new DllNotFoundException($"The router {_endpoint.RouterId} has not been registered");
            }

            this.tunnel = services.GetServices<IStreamTunnel>().SingleOrDefault(x => x.Id == _endpoint.TunnelId);
            if (this.tunnel == null)
            {
                logger.LogError($"The tunnel {_endpoint.TunnelId} has not been registered");
                throw new DllNotFoundException($"The tunnel {_endpoint.TunnelId} has not been registered");
            }

            server = new TcpListener(endpoint);
            server.Server.ReceiveTimeout = 1000 * 30;
            server.Server.SendTimeout = 1000 * 30;
            server.Start();
            logger.LogInformation($"TCP Endpoitn Listener is listening on {endpoint}...");
            StartAcceptAsync();
        }

        private async ValueTask StartAcceptAsync()
        {
            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} connected...");
                HandleClientAcceptAsync(client);
            }
        }

        private async ValueTask HandleClientAcceptAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = MemoryPool<byte>.Shared.Rent(router.ExpectedBufferSize);
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} routing...");
            var result = await router.DetermineIdentifierAsync(stream, buffer.Memory, client.Client.RemoteEndPoint as IPEndPoint);
            if (!result.IsSucceeded)
            {
                logger.LogWarning($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} route failed.");
                buffer.Dispose();
                client.Close();
                client.Dispose();
            }
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} destination is '{result.Identifier}'...");
            var tunnelContext = streamTunnelContextFactory.Create(buffer, result.Identifier, router, tunnel);
            tunnelContext.HeaderLength = result.HeaderLength;
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} creating tunnel, connection id = {tunnelContext.ConnectionId}");
            tunnelContext.RightClient = client;
            var user = await manager.GetEndpointUserByIdentifierAsync(result.Identifier);
            if (user.Type == EndpointUserType.NonPublic) // Agent based connection
            {
                logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} notifying '{result.Identifier}'...");
                await notifier.NotifyStreamTunnelCreationAsync(result.Identifier, tunnelContext.ConnectionId, server.LocalEndpoint as IPEndPoint);
            }
            else // Public connection
            {
                try
                {
                    var preCreateEndpoint = await manager.GetPreCreateEndpointByIdentifierAsync(result.Identifier);
                    tunnelContext.LeftClient = new TcpClient();
                    tunnelContext.LeftClient.ReceiveTimeout = 1000 * 30;
                    tunnelContext.LeftClient.SendTimeout = 1000 * 30;
                    // Right: Tunnel <-> Client
                    // Left: Destination Server <-> Tunnel
                    var destEndpoint = await AddressHelper.ParseAddressAsync(preCreateEndpoint.DestinationEndpoint, 0);
                    await tunnelContext.LeftClient.ConnectAsync(destEndpoint.Address, destEndpoint.Port);

                    // Start forwarding
                    var concatStream = new ConcatStream();
                    concatStream.Join(tunnelContext.GetHeaderStream(), tunnelContext.RightClient.GetStream());
                    Stream leftStream = tunnelContext.LeftClient.GetStream();
                    if (preCreateEndpoint.DestinationWithSSL)
                    {
                        var baseStream = tunnelContext.LeftClient.GetStream();
                        var sslStream = new SslStream(baseStream);
                        sslStream.ReadTimeout = baseStream.ReadTimeout;
                        sslStream.WriteTimeout = baseStream.WriteTimeout;
                        await sslStream.AuthenticateAsClientAsync(AddressHelper.TrimPort(preCreateEndpoint.DestinationEndpoint));
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
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: Stopping...");
            server?.Stop();
            server = null;
            scope?.Dispose();
            scope = null;
        }
    }
}
