using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigurationController : ControllerBase
    {
        [HttpGet]
        public async ValueTask<IEnumerable<Config>> Get(
            [FromServices] SlotContext db,
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
            [FromServices] SlotContext db,
            CancellationToken cancellationToken = default)
        {
            var slots = await db.Configurations
                .ToListAsync(cancellationToken);

            foreach (var slot in slots)
            {
                var update = request.Configurations.SingleOrDefault(x => x.Key == slot.Key);
                if (update != null)
                {
                    slot.Value = update.Value;
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            return await db.Configurations
                .ToListAsync(cancellationToken);
        }
    }
}
