﻿using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Token;
using Pomelo.Net.Gateway.Association.Udp;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PacketTunnelServer : IDisposable
    {
        private PomeloUdpClient server;
        private IPEndPoint endpoint;
        private ILogger<PacketTunnelServer> logger;
        private ITokenValidator tokenValidator;
        private IUdpAssociator udpAssociator;
        private PacketTunnelContextFactory packetTunnelContextFactory;
        private IUdpEndpointListenerFinder udpEndpointListenerFinder;
        private IServiceProvider services;
        private CancellationTokenSource loopCancellationToken;

        public PomeloUdpClient Server => server;

        public PacketTunnelServer(IServiceProvider services)
        {
            this.services = services;
            this.logger = services.GetRequiredService<ILogger<PacketTunnelServer>>();
            this.udpAssociator = services.GetRequiredService<IUdpAssociator>();
            this.tokenValidator = services.GetRequiredService<ITokenValidator>();
            this.packetTunnelContextFactory = services.GetRequiredService<PacketTunnelContextFactory>();
            this.udpEndpointListenerFinder = services.GetRequiredService<IUdpEndpointListenerFinder>();
        }

        public void Stop()
        {
            server?.Dispose();
            loopCancellationToken?.Cancel();
            loopCancellationToken?.Dispose();
            server = null;
        }

        public void Start(IPEndPoint tunnelServerEndpoint)
        {
            Stop();
            server = new PomeloUdpClient(endpoint);
            this.endpoint = tunnelServerEndpoint;
            logger.LogInformation($"Packet Tunnel Server is listening on {endpoint}...");
            loopCancellationToken = new CancellationTokenSource();
            _ = StartAsync(loopCancellationToken.Token);
        }

        private async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(PomeloUdpClient.MaxUDPSize);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var info = await server.ReceiveAsync(buffer);
                    var op = (PacketTunnelOpCode)buffer[0];
                    logger.LogInformation($"{info.RemoteEndPoint}: {op}");
                    switch (op)
                    {
                        case PacketTunnelOpCode.Login:
                            await HandleLoginCommandAsync(buffer.AsMemory().Slice(1, info.ReceivedBytes - 1), info.RemoteEndPoint);
                            break;
                        case PacketTunnelOpCode.AgentToTunnel:
                            await HandleAgentToTunnelCommandAsync(new ArraySegment<byte>(buffer, 0, info.ReceivedBytes), info);
                            break;
                        case PacketTunnelOpCode.HeartBeat:
                            await HandleHeartBeatAsync(new ArraySegment<byte>(buffer, 0, 2), info);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async ValueTask HandleLoginCommandAsync(Memory<byte> buffer, IPEndPoint endpoint)
        {
            // +-----------------+---------------------+
            // | Token (8 bytes) | Identifier in ASCII |
            // +-----------------+---------------------+
            var token = BitConverter.ToInt64(buffer.Slice(0, 8).Span);
            var identifier = Encoding.ASCII.GetString(buffer.Slice(8).Span);

            if (!await tokenValidator.ValidateAsync(token, identifier))
            {
                logger.LogInformation($"{endpoint}<{identifier}> login to packet tunnel server failed");
                var _buffer = ArrayPool<byte>.Shared.Rent(2);
                try
                {
                    _buffer[0] = (byte)PacketTunnelOpCode.Login;
                    _buffer[1] = 0x01;
                    await server.SendAsync(new ArraySegment<byte>(_buffer, 0, 2), endpoint);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }
            }

            var __buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                __buffer[0] = (byte)PacketTunnelOpCode.Login;
                __buffer[1] = 0x00;
                await server.SendAsync(new ArraySegment<byte>(__buffer, 0, 2), endpoint);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(__buffer);
            }
            udpAssociator.SetAgentUdpEndpoint(identifier, endpoint);
            logger.LogInformation($"{endpoint}<{identifier}> login to packet tunnel server succeeded");
        }

        private async ValueTask HandleHeartBeatAsync(ArraySegment<byte> buffer, ReceiveResult from)
        {
            await server.SendAsync(buffer, from.RemoteEndPoint);
        }

        private async ValueTask HandleAgentToTunnelCommandAsync(ArraySegment<byte> buffer, ReceiveResult from)
        {
            var connectionId = new Guid(buffer.Slice(1, 16));
            var context = packetTunnelContextFactory.GetContextByConnectionId(connectionId);
            if (context == null)
            {
                logger.LogWarning($"Connection {connectionId} has not been found");
                return;
            }
            var tunnel = services.GetServices<IPacketTunnel>().SingleOrDefault(x => x.Id == context.TunnelId);
            if (tunnel == null)
            {
                logger.LogWarning($"Packet tunnel {context.TunnelId} has not been found");
            }
            await tunnel.BackwardAsync(udpEndpointListenerFinder.FindServerByEndpoint(context.EntryEndpoint), buffer, from, context);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
