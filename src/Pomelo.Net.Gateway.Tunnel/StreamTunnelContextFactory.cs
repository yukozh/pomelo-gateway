using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Router;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class StreamTunnelContextFactory : IDisposable
    {
        public const int TunnelCreateTimeoutSeconds = 60;
        private ConcurrentDictionary<Guid, StreamTunnelContext> tunnels;

        public StreamTunnelContextFactory()
        {
            this.tunnels = new ConcurrentDictionary<Guid, StreamTunnelContext>();
            CollectAsync();
        }

        public IEnumerable<StreamTunnelContext> EnumerateContexts() => tunnels.Values;

        private async ValueTask CollectAsync()
        {
            while (true)
            {
                try 
                {
                    foreach (var context in EnumerateContexts())
                    {
                        if (context.CreatedTimeUtc.AddSeconds(TunnelCreateTimeoutSeconds) < DateTime.UtcNow
                            && context.Status == StreamTunnelStatus.WaitingForClient)
                        {
                            tunnels.Remove(context.ConnectionId, out var _);
                            context.Dispose();
                        }
                    }
                } 
                catch (Exception ex)
                { 
                
                }
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
            tunnels.TryRemove(connectionId, out var _);
        }

        public void DestroyContextsForUserIdentifier(string identifier)
        {
            foreach (var context in tunnels.Values)
            {
                if (context.UserIdentifier == identifier)
                {
                    tunnels.TryRemove(context.ConnectionId, out var _);
                    context.Dispose();
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
