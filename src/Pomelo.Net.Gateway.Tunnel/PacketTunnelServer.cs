using System;
using System.Buffers;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Token;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PacketTunnelServer
    {
        private PomeloUdpClient server;
        private IPEndPoint endpoint;
        private ILogger<PacketTunnelServer> logger;
        private ITokenValidator tokenValidator;

        public event Action<string, IPEndPoint> SetIdentifierEndpoint;
        public PomeloUdpClient Server => server;

        public PacketTunnelServer(IPEndPoint endpoint, IServiceProvider services)
        {
            this.endpoint = endpoint;
            this.logger = services.GetRequiredService<ILogger<PacketTunnelServer>>();
            server = new PomeloUdpClient(endpoint);
        }

        private async ValueTask StartAsync()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(256);
            try
            {
                while (true)
                {
                    var info = await server.ReceiveAsync(buffer);
                    var op = (PacketTunnelOpCode)buffer[0];
                    logger.LogInformation($"{info.RemoteEndPoint}: {op.ToString()}");
                    switch (op)
                    {
                        case PacketTunnelOpCode.Login:
                            HandleLoginCommandAsync(buffer.AsMemory().Slice(1, info.ReceivedBytes - 1), info.RemoteEndPoint);
                            break;
                        case PacketTunnelOpCode.HeartBeat:
                            break;
                    }
                }
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

            logger.LogInformation($"{endpoint}<{identifier}> login to packet tunnel server succeeded");
            SetIdentifierEndpoint(identifier, endpoint);
        }
    }
}
