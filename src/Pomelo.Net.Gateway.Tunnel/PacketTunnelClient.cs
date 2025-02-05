using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Token;
using Pomelo.Net.Gateway.EndpointCollection;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PacketTunnelClient
    {
        private IServiceProvider services;
        private ITokenProvider tokenProvider;
        private IPEndPoint serverEndpoint;
        private ILogger<PacketTunnelClient> logger;
        private DateTime lastHeartBeatTimeUtc;
        private PacketTunnelContextFactory packetTunnelContextFactory;
        private IMappingRuleProvider mappingRuleProvider;
        private int resetDelay = 1000;

        public PomeloUdpClient Client { get; private set; }
        public IPEndPoint ServerEndpoint => serverEndpoint;
        public bool Connected { get; private set; }

        public PacketTunnelClient(IServiceProvider services)
        {
            this.services = services;
            this.logger = services.GetRequiredService<ILogger<PacketTunnelClient>>();
            this.packetTunnelContextFactory = services.GetRequiredService<PacketTunnelContextFactory>();
            this.mappingRuleProvider = services.GetRequiredService<IMappingRuleProvider>();
            this.tokenProvider = services.GetRequiredService<ITokenProvider>();
        }

        public void SetServer(IPEndPoint serverEndpoint)
        {
            this.serverEndpoint = serverEndpoint;
        }

        public void Start()
        {
            if (this.serverEndpoint == null)
            {
                throw new InvalidOperationException("Please call SetServer before start");
            }

            _ = ResetAsync();
        }

        private async ValueTask ResetAsync()
        {
            try
            {
                if (serverEndpoint == null)
                {
                    return;
                }

                logger.LogInformation("Starting packet tunnel client...");
                lastHeartBeatTimeUtc = DateTime.UtcNow;
                Connected = false;
                if (this.Client != null)
                {
                    this.Client.Dispose();
                    this.Client = null;
                }

                if (OperatingSystem.IsWindows())
                {
                    this.Client = new PomeloUdpClient();
                }
                else
                {
                    this.Client = new PomeloUdpClient(serverEndpoint.AddressFamily);
                }
                logger.LogInformation("Send login packet");
                await LoginAsync();
                logger.LogInformation("Start loop to receive packet tunnel operations");
                StartReceiveAsync();
            }
            catch(Exception ex)
            {
                logger.LogError(ex.ToString());
                await Task.Delay(resetDelay);
                resetDelay += 1000;
                if (resetDelay > 10000)
                {
                    resetDelay = 1000;
                }
                logger.LogWarning("Reset Packet Tunnel Client");
                ResetAsync();
            }
        }

        private async ValueTask LoginAsync()
        {
            // +-----------------+-----------------+---------------------+
            // | OpCode (1 byte) | Token (8 bytes) | Identifier in ASCII |
            // +-----------------+-----------------+---------------------+
            var buffer = ArrayPool<byte>.Shared.Rent(256);
            try
            {
                buffer[0] = (byte)PacketTunnelOpCode.Login;
                BitConverter.TryWriteBytes(new ArraySegment<byte>(buffer, 1, 8), tokenProvider.Token);
                Encoding.ASCII.GetBytes(tokenProvider.UserIdentifier, new ArraySegment<byte>(buffer, 9, tokenProvider.UserIdentifier.Length));
                var _buffer = new ArraySegment<byte>(buffer, 0, 9 + tokenProvider.UserIdentifier.Length);
                await this.Client.SendAsync(_buffer, serverEndpoint);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async ValueTask StartReceiveAsync()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(PomeloUdpClient.MaxUDPSize);
            while (true)
            {
                try
                {
                    var info = await Client.ReceiveAsync(buffer);
                    if (!info.RemoteEndPoint.Equals(serverEndpoint))
                    {
                        logger.LogWarning($"Error packet from {info.RemoteEndPoint}, expected {serverEndpoint}");
                    }
                    var op = (PacketTunnelOpCode)buffer[0];

                    logger.LogInformation($"Received packet tunnel operation {op} from {info.RemoteEndPoint}");
                    switch (op)
                    {
                        case PacketTunnelOpCode.Login:
                            HandleLoginCommand(new ArraySegment<byte>(buffer, 0, 2));
                            break;
                        case PacketTunnelOpCode.TunnelToAgent:
                            await HandleTunnelToAgentCommandAsync(new ArraySegment<byte>(buffer, 0, info.ReceivedBytes), info);
                            break;
                        case PacketTunnelOpCode.HeartBeat:
                            HandleHeartBeatCommand();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Connected = false;
                    logger.LogError(ex.ToString());
                    throw;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private async ValueTask LoopHeartBeatAsync()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1);
            buffer[0] = (byte)PacketTunnelOpCode.HeartBeat;
            while (true)
            {
                try
                {
                    logger.LogInformation("Heart Beat");
                    await Client.SendAsync(buffer, serverEndpoint);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
                await Task.Delay(1000 * 30);
            }
        }

        private void HandleLoginCommand(ArraySegment<byte> packet)
        {
            if (packet[1] != 0x00)
            {
                throw new InvalidOperationException("Login failed due to credential is invalid.");
            }
            Connected = true;
            lastHeartBeatTimeUtc = DateTime.UtcNow;
            logger.LogInformation("Packet tunnel client login succeeded.");
            LoopHeartBeatAsync();
        }

        private void HandleHeartBeatCommand()
        {
            lastHeartBeatTimeUtc = DateTime.UtcNow;
        }

        private async ValueTask HandleTunnelToAgentCommandAsync(ArraySegment<byte> buffer, ReceiveResult from)
        {
            // +-----------------+--------------------------+-------------------+
            // | OpCode (1 byte) | Connection ID (16 bytes) | Is IPv6? (1 byte) |
            // +-----------------+--------------------------+-------------+-----+
            // | Server Address (16 bytes, for IPv4 has 12 bytes padding) |
            // +----------------+-------------+---------------------------+
            // | Port (2 bytes) | Packet body |
            // +----------------+-------------+

            try
            {
                var connectionId = new Guid(buffer.Slice(1, 16));
                var isIPv6 = buffer[17] == 0x01;
                var serverEndpoint = new IPEndPoint(
                    new IPAddress(buffer.Slice(18, isIPv6 ? 16 : 4).AsSpan()),
                    BitConverter.ToUInt16(buffer.Slice(34, 2).AsSpan()));
                var rule = mappingRuleProvider.Rules.SingleOrDefault(x => x.RemoteEndpoint.Equals(serverEndpoint.ToString()));
                if (rule == null)
                {
                    logger.LogWarning($"Packet tunnel rule not found: {serverEndpoint}");
                    return;
                }
                var tunnel = FindPacketTunnelById(rule.LocalTunnelId);
                if (tunnel == null)
                {
                    logger.LogWarning($"Local packet tunnel provider {rule.LocalTunnelId} is not found");
                }
                await tunnel.ForwardAsync(null, buffer, from, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                logger.LogWarning("Reset Packet Tunnel Client");
                ResetAsync();
            }
        }

        public IPacketTunnel FindPacketTunnelById(Guid tunnelId)
            => services.GetServices<IPacketTunnel>().SingleOrDefault(x => x.Id == tunnelId);
    }
}
