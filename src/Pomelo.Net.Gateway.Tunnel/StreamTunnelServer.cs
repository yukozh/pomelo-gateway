﻿using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Token;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class StreamTunnelServer : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private ITokenValidator tokenValidator;
        private IServiceProvider services;
        private ILogger<StreamTunnelServer> logger;
        private IPEndPoint endpoint;

        public IPEndPoint Endpoint => endpoint;

        public StreamTunnelServer(
            IPEndPoint endpoint, 
            IServiceProvider services)
        {
            server = new TcpListener(endpoint);
            server.Server.ReceiveTimeout = 1000 * 30;
            server.Server.SendTimeout = 1000 * 30;
            this.endpoint = endpoint;
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.tokenValidator = services.GetRequiredService<ITokenValidator>();
            this.logger = services.GetRequiredService<ILogger<StreamTunnelServer>>();
            this.services = services;
        }

        public void Start()
        {
            server.Start();
            logger.LogInformation($"Stream Tunnel Server is listening on {server.LocalEndpoint}...");
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
            Guid connectionId = default;
            using (var authenticationBuffer = MemoryPool<byte>.Shared.Rent(8 + 16))
            {
                try
                {
                    // Handshake
                    // +-----------------+--------------------------+
                    // | Token (8 bytes) | Connection ID (16 bytes) |
                    // +-----------------+--------------------------+
                    var _authenticationBuffer = authenticationBuffer.Memory.Slice(0, 8 + 16);
                    await stream.ReadExAsync(_authenticationBuffer);
                    var token = BitConverter.ToInt64(_authenticationBuffer.Slice(0, 8).Span);
                    connectionId = new Guid(_authenticationBuffer.Slice(8, 16).Span);
                    var context = streamTunnelContextFactory.GetContextByConnectionId(connectionId);
                    var result = await tokenValidator.ValidateAsync(token, context.UserIdentifier);

                    // +-----------------+
                    // | Result (1 byte) |
                    // +-----------------+
                    // 0=OK, 1=Failed
                    if (result)
                    {
                        _authenticationBuffer.Span[0] = 0x00;
                        await stream.WriteAsync(_authenticationBuffer.Slice(0, 1));
                    }
                    else
                    {
                        _authenticationBuffer.Span[0] = 0x01;
                        await stream.WriteAsync(_authenticationBuffer.Slice(0, 1));
                        client.Close();
                        client.Dispose();
                        connectionId = default;
                        return;
                    }
                    context.LeftClient = client; // Agent side

                    // Start tunneling
                    var concatStream = new ConcatStream();
                    concatStream.Join(context.GetHeaderStream(), context.RightClient.GetStream());
                    await Task.WhenAll(new[]
                    {
                        context.Tunnel.ForwardAsync(concatStream, context.LeftClient.GetStream(), context).AsTask(),
                        context.Tunnel.BackwardAsync(context.LeftClient.GetStream(), context.RightClient.GetStream(), context).AsTask()
                    });
                }
                finally
                {
                    client.Close();
                    client.Dispose();

                    if (connectionId != default)
                    {
                        streamTunnelContextFactory.DestroyContext(connectionId);
                    }
                }
            }
        }

        public void Dispose()
        {
            server?.Stop();
        }
    }
}
