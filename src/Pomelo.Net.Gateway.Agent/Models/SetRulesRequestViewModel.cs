using System.Collections.Generic;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Agent.Models
{
    public class SetRulesRequestViewModel
    {
        public IEnumerable<MappingRule2> Rules { get; set; }
    }
}
