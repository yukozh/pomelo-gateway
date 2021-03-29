using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpPortListner : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private IStreamRouter router;
        private IStreamTunnel tunnel;
        private ITunnelCreationNotifier notifier;

        public TcpPortListner(IPEndPoint endpoint, IServiceProvider services, StreamTunnelContextFactory streamTunnelContextFactory)
        {
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            var ruleContext = services.GetRequiredService<RuleContext>();
            var _endpoint = ruleContext.Endpoints.SingleOrDefault(x => x.Address == endpoint.Address.ToString() && x.Protocol == Protocol.TCP && x.Port == endpoint.Port);
            if (_endpoint == null)
            {
                throw new InvalidOperationException("The endpoint info has not been found");
            }

            this.router = services.GetServices<IStreamRouter>().SingleOrDefault(x => x.Id == _endpoint.RouterId);
            if (this.router == null)
            {
                throw new DllNotFoundException($"The router {_endpoint.RouterId} has not been registered");
            }

            this.tunnel = services.GetServices<IStreamTunnel>().SingleOrDefault(x => x.Id == _endpoint.TunnelId);
            if (this.tunnel == null)
            {
                throw new DllNotFoundException($"The tunnel {_endpoint.TunnelId} has not been registered");
            }

            server = new TcpListener(endpoint);
            server.Start();
            StartAcceptAsync();
        }

        private async ValueTask StartAcceptAsync()
        {
            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                HandleClientAcceptAsync(client);
            }
        }

        private async ValueTask HandleClientAcceptAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = MemoryPool<byte>.Shared.Rent(router.ExpectedBufferSize);
            var result = await router.DetermineIdentifierAsync(stream, buffer.Memory, server.LocalEndpoint as IPEndPoint);
            if (!result.IsSucceeded)
            {
                buffer.Dispose();
                client.Close();
                client.Dispose();
            }
            var tunnelContext = streamTunnelContextFactory.Create(buffer, result.Identifier, router, tunnel);
            tunnelContext.RightClient = client;
            await notifier.NotifyStreamTunnelCreationAsync(result.Identifier, tunnelContext.ConnectionId, server.LocalEndpoint as IPEndPoint);
        }

        public void Dispose()
        {
            server?.Stop();
        }
    }
}
