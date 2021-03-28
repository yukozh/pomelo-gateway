using System;
using System.Buffers;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpPortListner : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private IStreamRouter router;
        private ITunnelCreationNotifier notifier;

        public TcpPortListner(IPEndPoint endpoint, IStreamRouter router, StreamTunnelContextFactory streamTunnelContextFactory)
        {
            this.streamTunnelContextFactory = streamTunnelContextFactory;
            this.router = router;
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
            var tunnel = streamTunnelContextFactory.Create(buffer, result.Identifier);
            tunnel.RightClient = client;
            await notifier.NotifyStreamTunnelCreationAsync(result.Identifier, tunnel.ConnectionId);
        }

        public void Dispose()
        {
            server?.Stop();
        }
    }
}
