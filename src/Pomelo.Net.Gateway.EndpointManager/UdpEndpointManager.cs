﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class UdpEndPointManager : IUdpEndpointListenerFinder, IDisposable
    {
        private ILogger<UdpEndPointManager> logger;
        private IServiceProvider services;
        private IServiceScope scope;
        private IEndPointProvider endPointProvider;
        private IStaticRuleProvider staticRuleProvider;
        private ConcurrentDictionary<IPEndPoint, UdpEndPointListener> listeners;

        public UdpEndPointManager(IServiceProvider services)
        {
            this.services = services;
            this.scope = services.CreateScope();
            this.logger = services.GetRequiredService<ILogger<UdpEndPointManager>>();
            this.listeners = new ConcurrentDictionary<IPEndPoint, UdpEndPointListener>();
            this.endPointProvider = services.GetRequiredService<IEndPointProvider>();
            this.staticRuleProvider = services.GetRequiredService<IStaticRuleProvider>();
        }

        public void Dispose()
        {
            scope?.Dispose();
            scope = null;
        }

        public async ValueTask<UdpEndPointListener> GetOrCreateListenerForEndpointAsync(
            IPEndPoint endpoint,
            Guid routerId,
            Guid tunnelId,
            string userId,
            EndpointType userType = EndpointType.Bridge,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation($"Creating UDP Endpoint Listener {endpoint}");

            var endPoint = await endPointProvider.GetOrAddActiveEndPointAsync(Protocol.UDP, endpoint, routerId, tunnelId, userId, userType, cancellationToken);

            return listeners.GetOrAdd(endpoint, (key) =>
            {
                return new UdpEndPointListener(key, routerId, tunnelId, services);
            });
        }

        public async ValueTask RemoveEndPointAsync(
            IPEndPoint ep, 
            CancellationToken cancellationToken = default)
        {
            await endPointProvider.RemoveEndPointAsync(Protocol.UDP, ep, cancellationToken);

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

        public async ValueTask EnsureStaticRulesEndPointsCreatedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoints = (await staticRuleProvider.GetStaticRulesAsync(cancellationToken))
                    .Where(x => x.Protocol == Protocol.UDP);

                foreach (var endpoint in endpoints)
                {
                    _ = await GetOrCreateListenerForEndpointAsync(
                        endpoint.ListenerEndpoint,
                        endpoint.RouterId,
                        endpoint.TunnelId,
                        endpoint.Id,
                        EndpointType.Static);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                throw;
            }
        }

        public PomeloUdpClient FindServerByEndpoint(IPEndPoint endpoint)
            => listeners.ContainsKey(endpoint) ? listeners[endpoint].Server : null;
    }
}
