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

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpEndpointManager : IDisposable
    {
        private EndpointContext context;
        private ILogger<TcpEndpointManager> logger;
        private IServiceProvider services;
        private IServiceScope scope;
        private ConcurrentDictionary<IPEndPoint, TcpEndpointListner> listeners;

        public TcpEndpointManager(IServiceProvider services)
        {
            this.services = services;
            this.scope = services.CreateScope();
            this.context = scope.ServiceProvider.GetService<EndpointContext>();
            this.logger = services.GetRequiredService<ILogger<TcpEndpointManager>>();
            this.listeners = new ConcurrentDictionary<IPEndPoint, TcpEndpointListner>();
        }

        public void Dispose()
        {
            scope?.Dispose();
            scope = null;
        }

        public TcpEndpointListner GetOrCreateListenerForEndpoint(
            IPEndPoint endpoint, 
            Guid routerId, 
            Guid tunnelId,
            string userIdentifier,
            EndpointUserType userType = EndpointUserType.NonPublic)
        {
            logger.LogInformation($"Creating TCP Endpoint Listener {endpoint}");
            var _endpoint = context.Endpoints
                .Include(x => x.Users)
                .SingleOrDefault(x => x.Address == endpoint.Address.ToString() 
                    && x.Port == endpoint.Port 
                    && x.Protocol == Protocol.TCP);
            if (_endpoint == null)
            {
                _endpoint = new Endpoint
                {
                    Id = Guid.NewGuid(),
                    Address = endpoint.Address.ToString(),
                    Protocol = Protocol.TCP,
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
                logger.LogInformation($"User {userIdentifier} is using the endpoint {endpoint}");
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
                return new TcpEndpointListner(key, services);
            });
        }

        public async ValueTask RemoveAllRulesFromUserIdentifierAsync(
            string identifier, 
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation($"Removing rules which created by {identifier}...");
            context.EndpointUsers.RemoveRange(context.EndpointUsers.Where(x => x.UserIdentifier == identifier));
            await context.SaveChangesAsync(cancellationToken);
            var endpointsToRecycle = await context.Endpoints.Where(x => x.Users.Count == 0).ToListAsync(cancellationToken);
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
            Protocol protocol,
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
                Protocol = protocol,
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
                x => x.Identifier == identifier,
                cancellationToken);
            if (endpoint != null)
            {
                context.PreCreateEndpoints.Remove(endpoint);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public async ValueTask EnsurePreCreateEndpointsAsync()
        {
            var endpoints = await context.PreCreateEndpoints.ToListAsync();
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
    }
}
