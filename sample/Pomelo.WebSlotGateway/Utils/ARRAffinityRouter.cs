using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Router;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Utils
{
    public class ARRAffinityRouter : IStreamRouter, IDisposable
    {
        private IServiceScope scope;
        private Guid[] slotMap;
        private SlotContext db;
        private ConfigurationHelper configurationHelper;
        private Random random;
        private ConcurrentDictionary<IPAddress, ARRContext> contexts;

        public ARRAffinityRouter(IServiceProvider services)
        {
            this.slotMap = null;
            this.scope = services.CreateScope();
            this.db = scope.ServiceProvider.GetRequiredService<SlotContext>();
            this.configurationHelper = services.GetRequiredService<ConfigurationHelper>();
            this.random = new Random();
            this.contexts = new ConcurrentDictionary<IPAddress, ARRContext>();
            RecycleAsync();
        }

        public Guid Id => Guid.Parse("374c20bc-e730-4da3-8c2f-7e570da35268");
        public string Name => "ARR Affinity Stream Router";
        public int ExpectedBufferSize => 0;

        public async ValueTask<RouteResult> DetermineIdentifierAsync(Stream stream, Memory<byte> buffer, IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!await configurationHelper.GetARRAffinitySwitchAsync(cancellationToken))
            {
                var slotId = await AssignSlotAsync(cancellationToken);
                return new RouteResult
                {
                    HeaderLength = 0,
                    IsSucceeded = true,
                    Identifier = slotId.ToString()
                };
            }
            else
            {
                var slotId = await AssignSlotAsync(cancellationToken);
                var context = contexts.GetOrAdd(endpoint.Address, (key) =>
                {
                    return new ARRContext
                    {
                        ClientAddress = endpoint.Address,
                        SlotId = slotId
                    };
                });
                context.LastActiveTimeUtc = DateTime.UtcNow;
                if (!await IsSlotValidAsync(context.SlotId, cancellationToken))
                {
                    await ReloadSlotsAsync(cancellationToken);
                    context.SlotId = await AssignSlotAsync(cancellationToken);
                }
                return new RouteResult
                {
                    HeaderLength = 0,
                    IsSucceeded = true,
                    Identifier = context.SlotId.ToString()
                };
            }
        }

        public async ValueTask ReloadSlotsAsync(CancellationToken cancellationToken = default)
        {
            var slots = await db.Slots
                .Where(x => x.Status == SlotStatus.Enabled)
                .ToListAsync(cancellationToken);
            slotMap = new Guid[slots.Sum(x => x.Priority)];
            var pos = 0;
            foreach(var slot in slots)
            {
                for (var i = 0; i < slot.Priority; ++i)
                {
                    slotMap[pos++] = slot.Id;
                }
            }
        }

        private async ValueTask RecycleAsync()
        {
            while (true)
            {
                if (await configurationHelper.GetARRAffinitySwitchAsync())
                {
                    var expireMinutes = await configurationHelper.GetARRAffinityExpireMinutesAsync();
                    foreach (var context in contexts.Values)
                    {
                        if (context.LastActiveTimeUtc.AddMinutes(expireMinutes) < DateTime.UtcNow)
                        {
                            contexts.TryRemove(context.ClientAddress, out var _);
                        }
                    }
                }
                await Task.Delay(1000 * 60);
            }
        }

        private async ValueTask<bool> IsSlotValidAsync(Guid slotId, CancellationToken cancellationToken = default)
        {
            return await db.Slots.AnyAsync(x => x.Id == slotId, cancellationToken);
        }

        private async ValueTask<Guid> AssignSlotAsync(CancellationToken cancellationToken = default)
        {
            if (slotMap == null)
            {
                await ReloadSlotsAsync(cancellationToken);
            }

            while (true)
            {
                var slotId = slotMap[random.Next(0, slotMap.Length)];
                if (await IsSlotValidAsync(slotId, cancellationToken))
                {
                    return slotId;
                }
            }
        }

        public void Dispose()
        {
            scope?.Dispose();
        }
    }
}
