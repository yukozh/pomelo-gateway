using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Authentication;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateClient : IDisposable
    {
        private TcpClient client;
        private IPEndPoint associateServerEndpoint;
        private IPEndPoint tunnelServerEndpoint;
        private IServiceProvider services;
        private IAuthenticator authenticator;
        private IMappingRuleProvider mappingRuleProvider;
        private ILogger<AssociateClient> logger;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private int retryDelay = 1000; // 1~10s
        private string serverVersion = "Unknown";
        private long token = 0;
        private List<Interface> tunnels;
        private List<Interface> routers;

        public string ServerVersion => serverVersion;
        public long Token => token;
        public IReadOnlyList<Interface> Tunnels => tunnels;
        public IReadOnlyList<Interface> Routers => routers;

        public AssociateClient(
            IPEndPoint associateServerEndpoint, 
            IPEndPoint tunnelServerEndpoint,
            IServiceProvider services)
        {
            this.associateServerEndpoint = associateServerEndpoint;
            this.tunnelServerEndpoint = tunnelServerEndpoint;
            this.services = services;
            this.authenticator = services.GetRequiredService<IAuthenticator>();
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.mappingRuleProvider = services.GetRequiredService<IMappingRuleProvider>();
            this.logger = services.GetRequiredService<ILogger<AssociateClient>>();
            this.tunnels = new List<Interface>();
            this.routers = new List<Interface>();
            this.Reset();
        }

        private bool Reset()
        {
            tunnels.Clear();
            routers.Clear();
            client?.Dispose();
            client = new TcpClient();
            try
            {
                client.Connect(associateServerEndpoint);
                HandshakeAsync(client.GetStream())
                    .ContinueWith(async (task) => await ReceiveNotificationAsync(client));
            }
            catch (SocketException ex)
            {
                logger.LogWarning(ex.ToString());
                return false;
            }

            logger.LogInformation("Associate Client Reset");
            return true;
        }

        private async Task HandshakeAsync(NetworkStream stream)
        {
            try
            {
                logger.LogInformation("Handshaking...");
                using (var buffer = MemoryPool<byte>.Shared.Rent(256))
                {
                    // Handshake
                    await authenticator.SendAuthenticatePacketAsync(stream);

                    // Receive Server Info
                    // +--------------+----------------+----------------+----------------+
                    // | Login Result | Server Version | Stream Routers | Stream Tunnels |
                    // +--------------+----------------+----------------+----------------+

                    // 1. Login Result
                    logger.LogError("Handshake: Sending authentication packet");
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    switch (buffer.Memory.Span[0])
                    {
                        case 0x00:
                            await stream.ReadExAsync(buffer.Memory.Slice(0, 8));
                            token = BitConverter.ToInt64(buffer.Memory.Slice(0, 8).Span);
                            logger.LogInformation($"Handshake: Authenticate succeeded, token = {token}");
                            break;
                        case 0x01:
                            logger.LogError("Handshake failed, invalid credential");
                            throw new AssociateInvalidCredentialException("Credential is invalid");
                        case 0x02:
                            logger.LogError("The current credential is using in another place");
                            throw new AssociateClientConflictException("The current credential is using in another place");
                    }

                    // 2. Server Version
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    await stream.ReadExAsync(buffer.Memory.Slice(1, buffer.Memory.Span[0]));
                    serverVersion = string.Join(
                        '.', 
                        buffer.Memory.Slice(1, buffer.Memory.Span[0]).Span
                            .ToArray()
                            .Select(x => ((int)x)
                            .ToString()));
                    logger.LogInformation($"Pomelo Gateway Server {serverVersion}");

                    // 3. Stream Router List
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    var count = (int)buffer.Memory.Span[0];
                    for (var i = 0; i < count; ++i)
                    {
                        await stream.ReadExAsync(buffer.Memory.Slice(0, 17));
                        await stream.ReadExAsync(buffer.Memory.Slice(17, buffer.Memory.Span[0]));
                        var item = new Interface
                        {
                            Id = new Guid(buffer.Memory.Slice(1, 16).Span),
                            Name = Encoding.UTF8.GetString(buffer.Memory.Slice(17, buffer.Memory.Span[0]).Span)
                        };
                        routers.Add(item);
                        logger.LogInformation($"Server Side Stream Router: name={item.Name}, id={item.Id}");
                    }

                    // 4. Stream Tunnel List
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    count = (int)buffer.Memory.Span[0];
                    for (var i = 0; i < count; ++i)
                    {
                        await stream.ReadExAsync(buffer.Memory.Slice(0, 17));
                        await stream.ReadExAsync(buffer.Memory.Slice(17, buffer.Memory.Span[0]));
                        var item = new Interface
                        {
                            Id = new Guid(buffer.Memory.Slice(1, 16).Span),
                            Name = Encoding.UTF8.GetString(buffer.Memory.Slice(17, buffer.Memory.Span[0]).Span)
                        };
                        tunnels.Add(item);
                        logger.LogInformation($"Server Side Stream Tunnel: name={item.Name}, id={item.Id}");
                    }
                    logger.LogInformation("Handshake finished");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                logger.LogError($"Retry after sleep {retryDelay}ms");
                await Task.Delay(retryDelay);
                retryDelay += 1000;
                if (retryDelay > 10000)
                {
                    retryDelay = 1000;
                }
                Reset();
            }
        }

        private async Task ReceiveNotificationAsync(TcpClient client)
        {
            try
            {
                logger.LogInformation("Begin receiving tunnel creation notifications...");
                var stream = client.GetStream();
                using (var buffer = MemoryPool<byte>.Shared.Rent(256))
                {
                    // Begin Receive Notifications
                    while (true)
                    {
                        // +-------------------+--------------------------+-------------------+
                        // | Protocol (1 byte) | Connection ID (16 bytes) | Is IPv6? (1 byte) | 
                        // +-------------------+-+------------------------+----------+--------+
                        // | From Port (2 bytes) | From Address (4 bytes / 16 bytes) |
                        // +---------------------+-----------------------------------+
                        await stream.ReadExAsync(buffer.Memory.Slice(0, 24));
                        var isIPv6 = buffer.Memory.Span[17] != 0x00;
                        if (isIPv6) // Is IPv6, read more 12 bytes
                        {
                            await stream.ReadExAsync(buffer.Memory.Slice(24, 12));
                        }

                        var from = new IPEndPoint(
                            isIPv6 
                                ? new IPAddress(buffer.Memory.Slice(20, 16).Span) 
                                : new IPAddress(buffer.Memory.Slice(20, 4).Span),
                            BitConverter.ToUInt16(buffer.Memory.Slice(18, 2).Span));

                        if (buffer.Memory.Span[0] == (byte)Protocol.TCP)
                        {
                            // Parse notification body
                            var context = streamTunnelContextFactory.Create(
                                null,
                                authenticator.UserIdentifier,
                                null,
                                FindStreamTunnelById(FindMappingRuleByFromEndpoint(from).LocalTunnelId),
                                new Guid(buffer.Memory.Slice(1, 16).Span));

                            logger.LogInformation($"Tunnel creating, id={context.ConnectionId}, protocol=TCP, from={from}");
                            CreateStreamTunnelAsync(context, from);
                        }
                        else
                        {
                            logger.LogError("Protocol not supported");
                            throw new NotSupportedException();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                logger.LogError($"Retry after sleep {retryDelay}ms");
                await Task.Delay(retryDelay);
                retryDelay += 1000;
                if (retryDelay > 10000)
                {
                    retryDelay = 1000;
                }
                Reset();
            }
        }

        private MappingRule FindMappingRuleByFromEndpoint(IPEndPoint endpoint)
            => mappingRuleProvider.Rules.Single(x => x.RemoteEndpoint.Equals(endpoint));

        private IStreamTunnel FindStreamTunnelById(Guid id)
            => services.GetServices<IStreamTunnel>().Single(x => x.Id == id);

        private async ValueTask CreateStreamTunnelAsync(StreamTunnelContext context, IPEndPoint from)
        {
            try
            {
                logger.LogInformation("Creating stream tunnel...");
                var rule = FindMappingRuleByFromEndpoint(from);
                logger.LogInformation($"[{context.ConnectionId}] Tunnel left={rule.LocalEndpoint}, Tunnel right={from}, Tunnel ID = {rule.LocalTunnelId}");
                context.LeftClient = new TcpClient();
                context.RightClient = new TcpClient();

                // Connect
                logger.LogInformation($"[{context.ConnectionId}] Tunnel connecting...");
                await Task.WhenAll(new[]
                {
                    context.LeftClient.ConnectAsync(rule.LocalEndpoint.Address, rule.LocalEndpoint.Port),
                    context.RightClient.ConnectAsync(tunnelServerEndpoint.Address, tunnelServerEndpoint.Port),
                });

                // Handshake
                logger.LogInformation($"[{context.ConnectionId}] Handshaking...");
                await HandshakeWithTunnelServerAsync(context.RightClient, context.ConnectionId);

                logger.LogInformation($"[{context.ConnectionId}] Forwarding...");
                // Forwarding
                await Task.WhenAll(new[]
                {
                    context.Tunnel.ForwardAsync(context.LeftClient.GetStream(), context.RightClient.GetStream()).AsTask(),
                    context.Tunnel.BackwardAsync(context.RightClient.GetStream(), context.LeftClient.GetStream()).AsTask()
                });
                logger.LogInformation($"[{context.ConnectionId}] Closed");
            }
            catch (Exception ex)
            {
                logger.LogError($"[{context.ConnectionId}] {ex.ToString()}");
                throw;
            }
            finally
            {
                logger.LogInformation($"[{context.ConnectionId}] Disposing...");
                streamTunnelContextFactory.Delete(context.ConnectionId);
                context.Dispose();
                logger.LogInformation($"[{context.ConnectionId}] Disposed...");
            }
        }

        private async ValueTask HandshakeWithTunnelServerAsync(TcpClient client, Guid connectionId)
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(24))
            {
                var stream = client.GetStream();
                // +-----------------+--------------------------+
                // | Token (8 bytes) | Connection ID (16 bytes) |
                // +-----------------+--------------------------+
                BitConverter.TryWriteBytes(buffer.Memory.Slice(0, 8).Span, token);
                connectionId.TryWriteBytes(buffer.Memory.Slice(8, 16).Span);
                await stream.WriteAsync(buffer.Memory.Slice(0, 24));

                // +-----------------+
                // | Result (1 byte) |
                // +-----------------+
                // 0=OK, 1=Failed
                await stream.ReadAsync(buffer.Memory.Slice(0, 1));
                if (buffer.Memory.Span[0] != 0x00)
                {
                    logger.LogError($"[{connectionId}] Invalid Credential, handshake failed");
                    throw new AssociateInvalidCredentialException("Invalid Credential");
                }
            }
        }

        public void Dispose()
        {
            client?.Dispose();
            client = null;
        }
    }
}
