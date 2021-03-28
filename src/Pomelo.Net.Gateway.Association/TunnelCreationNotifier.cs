using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Association
{
    public class TunnelCreationNotifier : ITunnelCreationNotifier
    {
        private AssociateServer server;

        public TunnelCreationNotifier(AssociateServer server)
        {
            this.server = server;
        }

        public ValueTask NotifyPacketTunnelCreationAsync(string userIdentifier, Guid connectionId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async ValueTask NotifyStreamTunnelCreationAsync(string userIdentifier, Guid connectionId, CancellationToken cancellationToken = default)
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(17))
            {
                var context = this.server.GetAssociateContextByUserIdentifier(userIdentifier);
                var stream = context.Client.GetStream();
                var _buffer = buffer.Memory.Slice(0, 17);
                _buffer.Span[0] = (byte)Protocol.TCP;
                connectionId.TryWriteBytes(_buffer.Slice(1, 16).Span);
                await stream.WriteAsync(_buffer, cancellationToken);
            }
        }
    }
}
