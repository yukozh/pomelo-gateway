using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Router;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class StreamTunnelContextFactory : IDisposable
    {
        public const int TunnelCreateTimeoutSeconds = 60;
        private ConcurrentDictionary<Guid, StreamTunnelContext> contexts;
        private ILogger<StreamTunnelContextFactory> logger;

        public StreamTunnelContextFactory(ILogger<StreamTunnelContextFactory> logger)
        {
            this.contexts = new ConcurrentDictionary<Guid, StreamTunnelContext>();
            this.logger = logger;
            RecycleAsync();
        }

        public IEnumerable<StreamTunnelContext> EnumerateContexts() => contexts.Values;

        public IEnumerable<StreamTunnelContext> EnumerateContexts(string userIdentifier) => contexts.Values.Where(x => x.UserIdentifier == userIdentifier);

        private async ValueTask RecycleAsync()
        {
            while (true)
            {
                try 
                {
                    logger.LogInformation("Recycling tunnels...");
                    foreach (var context in EnumerateContexts())
                    {
                        if (context.CreatedTimeUtc.AddSeconds(TunnelCreateTimeoutSeconds) < DateTime.UtcNow
                            && context.Status == StreamTunnelStatus.WaitingForClient)
                        {
                            contexts.Remove(context.ConnectionId, out var _);
                            logger.LogInformation($"TCP Tunnel {context.ConnectionId} has been recycled due to agent has not connected");
                            context.Dispose();
                        }

                        if (context.LastCommunicationTimeUtc.AddMinutes(5) < DateTime.UtcNow)
                        {
                            contexts.Remove(context.ConnectionId, out var _);
                            logger.LogInformation($"TCP Tunnel {context.ConnectionId} has been recycled due to the tunnel idle for a long time");
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

        public StreamTunnelContext Create(IMemoryOwner<byte> headerBuffer, string userIdentifier, IStreamRouter router, IStreamTunnel tunnel)
        {
            var context = new StreamTunnelContext(headerBuffer, userIdentifier, router, tunnel);
            contexts.TryAdd(context.ConnectionId, context);
            return context;
        }

        public StreamTunnelContext Create(IMemoryOwner<byte> headerBuffer, string userIdentifier, IStreamRouter router, IStreamTunnel tunnel, Guid connectionId)
        {
            var context = new StreamTunnelContext(connectionId, headerBuffer, userIdentifier, router, tunnel);
            contexts.TryAdd(context.ConnectionId, context);
            return context;
        }

        public void DestroyContext(Guid connectionId)
        {
            if (contexts.TryRemove(connectionId, out var context))
            {
                logger.LogInformation($"Disposing TCP tunnel {connectionId}");
                context?.Dispose();
            }
        }

        public void DestroyContextsForUserIdentifier(string identifier)
        {
            foreach (var context in contexts.Values)
            {
                if (context.UserIdentifier == identifier)
                {
                    if (contexts.TryRemove(context.ConnectionId, out var _))
                    {
                        logger.LogInformation($"Disposing TCP tunnel {context.ConnectionId}");
                        context?.Dispose();
                    }
                }
            }
        }

        public StreamTunnelContext GetContextByConnectionId(Guid id) => contexts.ContainsKey(id) ? contexts[id] : null;

        public void Dispose()
        {
            foreach (var x in contexts)
            {
                x.Value?.Dispose();
            }
        }
    }
}
