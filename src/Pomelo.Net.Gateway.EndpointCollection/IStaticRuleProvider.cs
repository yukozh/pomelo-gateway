using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public interface IStaticRuleProvider
    {
        ValueTask<IEnumerable<StaticRule>> GetStaticRulesAsync(
            CancellationToken cancellationToken = default);

        ValueTask<StaticRule> GetStaticRuleByListenerEndPointAsync(
            Protocol protocol,
            IPEndPoint endPoint,
            CancellationToken cancellationToken = default);
    }
}
