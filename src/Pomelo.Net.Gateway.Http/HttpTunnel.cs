using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class HttpTunnel : IStreamTunnel
    {
        private const int BufferSize = 2048;
        public Guid Id => Guid.Parse("5237e1e5-5f1a-4df0-a716-2f3dbac2a3ff");
        public string Name => "HTTP Tunnel";

        public async ValueTask BackwardAsync(
            Stream rightToTunnelStream,
            Stream tunnelToLeftStream,
            StreamTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            if (!tunnelToLeftStream.CanWrite)
            {
                return;
            }

            using (var buffer = MemoryPool<byte>.Shared.Rent(BufferSize))
            {
                while (true)
                {
                    var length = await rightToTunnelStream.ReadAsync(buffer.Memory);
                    if (length == 0)
                    {
                        break;
                    }
                    context.LastCommunicationTimeUtc = DateTime.UtcNow;
                    await tunnelToLeftStream.WriteAsync(buffer.Memory.Slice(0, length));
                }
            }
        }

        public async ValueTask ForwardAsync(
            Stream leftToTunnelStream,
            Stream tunnelToRightStream,
            StreamTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            if (!tunnelToRightStream.CanWrite)
            {
                return;
            }

            using (var buffer = MemoryPool<byte>.Shared.Rent(BufferSize))
            {
                while (true)
                {
                    var length = await leftToTunnelStream.ReadAsync(buffer.Memory);
                    if (length == 0)
                    {
                        break;
                    }
                    context.LastCommunicationTimeUtc = DateTime.UtcNow;
                    await tunnelToRightStream.WriteAsync(buffer.Memory.Slice(0, length));
                }
            }
        }
    }
}
