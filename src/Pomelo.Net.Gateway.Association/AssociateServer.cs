using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Authentication;
using Pomelo.Net.Gateway.Association.Token;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateServer : ITunnelCreationNotifier, ITokenValidator, IDisposable 
    {
        public const string Version = "0.9.0";

        private ConcurrentDictionary<string, AssociateContext> clients;
        private TcpListener server;
        private IPEndPoint endpoint;
        private IAuthenticator authenticator;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private TcpEndpointManager tcpEndpointManager;
        private ILogger<AssociateServer> logger;
        private IServiceProvider services;

        private AssociateServer(IServiceProvider services)
        {
            this.clients = new ConcurrentDictionary<string, AssociateContext>();
            this.services = services;
            this.authenticator = services.GetRequiredService<IAuthenticator>();
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.tcpEndpointManager = services.GetRequiredService<TcpEndpointManager>();
            this.logger = services.GetRequiredService<ILogger<AssociateServer>>();
        }

        public AssociateServer(IPEndPoint endpoint, IServiceProvider services)
            : this(services)
        {
            this.endpoint = endpoint;
        }

        public void Start()
        {
            logger.LogInformation("Starting associate server...");
            server = new TcpListener(endpoint);
            server.Start();
            logger.LogInformation($"Associate server is listening on {endpoint}...");
            LoopAcceptAsync();
        }

        public AssociateContext GetAssociateContextByUserIdentifier(string identifier) 
            => clients.ContainsKey(identifier) ? clients[identifier] : null;

        private async ValueTask LoopAcceptAsync()
        {
            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                logger.LogInformation($"Accepted client from {client.Client.RemoteEndPoint}");
                HandleClientAcceptAsync(client);
            }
        }

        private async ValueTask HandleClientAcceptAsync(TcpClient client, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var context = new AssociateContext(client))
                {
                    while (true)
                    {
                        try
                        {
                            await context.Stream.ReadExAsync(context.HeaderBuffer, cancellationToken);
                            var operation = (AssociateOpCode)context.HeaderBuffer.Span[0];
                            int length = context.HeaderBuffer.Span[1];
                            await context.Stream.ReadExAsync(context.BodyBuffer.Slice(0, length), cancellationToken);
                            await HandleOpCommandAsync(operation, context.BodyBuffer.Slice(0, length), context);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex.ToString());
                            break;
                        }
                        finally
                        {
                            streamTunnelContextFactory.DestroyContextsForUserIdentifier(context.Credential.Identifier);
                            logger.LogInformation($"User {context.Credential.Identifier} is disconnected, recycled its resources.");
                        }
                    }

                    if (context.Credential.IsSucceeded)
                    {
                        clients.TryRemove(context.Credential.Identifier, out var _);
                    }
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex.ToString());
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        private async ValueTask<bool> HandleOpCommandAsync(
            AssociateOpCode code, 
            Memory<byte> body, 
            AssociateContext context)
        {
            logger.LogInformation($"{context.Client.Client.RemoteEndPoint}: {code.ToString()}");
            switch (code)
            {
                case AssociateOpCode.BasicAuthLogin:
                    // +--------------+----------------+----------------+----------------+
                    // | Login Result | Server Version | Stream Routers | Stream Tunnels |
                    // +--------------+----------------+----------------+----------------+
                    await HandleBasicAuthLoginCommandAsync(authenticator, clients, body, context);
                    await HandleVersionCommandAsync(Version, body, context);
                    await HandleListStreamRoutersCommandAsync(services, context);
                    await HandleListStreamTunnelsCommandAsync(services, context);
                    break;
                case AssociateOpCode.SetRule:
                    HandleSetRuleCommand(body, tcpEndpointManager, context.Credential.Identifier);
                    break;
                case AssociateOpCode.CleanRules:
                    await HandleCleanRulesCommandAsync(tcpEndpointManager, context.Credential.Identifier);
                    break;
                default:
                    logger.LogInformation($"{context.Client.Client.RemoteEndPoint}: Invalid OpCode");
                    return false;
            }

            return true;
        }

        internal static async ValueTask HandleVersionCommandAsync(
            string version, 
            Memory<byte> buffer, 
            AssociateContext context)
        {
            // +-----------------+---------+
            // | Length (1 byte) | Version |
            // +-----------------+---------+
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
            // +-----------------+-----------------+
            // | Result (1 byte) | Token (8 bytes) |
            // +-----------------+-----------------+
            context.Credential = await authenticator.AuthenticateAsync(buffer);
            context.ResponseBuffer.Span[0] = context.Credential.IsSucceeded ? (byte)0x00 : (byte)0x01;
            if (context.Credential.IsSucceeded)
            {
                if (!clients.TryAdd(context.Credential.Identifier, context))
                {
                    context.ResponseBuffer.Span[0] = 0x02;
                    await context.Stream.WriteAsync(context.ResponseBuffer.Slice(0, 1));
                    throw new AssociateClientConflictException($"Client {context.Credential.Identifier} already connected");
                }
            }
            await context.Stream.WriteAsync(context.ResponseBuffer.Slice(0, 1));

            if (!context.Credential.IsSucceeded)
            {
                throw new AssociateInvalidCredentialException($"Invalid Credential");
            }

            // Send token
            BitConverter.TryWriteBytes(context.ResponseBuffer.Slice(0, 8).Span, context.Credential.Token);
            await context.Stream.WriteAsync(context.ResponseBuffer.Slice(0, 8));
        }

        internal static async ValueTask HandleListStreamRoutersCommandAsync(
            IServiceProvider services, 
            AssociateContext context)
        {
            var routers = services.GetServices<IStreamRouter>();
            using (var buffer = MemoryPool<byte>.Shared.Rent(256))
            {
                // +-----------------------+
                // | Router Count (1 byte) |
                // +-----------------------+

                // Send router count
                buffer.Memory.Span[0] = (byte)routers.Count();
                await context.Stream.WriteAsync(buffer.Memory.Slice(0, 1));

                // Send router info
                foreach (var router in routers)
                {
                    // +----------------------+----------------------+--------------+
                    // | Name Length (1 byte) | Router ID (16 bytes) | Name in UTF8 |
                    // +----------------------+----------------------+--------------+
                    router.Id.TryWriteBytes(buffer.Memory.Slice(1, 16).Span);
                    var length = (byte)Encoding.UTF8.GetBytes(router.Name, buffer.Memory.Slice(17).Span);
                    buffer.Memory.Span[0] = length;
                    await context.Stream.WriteAsync(buffer.Memory.Slice(0, 17 + length));
                }
            }
        }

        internal static async ValueTask HandleListStreamTunnelsCommandAsync(
            IServiceProvider services,
            AssociateContext context)
        {
            var tunnels = services.GetServices<IStreamTunnel>();
            using (var buffer = MemoryPool<byte>.Shared.Rent(256))
            {
                // +-----------------------+
                // | Tunnel Count (1 byte) |
                // +-----------------------+

                // Send tunnel count
                buffer.Memory.Span[0] = (byte)tunnels.Count();
                await context.Stream.WriteAsync(buffer.Memory.Slice(0, 1));

                // Send tunnel info
                foreach (var tunnel in tunnels)
                {
                    // +----------------------+----------------------+--------------+
                    // | Name Length (1 byte) | Tunnel ID (16 bytes) | Name in UTF8 |
                    // +----------------------+----------------------+--------------+
                    tunnel.Id.TryWriteBytes(buffer.Memory.Slice(1, 16).Span);
                    var length = (byte)Encoding.UTF8.GetBytes(tunnel.Name, buffer.Memory.Slice(17).Span);
                    buffer.Memory.Span[0] = length;
                    await context.Stream.WriteAsync(buffer.Memory.Slice(0, 17 + length));
                }
            }
        }

        internal static void HandleSetRuleCommand(
            Memory<byte> body, 
            TcpEndpointManager tcpEndpointManager, 
            string userIdentifier)
        {
            var endpoint = RuleParser.ParseRulePacket(body);
            if (endpoint.Protocol == Protocol.UDP)
            {
                throw new NotSupportedException();
            }
            tcpEndpointManager.GetOrCreateListenerForEndpoint(
                new IPEndPoint(endpoint.IPAddress, endpoint.Port), 
                endpoint.RouterId, 
                endpoint.TunnelId, 
                userIdentifier);
        }

        internal static async ValueTask HandleCleanRulesCommandAsync(
            TcpEndpointManager tcpEndpointManager, 
            string userIdentifier)
        {
            await tcpEndpointManager.RemoveAllRulesFromUserIdentifierAsync(userIdentifier);
        }

        public void Dispose()
        {
            server?.Stop();
        }

        public virtual async ValueTask NotifyStreamTunnelCreationAsync(
            string userIdentifier, 
            Guid connectionId, 
            IPEndPoint from, 
            CancellationToken cancellationToken = default)
        {
            // +-------------------+--------------------------+-------------------+
            // | Protocol (1 byte) | Connection ID (16 bytes) | Is IPv6? (1 byte) | 
            // +-------------------+-+------------------------+----------+--------+
            // | From Port (2 bytes) | From Address (4 bytes / 16 bytes) |
            // +---------------------+-----------------------------------+
            using (var buffer = MemoryPool<byte>.Shared.Rent(36))
            {
                var context = this.GetAssociateContextByUserIdentifier(userIdentifier);
                var stream = context.Client.GetStream();
                buffer.Memory.Span[0] = (byte)Protocol.TCP;
                connectionId.TryWriteBytes(buffer.Memory.Slice(1, 16).Span);
                BitConverter.TryWriteBytes(buffer.Memory.Slice(18, 2).Span, (ushort)from.Port);
                from.Address.TryWriteBytes(buffer.Memory.Slice(20).Span, out var length);
                buffer.Memory.Span[17] = length == 4 ? (byte)0x00 : (byte)0x01;
                await stream.WriteAsync(buffer.Memory.Slice(0, 20 + length), cancellationToken);
            }
        }

        public virtual ValueTask NotifyPacketTunnelCreationAsync(
            string userIdentifier, 
            Guid connectionId, 
            IPEndPoint from, 
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public virtual async ValueTask<bool> ValidateAsync(long token, string userIdentifier)
        {
            var context = this.GetAssociateContextByUserIdentifier(userIdentifier);
            if (context == null)
            {
                return false;
            }
            return context.Credential.Token == token;
        }
    }
}
