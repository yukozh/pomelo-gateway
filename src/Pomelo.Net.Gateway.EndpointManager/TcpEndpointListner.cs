using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpEndpointListner : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private IServiceScope scope;
        private IStreamRouter router;
        private IStreamTunnel tunnel;
        private ITunnelCreationNotifier notifier;
        private ILogger<TcpEndpointListner> logger;

        public TcpEndpointListner(IPEndPoint endpoint, IServiceProvider services)
        {
            this.scope = services.CreateScope();
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.logger = services.GetRequiredService<ILogger<TcpEndpointListner>>();
            this.notifier = services.GetRequiredService<ITunnelCreationNotifier>();
            var ruleContext = scope.ServiceProvider.GetRequiredService<RuleContext>();
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
            var result = await router.DetermineIdentifierAsync(stream, buffer.Memory, server.LocalEndpoint as IPEndPoint);
            if (!result.IsSucceeded)
            {
                logger.LogWarning($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} route failed.");
                buffer.Dispose();
                client.Close();
                client.Dispose();
            }
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} destination is '{result.Identifier}'...");
            var tunnelContext = streamTunnelContextFactory.Create(buffer, result.Identifier, router, tunnel);
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} creating tunnel, connection id = {tunnelContext.ConnectionId}");
            tunnelContext.RightClient = client;
            logger.LogInformation($"TCP Endpoitn Listener<{server.LocalEndpoint}>: {client.Client.RemoteEndPoint} notifying '{result.Identifier}'...");
            await notifier.NotifyStreamTunnelCreationAsync(result.Identifier, tunnelContext.ConnectionId, server.LocalEndpoint as IPEndPoint);
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
