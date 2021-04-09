using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class UdpEndpointManager : IUdpServerProvider, IDisposable
    {
        private EndpointContext context;
        private ILogger<UdpEndpointManager> logger;
        private IServiceProvider services;
        private IServiceScope scope;
        private ConcurrentDictionary<IPEndPoint, UdpEndpointListener> listeners;

        public UdpEndpointManager(IServiceProvider services)
        {
            this.services = services;
            this.scope = services.CreateScope();
            this.context = scope.ServiceProvider.GetService<EndpointContext>();
            this.logger = services.GetRequiredService<ILogger<UdpEndpointManager>>();
            this.listeners = new ConcurrentDictionary<IPEndPoint, UdpEndpointListener>();
        }

        public void Dispose()
        {
            scope?.Dispose();
            scope = null;
        }

        public UdpEndpointListener GetOrCreateListenerForEndpoint(
            IPEndPoint endpoint,
            Guid routerId,
            Guid tunnelId,
            string userIdentifier,
            EndpointUserType userType = EndpointUserType.NonPublic)
        {
            logger.LogInformation($"Creating UDP Endpoint Listener {endpoint}");
            var _endpoint = context.Endpoints
                .Include(x => x.Users)
                .SingleOrDefault(x => x.Address == endpoint.Address.ToString()
                    && x.Port == endpoint.Port
                    && x.Protocol == Protocol.UDP);
            if (_endpoint == null)
            {
                _endpoint = new Endpoint
                {
                    Id = Guid.NewGuid(),
                    Address = endpoint.Address.ToString(),
                    Protocol = Protocol.UDP,
                    Port = (ushort)endpoint.Port,
                    RouterId = routerId,
                    TunnelId = tunnelId
                };
                context.Endpoints.Add(_endpoint);
                context.SaveChanges();
            }
            if (!_endpoint.Users.Any(x => x.EndpointId == _endpoint.Id
                && x.UserIdentifier == userIdentifier))
            {
                logger.LogInformation($"User {userIdentifier} is using the endpoint UDP:{endpoint}");
                _endpoint.Users.Add(new EndpointUser
                {
                    EndpointId = _endpoint.Id,
                    UserIdentifier = userIdentifier,
                    Type = userType
                });
                context.SaveChanges();
            }

            return listeners.GetOrAdd(endpoint, (key) =>
            {
                return new UdpEndpointListener(key, routerId, tunnelId, services);
            });
        }

        public async ValueTask RemoveAllRulesFromUserIdentifierAsync(
            string identifier,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation($"Removing rules which created by {identifier}...");
            context.EndpointUsers.RemoveRange(context.EndpointUsers
                .Where(x => x.UserIdentifier == identifier)
                .Where(x => x.Endpoint.Protocol == Protocol.UDP));
            await context.SaveChangesAsync(cancellationToken);
            var endpointsToRecycle = await context.Endpoints
                .Where(x => x.Users.Count == 0)
                .ToListAsync(cancellationToken);
            if (endpointsToRecycle.Count > 0)
            {
                foreach (var endpoint in endpointsToRecycle)
                {
                    RecycleEndpoint(endpoint);
                }
                context.Endpoints.RemoveRange(endpointsToRecycle);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async ValueTask InsertPreCreateEndpointRuleAsync(
            string identifier,
            IPEndPoint serverEndpoint,
            IPEndPoint destinationEndpoint,
            Guid routerId,
            Guid tunnelId,
            CancellationToken cancellationToken = default)
        {
            context.PreCreateEndpoints.Add(new PreCreateEndpoint
            {
                DestinationEndpoint = destinationEndpoint.ToString(),
                ServerEndpoint = serverEndpoint.ToString(),
                Identifier = identifier,
                Protocol = Protocol.UDP,
                RouterId = routerId,
                TunnelId = tunnelId
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        public async ValueTask RemovePreCreateEndpointRuleAsync(
            string identifier,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await context.PreCreateEndpoints.SingleOrDefaultAsync(
                x => x.Identifier == identifier && x.Protocol == Protocol.UDP,
                cancellationToken);
            if (endpoint != null)
            {
                context.PreCreateEndpoints.Remove(endpoint);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async ValueTask EnsurePreCreateEndpointsAsync()
        {
            try
            {
                var endpoints = await context.PreCreateEndpoints
                    .Where(x => x.Protocol == Protocol.UDP)
                    .ToListAsync();
                foreach (var endpoint in endpoints)
                {
                    GetOrCreateListenerForEndpoint(
                        IPEndPoint.Parse(endpoint.ServerEndpoint),
                        endpoint.RouterId,
                        endpoint.TunnelId,
                        endpoint.Identifier,
                        EndpointUserType.Public);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                throw;
            }
        }

        public async ValueTask<EndpointUser> GetEndpointUserByIdentifierAsync(
            string identifier,
            CancellationToken cancellationToken = default)
        {
            return await context.EndpointUsers
                .SingleAsync(x => x.UserIdentifier == identifier, cancellationToken);
        }

        public async ValueTask<PreCreateEndpoint> GetPreCreateEndpointByIdentifierAsync(
            string identifier,
            CancellationToken cancellationToken = default)
        {
            return await context.PreCreateEndpoints
                .SingleAsync(x => x.Identifier == identifier, cancellationToken);
        }

        private void RecycleEndpoint(Endpoint endpoint)
        {
            logger.LogInformation($"No user uses endpoint {endpoint.Address}:{endpoint.Port}, recycling...");
            this.listeners.TryRemove(new IPEndPoint(endpoint.IPAddress, endpoint.Port), out var listener);
            listener?.Dispose();
        }

        public PomeloUdpClient FindServerByEndpoint(IPEndPoint endpoint)
            => listeners.ContainsKey(endpoint) ? listeners[endpoint].Server : null;
    }
}
