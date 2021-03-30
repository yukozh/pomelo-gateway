using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Association;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Agent.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RuleController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<MappingRule> Get([FromServices] IMappingRuleProvider mappingRuleProvider)
        {
            return mappingRuleProvider.Rules;
        }

        [HttpPut]
        [HttpPost]
        [HttpPatch]
        public async ValueTask<IEnumerable<MappingRule>> Post(
            [FromServices] IMappingRuleProvider mappingRuleProvider,
            [FromServices] AssociateClient associateClient,
            [FromBody] IEnumerable<MappingRule> rules,
            CancellationToken cancellationToken = default)
        {
            await mappingRuleProvider.SetRulesAsync(rules, cancellationToken);
            mappingRuleProvider.Reload();
            await associateClient.SendCleanRulesAsync();
            await associateClient.SendRulesAsync();
            return rules;
        }
    }
}
