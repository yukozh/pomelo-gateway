using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association.Authentication;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateServer : IDisposable
    {
        public const string Version = "0.9.0";

        private ConcurrentDictionary<string, AssociateContext> clients;
        private TcpListener server;
        private IPEndPoint endpoint;
        private IAuthenticator authenticator;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private IServiceProvider services;

        private AssociateServer(IServiceProvider services)
        {
            this.clients = new ConcurrentDictionary<string, AssociateContext>();
            this.services = services;
            this.authenticator = services.GetRequiredService<IAuthenticator>();
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
        }

        public AssociateServer(IPEndPoint endpoint, IServiceProvider services)
            : this(services)
        {
            this.endpoint = endpoint;
        }

        public AssociateServer(int port, IServiceProvider services)
            : this(services)
        {
            this.endpoint = new IPEndPoint(IPAddress.Any, port);
        }

        public void Start()
        {
            server = new TcpListener(endpoint);
            server.Start();
            LoopAcceptAsync();
        }

        public AssociateContext GetAssociateContextByUserIdentifier(string identifier) 
            => clients.ContainsKey(identifier) ? clients[identifier] : null;

        private async ValueTask LoopAcceptAsync()
        {
            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                HandleClientAcceptAsync(client);
            }
        }

        private async ValueTask HandleClientAcceptAsync(TcpClient client, CancellationToken cancellationToken = default)
        {
            using (var context = new AssociateContext(client))
            {
                context.OnDispose += (context) => 
                {
                    if (context.IsAuthenticated)
                    {
                        clients.TryRemove(context.Credential.Identifier, out var _);
                    }
                };

                while (true)
                {
                    try
                    {
                        await context.Stream.ReadExAsync(context.HeaderBuffer, cancellationToken);
                        var operation = (AssociateOpCode)context.HeaderBuffer.Span[0];
                        int length = context.HeaderBuffer.Span[1];
                        await context.Stream.ReadExAsync(context.BodyBuffer, cancellationToken);
                        await HandleOpCommandAsync(operation, context.BodyBuffer.Slice(0, length), context);
                    }
                    catch (IOException ex)
                    {

                    }
                    catch (Exception ex)
                    {

                    }
                    finally
                    {
                        streamTunnelContextFactory.DestroyContextsForUserIdentifier(context.Credential.Identifier);
                    }
                }
            }
        }

        private async ValueTask<bool> HandleOpCommandAsync(
            AssociateOpCode code, 
            Memory<byte> body, 
            AssociateContext context)
        {
            switch (code)
            {
                case AssociateOpCode.Version:
                    await HandleVersionCommandAsync(Version, body, context);
                    break;
                case AssociateOpCode.BasicAuthLogin:
                    await HandleBasicAuthLoginCommandAsync(authenticator, clients, body, context);
                    break;
                case AssociateOpCode.ListStreamRouters:

                    break;
                default:
                    return false;
            }

            return true;
        }

        internal static async ValueTask HandleVersionCommandAsync(
            string version, 
            Memory<byte> buffer, 
            AssociateContext context)
        {
            var _version = version.Split(".").Select(x => Convert.ToByte(x)).ToArray();
            var length = _version.Length;
            context.ResponseBuffer.Span[0] = (byte)length;
            for (var i = 0; i < length; ++i)
            {
                context.ResponseBuffer.Span[i + 1] = _version[i];
            }
            await context.Stream.WriteAsync(context.ResponseBuffer.Slice(0, length + 1));
        }

        internal static async ValueTask HandleBasicAuthLoginCommandAsync(
            IAuthenticator authenticator, 
            ConcurrentDictionary<string, AssociateContext> clients,
            Memory<byte> buffer, 
            AssociateContext context)
        {
            context.Credential = await authenticator.AuthenticateAsync(buffer);
            context.ResponseBuffer.Span[0] = context.Credential.IsSucceeded ? (byte)0x00 : (byte)0x01;
            if (context.Credential.IsSucceeded)
            {
                if (!clients.TryAdd(context.Credential.Identifier, context))
                {
                    throw new AssociateClientConflictException($"Client {context.Credential.Identifier} already connected");
                }
            }
            await context.Stream.WriteAsync(context.ResponseBuffer.Slice(0, 1));
        }

        internal static async ValueTask HandleListStreamRoutersCommandAsync(
            IServiceProvider services, 
            AssociateContext context)
        {
            var routers = services.GetServices<IStreamRouter>();
        }

        public void Dispose()
        {
            server?.Stop();
        }
    }
}
