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
    public class DefaultStreamTunnel : IStreamTunnel
    {
        private const int BufferSize = 2048;
        public Guid Id => Guid.Parse("4048bf29-0997-4f9d-827b-fe29ceb0e4fe");
        public string Name => nameof(DefaultStreamTunnel);

        public async ValueTask BackwardAsync(Stream rightToTunnelStream, Stream tunnelToLeftStream, CancellationToken cancellationToken = default)
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
                    await tunnelToLeftStream.WriteAsync(buffer.Memory.Slice(0, length));
                }
            }
        }

        public async ValueTask ForwardAsync(Stream leftToTunnelStream, Stream tunnelToRightStream, CancellationToken cancellationToken = default)
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
                    await tunnelToRightStream.WriteAsync(buffer.Memory.Slice(0, length));
                }
            }
        }
    }
}
