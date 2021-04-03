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
        private ConcurrentDictionary<Guid, StreamTunnelContext> tunnels;
        private ILogger<StreamTunnelContextFactory> logger;

        public StreamTunnelContextFactory(ILogger<StreamTunnelContextFactory> logger)
        {
            this.tunnels = new ConcurrentDictionary<Guid, StreamTunnelContext>();
            this.logger = logger;
            RecycleAsync();
        }

        public IEnumerable<StreamTunnelContext> EnumerateContexts() => tunnels.Values;

        public IEnumerable<StreamTunnelContext> EnumerateContexts(string userIdentifier) => tunnels.Values.Where(x => x.UserIdentifier == userIdentifier);

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
                            tunnels.Remove(context.ConnectionId, out var _);
                            logger.LogInformation($"Tunnel {context.ConnectionId} has been recycled due to agent has not connected");
                            context.Dispose();
                        }

                        if (context.LastCommunicationTimeUtc.AddMinutes(5) < DateTime.UtcNow)
                        {
                            tunnels.Remove(context.ConnectionId, out var _);
                            logger.LogInformation($"Tunnel {context.ConnectionId} has been recycled due to the tunnel idle for a long time");
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
            tunnels.TryAdd(context.ConnectionId, context);
            return context;
        }

        public StreamTunnelContext Create(IMemoryOwner<byte> headerBuffer, string userIdentifier, IStreamRouter router, IStreamTunnel tunnel, Guid connectionId)
        {
            var context = new StreamTunnelContext(connectionId, headerBuffer, userIdentifier, router, tunnel);
            tunnels.TryAdd(context.ConnectionId, context);
            return context;
        }

        public void Delete(Guid connectionId)
        {
            if (tunnels.TryRemove(connectionId, out var context))
            {
                logger.LogInformation($"Disposing tunnel {connectionId}");
                context?.Dispose();
            }
        }

        public void DestroyContextsForUserIdentifier(string identifier)
        {
            foreach (var context in tunnels.Values)
            {
                if (context.UserIdentifier == identifier)
                {
                    if (tunnels.TryRemove(context.ConnectionId, out var _))
                    {
                        logger.LogInformation($"Disposing tunnel {context.ConnectionId}");
                        context?.Dispose();
                    }
                }
            }
        }

        public StreamTunnelContext GetContextByConnectionId(Guid id) => tunnels.ContainsKey(id) ? tunnels[id] : null;

        public void Dispose()
        {
            foreach (var x in tunnels)
            {
                x.Value?.Dispose();
            }
        }
    }
}
