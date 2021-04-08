using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PacketTunnelContextFactory
    {
        private ConcurrentDictionary<Guid, PacketTunnelContext> contexts;
        private ILogger<PacketTunnelContextFactory> logger;

        public PacketTunnelContextFactory(ILogger<PacketTunnelContextFactory> logger)
        {
            this.logger = logger;
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

        public void DestroyContextsForUserIdentifier(string identifier)
        {
            foreach (var context in contexts.Values)
            {
                if (context.Identifier == identifier)
                {
                    if (contexts.TryRemove(context.ConnectionId, out var _))
                    {
                        logger.LogInformation($"Disposing UDP tunnel {context.ConnectionId}");
                        context?.Dispose();
                    }
                }
            }
        }
    }
}
