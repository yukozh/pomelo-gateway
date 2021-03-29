using System.Collections.Generic;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public interface IMappingRuleProvider
    {
        public IReadOnlyCollection<MappingRule> Rules { get; }

        public void Reload();
    }
}
