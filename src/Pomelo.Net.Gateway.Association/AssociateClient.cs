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
using Pomelo.Net.Gateway.Association.Token;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateClient : ITokenProvider, IDisposable
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
        private List<Interface> serverStreamTunnelProviders;
        private List<Interface> serverStreamRouters;
        private bool connected;

        public bool Connected => connected;
        public string ServerVersion => serverVersion;
        public IPEndPoint AssociateServer => associateServerEndpoint;
        public IPEndPoint TunnelServer => tunnelServerEndpoint;
        public long Token => token;
        public IReadOnlyList<Interface> ServerStreamTunnelProviders => serverStreamTunnelProviders;
        public IReadOnlyList<Interface> ServerStreamRouters => serverStreamRouters;

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
            this.serverStreamTunnelProviders = new List<Interface>();
            this.serverStreamRouters = new List<Interface>();
        }

        public void Start()
        {
            this.HeartBeatAsync();
            this.Reset();
        }

        public async Task SendRulesAsync()
        {
            var stream = client.GetStream();
            using (var buffer = MemoryPool<byte>.Shared.Rent(256))
            {
                foreach (var rule in mappingRuleProvider.Rules)
                {
                    var length = RuleParser.BuildRulePacket(new Endpoint 
                    {
                        IPAddress = rule.RemoteEndpoint.Address,
                        Port = (ushort)rule.RemoteEndpoint.Port,
                        RouterId = rule.RemoteRouterId,
                        TunnelId = rule.RemoteTunnelId,
                        Protocol = rule.Protocol
                    }, buffer.Memory.Slice(2));
                    buffer.Memory.Span[0] = (byte)AssociateOpCode.SetRule;
                    buffer.Memory.Span[1] = (byte)length;
                    await stream.WriteAsync(buffer.Memory.Slice(0, 2 + length));
                }
            }
        }

        public async Task SendCleanRulesAsync()
        {
            var stream = client.GetStream();
            using (var buffer = MemoryPool<byte>.Shared.Rent(2))
            {
                buffer.Memory.Span[0] = (byte)AssociateOpCode.CleanRules;
                buffer.Memory.Span[1] = 0x00;
                await stream.WriteAsync(buffer.Memory.Slice(0, 2));
            }
        }

        public async Task ReloadAndSendRulesAsync()
        {
            var stream = client.GetStream();
            using (var buffer = MemoryPool<byte>.Shared.Rent(2))
            { 
                buffer.Memory.Span[0] = (byte)AssociateOpCode.CleanRules;
                buffer.Memory.Span[1] = 0x00;
                await stream.WriteAsync(buffer.Memory.Slice(0, 2));
            }
            mappingRuleProvider.Reload();
            await SendRulesAsync();
        }

        private async ValueTask HeartBeatAsync()
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(2))
            {
                while (true)
                {
                    try
                    {
                        if (client != null && client.Connected)
                        {
                            var stream = client.GetStream();
                            buffer.Memory.Span[0] = (byte)AssociateOpCode.HeartBeat;
                            buffer.Memory.Span[1] = 0;
                            await stream.WriteAsync(buffer.Memory.Slice(0, 2));
                            logger.LogInformation("Heart beat...");
                        }
                        await Task.Delay(1000 * 30);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("An error occured when sending heart beat packet.");
                        logger.LogError(ex.ToString());
                        await Task.Delay(1000 * 30);
                    }
                }
            }
        }

        private bool Reset()
        {
            connected = false;
            serverStreamTunnelProviders.Clear();
            serverStreamRouters.Clear();
            if (client?.Connected ?? false)
            {
                client?.Close();
            }
            client?.Dispose();
            client = new TcpClient();
            client.ReceiveTimeout = 1000 * 30;
            client.SendTimeout = 1000 * 30;
            try
            {
                client.Connect(associateServerEndpoint);
                Task.Factory.StartNew(async () => 
                {
                    try
                    {
                        await HandshakeAsync();
                        await Task.WhenAll(new[]
                        {
                            ReceiveNotificationAsync(),
                            SendRulesAsync()
                        });
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
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex.ToString());
                logger.LogError($"Retry after sleep {retryDelay}ms");
                Task.Delay(retryDelay).Wait();
                retryDelay += 1000;
                if (retryDelay > 10000)
                {
                    retryDelay = 1000;
                }
                Reset();
            }

            logger.LogInformation("Associate Client Reset");
            return true;
        }

        private async Task HandshakeAsync()
        {
            var stream = client.GetStream();
            logger.LogInformation($"Connected to associate server {associateServerEndpoint}");
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
                logger.LogInformation("Handshake: Sending authentication packet");
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
                    serverStreamRouters.Add(item);
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
                    serverStreamTunnelProviders.Add(item);
                    logger.LogInformation($"Server Side Stream Tunnel: name={item.Name}, id={item.Id}");
                }
                logger.LogInformation("Handshake finished");
            }
        }

        private async Task ReceiveNotificationAsync()
        {
            logger.LogInformation("Begin receiving tunnel creation notifications...");
            var stream = client.GetStream();
            using (var buffer = MemoryPool<byte>.Shared.Rent(256))
            {
                connected = true;

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
                context.LeftClient.ReceiveTimeout = 1000 * 30;
                context.LeftClient.SendTimeout = 1000 * 30;
                context.RightClient = new TcpClient();
                context.RightClient.ReceiveTimeout = 1000 * 30;
                context.RightClient.SendTimeout = 1000 * 30;

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

                // Forwarding
                logger.LogInformation($"[{context.ConnectionId}] Forwarding...");
                await Task.WhenAll(new[]
                {
                    context.Tunnel.ForwardAsync(context.LeftClient.GetStream(), context.RightClient.GetStream(), context).AsTask(),
                    context.Tunnel.BackwardAsync(context.RightClient.GetStream(), context.LeftClient.GetStream(), context).AsTask()
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
                streamTunnelContextFactory.DestroyContext(context.ConnectionId);
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
            if (client?.Connected ?? false)
            {
                client?.Close();
            }
            client?.Dispose();
            client = null;
        }
    }
}
