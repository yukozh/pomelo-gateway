using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pomelo.Net.Gateway.EndpointCollection;
using Pomelo.Net.Gateway.EndpointManager;
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
            [FromServices] SlotContext db,
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
            [FromBody] IEnumerable<Slot> model,
            [FromServices] SlotContext db,
            [FromServices] TcpEndpointManager tcpEndpointManager,
            [FromServices] ConfigurationHelper config,
            CancellationToken cancellationToken = default)
        {
            var slots = await db.Slots.ToListAsync(cancellationToken);
            db.RemoveRange(slots);
            await db.SaveChangesAsync(cancellationToken);
            foreach (var slot in slots)
            {
                await tcpEndpointManager.RemoveAllRulesFromUserIdentifierAsync(slot.Id.ToString(), cancellationToken);
                await tcpEndpointManager.RemovePreCreateEndpointRuleAsync(slot.Id.ToString(), cancellationToken);
            }
            db.Slots.AddRange(model);
            await db.SaveChangesAsync(cancellationToken);
            var endpoint = await config.GetLocalEndpointAsync(cancellationToken);
            foreach (var slot in model)
            {
                await tcpEndpointManager.InsertPreCreateEndpointRuleAsync(
                    slot.Id.ToString(),
                    endpoint,
                    await AddressHelper.ParseAddressAsync(slot.Destination, 0),
                    Startup.RouterId,
                    Startup.TunnelId,
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
