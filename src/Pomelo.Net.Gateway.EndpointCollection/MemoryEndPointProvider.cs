using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class MemoryEndPointProvider : IEndPointProvider
    {
        private ConcurrentDictionary<IPEndPoint, EndPoint> tcpEndPoints = new ConcurrentDictionary<IPEndPoint, EndPoint>();
        private ConcurrentDictionary<IPEndPoint, EndPoint> udpEndPoints = new ConcurrentDictionary<IPEndPoint, EndPoint>();

        public ValueTask<EndPoint> GetActiveEndPointAsync(
            Protocol protocol, 
            IPEndPoint listenerEndPoint, 
            CancellationToken cancellationToken = default)
        {
            if (protocol == Protocol.TCP)
            {
                if (tcpEndPoints.TryGetValue(listenerEndPoint, out var ep))
                {
                    return ValueTask.FromResult(ep);
                }
                else
                {
                    return ValueTask.FromResult<EndPoint>(null);
                }
            }
            else
            {
                if (udpEndPoints.TryGetValue(listenerEndPoint, out var ep))
                {
                    return ValueTask.FromResult(ep);
                }
                else
                {
                    return ValueTask.FromResult<EndPoint>(null);
                }
            }
        }

        public ValueTask<IEnumerable<EndPoint>> GetActiveEndPointsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(tcpEndPoints.Values.Concat(udpEndPoints.Values));

        public ValueTask<EndPoint> GetOrAddActiveEndPointAsync(
            Protocol protocol,
            IPEndPoint endpoint, 
            Guid routerId, 
            Guid tunnelId, 
            string userId,
            EndpointType type = EndpointType.Bridge,
            CancellationToken cancellationToken = default)
        {
            var ep = new EndPoint
            {
                Id = Guid.NewGuid(),
                ListenerEndPoint = endpoint,
                Protocol = protocol,
                RouterId = routerId,
                TunnelId = tunnelId,
                Type = type
            };

            if (protocol == Protocol.TCP)
            {
                return ValueTask.FromResult(tcpEndPoints.GetOrAdd(endpoint, ep));
            }
            else
            {
                return ValueTask.FromResult(udpEndPoints.GetOrAdd(endpoint, ep));
            }
        }

        public ValueTask<IEnumerable<EndPoint>> RemoveAllActiveEndPointsFromUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            var ret = new List<EndPoint>();

            foreach (var ep in tcpEndPoints.Values)
            {
                if (!ep.UserIds.Contains(userId))
                {
                    continue;
                }

                ep.UserIds.Remove(userId);

                if (ep.UserIds.Count == 0)
                {
                    if (tcpEndPoints.TryRemove(ep.ListenerEndPoint, out var _))
                    {
                        ret.Add(ep);
                    }
                }
            }

            foreach (var ep in udpEndPoints.Values)
            {
                if (!ep.UserIds.Contains(userId))
                {
                    continue;
                }

                ep.UserIds.Remove(userId);

                if (ep.UserIds.Count == 0)
                {
                    if (udpEndPoints.TryRemove(ep.ListenerEndPoint, out var _))
                    {
                        ret.Add(ep);
                    }
                }
            }

            return ValueTask.FromResult(ret.AsEnumerable());
        }

        public ValueTask RemoveEndPointAsync(
            Protocol protocol, 
            IPEndPoint endPoint,
            CancellationToken cancellationToken = default)
        {
            if (protocol == Protocol.TCP)
            {
                tcpEndPoints.TryRemove(endPoint, out var _);
            }
            else
            {
                udpEndPoints.TryRemove(endPoint, out var _);
            }

            return ValueTask.CompletedTask;
        }
    }

    public static class MemoryEndPointProviderExtensions
    {
        public static IServiceCollection AddMemoryEndPointProvider(this IServiceCollection services)
            => services.AddSingleton<IEndPointProvider, MemoryEndPointProvider>();
    }
}
