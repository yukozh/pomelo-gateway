using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pomelo.Net.Gateway.Agent.Models;
using Pomelo.Net.Gateway.Association;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Agent.Controllers
{
    [Authorize]
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
        public async ValueTask<IEnumerable<MappingRule2>> Post(
            [FromServices] IMappingRuleProvider mappingRuleProvider,
            [FromServices] AssociateClient associateClient,
            [FromBody] SetRulesRequestViewModel rules,
            CancellationToken cancellationToken = default)
        {
            await mappingRuleProvider.SetRulesAsync(rules.Rules.Select(x => new MappingRule 
            {
                Protocol = x.Protocol,
                LocalEndpoint = IPEndPoint.Parse(x.LocalEndpoint),
                RemoteEndpoint = IPEndPoint.Parse(x.RemoteEndpoint),
                LocalTunnelId = x.LocalTunnelId,
                RemoteRouterId = x.RemoteRouterId,
                RemoteTunnelId = x.RemoteTunnelId
            }), cancellationToken);
            mappingRuleProvider.Reload();
            if (associateClient.Connected)
            {
                await associateClient.SendCleanRulesAsync();
                await associateClient.SendRulesAsync();
            }
            return rules.Rules;
        }
    }
}
