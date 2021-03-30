using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public interface IMappingRuleProvider
    {
        public IReadOnlyCollection<MappingRule> Rules { get; }

        public ValueTask SetRulesAsync(
            IEnumerable<MappingRule> rules, 
            CancellationToken cancellationToken = default);

        public void Reload();
    }
}
