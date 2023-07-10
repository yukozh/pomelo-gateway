using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpEndPointManager : IDisposable
    {
        private ILogger<TcpEndPointManager> logger;
        private IServiceProvider services;
        private IServiceScope scope;
        private IEndPointProvider endPointProvider;
        private IStaticRuleProvider staticRuleProvider;
        private ConcurrentDictionary<IPEndPoint, TcpEndPointListener> listeners;

        public TcpEndPointManager(IServiceProvider services)
        {
            this.services = services;
            this.scope = services.CreateScope();
            this.endPointProvider = scope.ServiceProvider.GetService<IEndPointProvider>();
            this.staticRuleProvider = scope.ServiceProvider.GetService<IStaticRuleProvider>();
            this.logger = services.GetRequiredService<ILogger<TcpEndPointManager>>();
            this.listeners = new ConcurrentDictionary<IPEndPoint, TcpEndPointListener>();
        }

        public void Dispose()
        {
            scope?.Dispose();
            scope = null;
        }

        public async ValueTask<TcpEndPointListener> GetOrCreateListenerForEndPointAsync(
            IPEndPoint ep, 
            Guid routerId, 
            Guid tunnelId,
            string userId,
            EndpointType type = EndpointType.Bridge,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation($"Creating TCP Endpoint Listener {ep}");
            var endPoint = await endPointProvider.GetOrAddActiveEndPointAsync(Protocol.TCP, ep, routerId, tunnelId, userId, type, cancellationToken);
            
            return listeners.GetOrAdd(ep, (key) => 
            {
                return new TcpEndPointListener(key, services);
            });
        }

        public async ValueTask RemoveEndPointAsync(
            IPEndPoint ep,
            CancellationToken cancellationToken = default)
        {
            await endPointProvider.RemoveEndPointAsync(Protocol.TCP, ep, cancellationToken);

            if (listeners.TryRemove(ep, out var listener))
            {
                listener.Dispose();
                logger.LogInformation($"Removed UDP listener {ep}");
            }
        }

        public async ValueTask RemoveAllRulesFromUserAsync(
            string userId, 
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation($"Removing rules which created by {userId}...");
            var endpointsToRecycle = await endPointProvider.RemoveAllActiveEndPointsFromUserAsync(userId, cancellationToken);
            foreach (var ep in endpointsToRecycle)
            {
                listeners.TryRemove(ep.ListenerEndPoint, out var listener);
                listener.Dispose();
                logger.LogInformation($"No user uses endpoint {ep.ListenerEndPoint}, recycling...");
            }
        }

        public async ValueTask EnsureStaticRulesEndPointsCreatedAsync()
        {
            var endpoints = (await staticRuleProvider.GetStaticRulesAsync())
                .Where(x => x.Protocol == Protocol.TCP);

            foreach (var endpoint in endpoints)
            {
                await GetOrCreateListenerForEndPointAsync(
                    endpoint.ListenerEndpoint,
                    endpoint.RouterId,
                    endpoint.TunnelId,
                    endpoint.Id,
                    EndpointType.Static);
            }
        }
    }
}
