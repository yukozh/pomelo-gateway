using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Http;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Utils
{
    public class ARRAffinityRouter : HttpRouterBase
    {
        private IServiceProvider services;
        private ConcurrentDictionary<string, Guid[]> slotMaps;
        private ConfigurationHelper configurationHelper;
        private Random random;
        private ConcurrentDictionary<string, ConcurrentDictionary<IPAddress, ARRContext>> contexts;

        public IEnumerable<ARRContext> Contexts => contexts.Values.SelectMany(x => x.Values);

        public ARRAffinityRouter(IServiceProvider services)
        {
            this.services = services;
            this.slotMaps = new ConcurrentDictionary<string, Guid[]>(StringComparer.OrdinalIgnoreCase);
            this.configurationHelper = services.GetRequiredService<ConfigurationHelper>();
            this.random = new Random();
            this.contexts = new ConcurrentDictionary<string, ConcurrentDictionary<IPAddress, ARRContext>>(StringComparer.OrdinalIgnoreCase);
            RecycleAsync();
        }

        public override Guid Id => Guid.Parse("374c20bc-e730-4da3-8c2f-7e570da35268");
        public override string Name => "ARR Affinity Stream Router";

        public async ValueTask ReloadSlotsAsync(CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SlotContext>();
                var slots = (await db.Slots
                    .AsNoTracking()
                    .Where(x => x.Status == SlotStatus.Enabled)
                    .ToListAsync(cancellationToken))
                    .GroupBy(x => x.Host);
                foreach (var group in slots)
                {
                    var slotMap = new Guid[group.Sum(x => x.Priority)];
                    var pos = 0;
                    foreach (var slot in group)
                    {
                        for (var i = 0; i < slot.Priority; ++i)
                        {
                            slotMap[pos++] = slot.Id;
                        }
                    }
                    slotMaps.AddOrUpdate(group.Key, slotMap, (_, __) => slotMap);
                }
                foreach (var keyToRemove in slotMaps.Keys.Where(x => !slots.Select(y => y.Key).Contains(x)))
                {
                    slotMaps.Remove(keyToRemove, out var _);
                }
            }
        }

        public override async ValueTask<string> FindDestinationByHeadersAsync(HttpHeader headers, IPEndPoint from, CancellationToken cancellationToken = default)
        {
            if (!await configurationHelper.GetARRAffinitySwitchAsync(cancellationToken))
            {
                var slotId = await AssignSlotAsync(headers.Host, cancellationToken);
                return slotId.ToString();
            }
            else
            {
                var slotId = await AssignSlotAsync(headers.Host, cancellationToken);
                var contextDic = contexts.GetOrAdd(headers.Host, (_) => new ConcurrentDictionary<IPAddress, ARRContext>());
                var context = contextDic.GetOrAdd(from.Address, (key) =>
                {
                    return new ARRContext
                    {
                        ClientAddress = from.Address,
                        SlotId = slotId,
                        Host = headers.Host
                    };
                });
                context.LastActiveTimeUtc = DateTime.UtcNow;
                if (!await IsSlotValidAsync(context.SlotId, cancellationToken))
                {
                    await ReloadSlotsAsync(cancellationToken);
                    context.SlotId = await AssignSlotAsync(headers.Host, cancellationToken);
                }
                return context.SlotId.ToString();
            }
        }

        private async ValueTask RecycleAsync()
        {
            while (true)
            {
                if (await configurationHelper.GetARRAffinitySwitchAsync())
                {
                    var expireMinutes = await configurationHelper.GetARRAffinityExpireMinutesAsync();
                    foreach (var contextDic in contexts.Values)
                    {
                        foreach (var context in contextDic.Values)
                        {
                            if (context.LastActiveTimeUtc.AddMinutes(expireMinutes) < DateTime.UtcNow)
                            {
                                contextDic.TryRemove(context.ClientAddress, out var _);
                            }
                        }
                    }
                }
                await Task.Delay(1000 * 60);
            }
        }

        private async ValueTask<bool> IsSlotValidAsync(Guid slotId, CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SlotContext>();
                return await db.Slots
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == slotId, cancellationToken);
            }
        }

        private async ValueTask<Guid> AssignSlotAsync(string host, CancellationToken cancellationToken = default)
        {
            if (slotMaps.Keys.Count == 0)
            {
                await ReloadSlotsAsync(cancellationToken);
            }
            if (!slotMaps.ContainsKey(host))
            {
                host = "*";
            }
            if (!slotMaps.ContainsKey(host))
            {
                throw new EntryPointNotFoundException($"The slot for host {host} has not been found");
            }

            while (true)
            {
                var slotId = slotMaps[host][random.Next(0, slotMaps[host].Length)];
                if (await IsSlotValidAsync(slotId, cancellationToken))
                {
                    return slotId;
                }
            }
        }
    }
}
