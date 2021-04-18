using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Router;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Utils
{
    public class HealthCheckerProcesser : IDisposable
    {
        private IServiceProvider services;
        private IServiceScope scope;
        private IHealthChecker healthChecker;
        private ConfigurationHelper config;
        private ARRAffinityRouter router;
        private ILogger<HealthCheckerProcesser> logger;

        public HealthCheckerProcesser(IServiceProvider services)
        {
            this.services = services;
            this.scope = services.CreateScope();
            this.logger = services.GetRequiredService<ILogger<HealthCheckerProcesser>>();
            this.healthChecker = scope.ServiceProvider.GetRequiredService<IHealthChecker>();
            this.config = scope.ServiceProvider.GetRequiredService<ConfigurationHelper>();
            this.router = scope.ServiceProvider.GetServices<IStreamRouter>().Single(x => x is ARRAffinityRouter) as ARRAffinityRouter;
            StartAsync();
        }

        public void Dispose()
        {
            scope?.Dispose();
        }

        private async ValueTask StartAsync()
        {
            while (true)
            {
                try
                {
                    using (var _scope = services.CreateScope())
                    {
                        var db = _scope.ServiceProvider.GetRequiredService<GatewayContext>();
                        var slots = await db.Slots.ToListAsync();
                        var shouldRebuildSlotAssignArray = false;
                        foreach (var slot in slots)
                        {
                            if (slot.Status == SlotStatus.Disabled)
                            {
                                continue;
                            }

                            logger.LogInformation($"Checking slot {slot.Name} health");
                            if (await healthChecker.IsHealthAsync(await AddressHelper.ParseAddressAsync(slot.Destination, 0)))
                            {
                                if (slot.Status != SlotStatus.Enabled)
                                {
                                    shouldRebuildSlotAssignArray = true;
                                }
                                slot.Status = SlotStatus.Enabled;
                            }
                            else
                            {
                                if (slot.Status != SlotStatus.Error)
                                {
                                    shouldRebuildSlotAssignArray = true;
                                }
                                slot.Status = SlotStatus.Error;
                                logger.LogInformation($"Checking slot {slot.Name} is unhealth, disabling...");
                            }
                        }
                        await db.SaveChangesAsync();
                        if (shouldRebuildSlotAssignArray)
                        {
                            await router.ReloadSlotsAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
                await Task.Delay(await config.GetHealthCheckerIntervalSecondsAsync() * 1000);
            }
        }
    }
}
