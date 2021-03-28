using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                try {
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

        public StreamTunnelContext Create(IMemoryOwner<byte> headerBuffer, string userIdentifier)
        {
            var context = new StreamTunnelContext(headerBuffer, userIdentifier);
            tunnels.TryAdd(context.ConnectionId, context);
            return context;
        }

        public StreamTunnelContext GetContextById(Guid id) => tunnels.ContainsKey(id) ? tunnels[id] : null;

        public void Dispose()
        {

        }
    }
}
