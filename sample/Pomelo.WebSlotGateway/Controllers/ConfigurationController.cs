using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Tunnel;
using Pomelo.WebSlotGateway.Models;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigurationController : ControllerBase
    {
        [HttpGet]
        public async ValueTask<IEnumerable<Config>> Get(
            [FromServices] GatewayContext db,
            CancellationToken cancellationToken = default)
        {
            return await db.Configurations
                .ToListAsync(cancellationToken);
        }

        [HttpPut]
        [HttpPost]
        [HttpPatch]
        public async ValueTask<IEnumerable<Config>> Put(
            [FromBody] SetConfigurationRequestViewModel request,
            [FromServices] TcpEndpointManager tcpEndpointManager,
            [FromServices] ConfigurationHelper config,
            [FromServices] GatewayContext db,
            [FromServices] StreamTunnelContextFactory streamTunnelContextFactory,
            CancellationToken cancellationToken = default)
        {
            var configurations = await db.Configurations
                .ToListAsync(cancellationToken);

            var needReset = false;
            foreach (var configuration in configurations)
            {
                var update = request.Configurations.SingleOrDefault(x => x.Key == configuration.Key);
                if (update != null)
                {
                    if (configuration.Key == ConfigurationHelper.KeyLocalEndpoint && configuration.Value != update.Value)
                    {
                        needReset = true;
                    }
                    configuration.Value = update.Value;
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            if (needReset) 
            {
                // Remove all rules
                var slots = await db.Slots.ToListAsync(cancellationToken);
                foreach (var slot in slots)
                {
                    await tcpEndpointManager.RemoveAllRulesFromUserIdentifierAsync(slot.Id.ToString(), cancellationToken);
                    await tcpEndpointManager.RemovePreCreateEndpointRuleAsync(slot.Id.ToString(), cancellationToken);
                    streamTunnelContextFactory.DestroyContextsForUserIdentifier(slot.Id.ToString());
                }
                db.RemoveRange(slots);
                await db.SaveChangesAsync(cancellationToken);

                // Setup rules
                foreach (var rule in slots)
                {
                    await tcpEndpointManager.InsertPreCreateEndpointRuleAsync(
                        rule.Id.ToString(),
                        await config.GetLocalEndpointAsync(),
                        await AddressHelper.ParseAddressAsync(rule.Destination, 0),
                        Startup.RouterId,
                        Startup.TunnelId);
                }
                await tcpEndpointManager.EnsurePreCreateEndpointsAsync();
            }

            return await db.Configurations
                .ToListAsync(cancellationToken);
        }
    }
}
