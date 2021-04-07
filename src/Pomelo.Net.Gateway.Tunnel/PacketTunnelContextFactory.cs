using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PacketTunnelContextFactory
    {
        private ConcurrentDictionary<Guid, PacketTunnelContext> contexts;

        public PacketTunnelContextFactory()
        {
            contexts = new ConcurrentDictionary<Guid, PacketTunnelContext>();
        }

        public PacketTunnelContext GetOrCreateContext(string identifier, IPEndPoint remoteEndpoint)
        {
            var context = FindContextByRemoteEndpoint(remoteEndpoint);
            if (context == null)
            {
                context = new PacketTunnelContext(Guid.NewGuid(), identifier);
                contexts.TryAdd(context.ConnectionId, context);
            }
            return context;
        }

        public PacketTunnelContext FindContextByRemoteEndpoint(IPEndPoint remoteEndpoint)
            => contexts.Values.SingleOrDefault(x => x.LeftEndpoint.Equals(remoteEndpoint));

        public PacketTunnelContext GetContextByConnectionId(Guid id)
            => contexts.ContainsKey(id) ? contexts[id] : null;
    }
}
