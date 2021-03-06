using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Tunnel;
using Pomelo.WebSlotGateway.Models;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SlotController : ControllerBase
    {
        [HttpGet]
        public async ValueTask<IEnumerable<Slot>> Get(
            [FromServices] GatewayContext db,
            CancellationToken cancellationToken = default)
        {
            return await db.Slots
                .OrderByDescending(x => x.Priority)
                .ToListAsync(cancellationToken);
        }

        [HttpPut]
        [HttpPost]
        [HttpPatch]
        public async ValueTask<IEnumerable<Slot>> Put(
            [FromBody] SetSlotsRequestViewModel request,
            [FromServices] GatewayContext db,
            [FromServices] TcpEndpointManager tcpEndpointManager,
            [FromServices] ConfigurationHelper config,
            [FromServices] StreamTunnelContextFactory streamTunnelContextFactory,
            [FromServices] IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            var slots = await db.Slots.ToListAsync(cancellationToken);
            foreach (var slot in slots)
            {
                await tcpEndpointManager.RemoveAllRulesFromUserIdentifierAsync(slot.Id.ToString(), cancellationToken);
                await tcpEndpointManager.RemovePreCreateEndpointRuleAsync(slot.Id.ToString(), cancellationToken);
                streamTunnelContextFactory.DestroyContextsForUserIdentifier(slot.Id.ToString());
            }
            db.RemoveRange(slots);
            await db.SaveChangesAsync(cancellationToken);
            db.Slots.AddRange(request.Slots);
            await db.SaveChangesAsync(cancellationToken);
            var endpoint = await config.GetLocalEndpointAsync(cancellationToken);
            var router = services.GetServices<IStreamRouter>().Single(x => x is ARRAffinityRouter) as ARRAffinityRouter;
            await router.ReloadSlotsAsync();
            foreach (var slot in request.Slots)
            {
                await tcpEndpointManager.InsertPreCreateEndpointRuleAsync(
                    slot.Id.ToString(),
                    endpoint,
                    slot.Destination,
                    Startup.RouterId,
                    Startup.TunnelId,
                    slot.DestinationType == DestinationType.Https,
                    cancellationToken);
                tcpEndpointManager.GetOrCreateListenerForEndpoint(
                    endpoint,
                    Startup.RouterId,
                    Startup.TunnelId,
                    slot.Id.ToString(), 
                    EndpointUserType.Public);
            }
            return await db.Slots.ToListAsync(cancellationToken);
        }
    }
}
