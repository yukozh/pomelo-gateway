using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public interface IEndPointProvider
    {
        ValueTask<IEnumerable<StaticRule>> GetStaticRulesAsync(
            CancellationToken cancellationToken = default);

        ValueTask<StaticRule> GetStaticRuleByListenerEndPointAsync(
            Protocol protocol,
            IPEndPoint endPoint,
            CancellationToken cancellationToken = default);

        ValueTask<IEnumerable<EndPoint>> GetActiveEndPointsAsync(
            CancellationToken cancellationToken = default);

        ValueTask<EndPoint> GetActiveEndPointAsync(
            Protocol protocol, 
            IPEndPoint listenerEndPoint,
            CancellationToken cancellationToken = default);

        ValueTask<EndPoint> GetOrAddActiveEndPointAsync(
            IPEndPoint endpoint,
            Guid routerId,
            Guid tunnelId,
            string userId,
            EndpointType type = EndpointType.Bridge,
            CancellationToken cancellationToken = default);

        ValueTask<IEnumerable<EndPoint>> RemoveAllActiveEndPointsFromUserAsync(
            string userId, 
            CancellationToken cancellationToken = default);
    }
}
