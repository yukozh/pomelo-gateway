using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
            RecycleAsync();
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

        public IEnumerable<PacketTunnelContext> EnumerateContexts(string identifier = null)
        {
            if (identifier == null)
            {
                return contexts.Values;
            }
            else
            {
                return contexts.Values.Where(x => x.Identifier == identifier);
            }
        }

        public void DestroyContext(Guid connectionId)
        {
            if (contexts.TryRemove(connectionId, out var context))
            {
                logger.LogInformation($"Disposing udp tunnel {connectionId}");
                context?.Dispose();
            }
        }

        private async ValueTask RecycleAsync()
        {
            while (true)
            {
                try
                {
                    logger.LogInformation("Recycling tunnels...");
                    foreach (var context in EnumerateContexts())
                    {
                        if (context.LastActionTimeUtc.AddMinutes(5) < DateTime.UtcNow)
                        {
                            contexts.Remove(context.ConnectionId, out var _);
                            logger.LogInformation($"UDP Tunnel {context.ConnectionId} has been recycled due to the tunnel idle for a long time");
                            context.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
                logger.LogInformation("Sleep 1 minute...");
                await Task.Delay(1000 * 60);
            }
        }
    }
}
