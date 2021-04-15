using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Utils
{
    public class ConfigurationHelper : IDisposable
    {
        public const string KeyUsername = "USERNAME";
        public const string KeyPassword = "PASSWORD";
        public const string KeyARRAffinitySwitch = "ARPSWITCH";
        public const string KeyARRAffinityExpireMinutes = "ARREXPIREMINUTES";
        public const string KeyLocalEndpoint = "LOCALENDPOINT";
        public const string KeyHealthCheckerIntervalSeconds = "HEALTHCHECKERINTERVALSECONDS";

        private IServiceScope scope;
        private SlotContext db;

        public ConfigurationHelper(IServiceProvider services)
        {
            scope = services.CreateScope();
            db = scope.ServiceProvider.GetRequiredService<SlotContext>();
        }

        public async ValueTask<string> GetValueAsync(string key, CancellationToken cancellationToken = default)
            => await db.Configurations
                .Where(x => x.Key == key)
                .Select(x => x.Value)
                .SingleAsync(cancellationToken);

        public ValueTask<string> GetUsernameAsync(CancellationToken cancellationToken = default)
            => GetValueAsync(KeyUsername, cancellationToken);

        public ValueTask<string> GetPasswordAsync(CancellationToken cancellationToken = default)
            => GetValueAsync(KeyPassword, cancellationToken);

        public async ValueTask<bool> GetARRAffinitySwitchAsync(CancellationToken cancellationToken = default)
            => Convert.ToBoolean(await GetValueAsync(KeyARRAffinitySwitch, cancellationToken));

        public async ValueTask<int> GetARRAffinityExpireMinutesAsync(CancellationToken cancellationToken = default)
            => Convert.ToInt32(await GetValueAsync(KeyARRAffinityExpireMinutes, cancellationToken));

        public async ValueTask<IPEndPoint> GetLocalEndpointAsync(CancellationToken cancellationToken = default)
            => await AddressHelper.ParseAddressAsync(await GetValueAsync(KeyLocalEndpoint, cancellationToken), 0);

        public async ValueTask<int> GetHealthCheckerIntervalSecondsAsync(CancellationToken cancellationToken = default)
            => Convert.ToInt32(await GetValueAsync(KeyHealthCheckerIntervalSeconds, cancellationToken));

        public void Dispose()
        {
            scope?.Dispose();
        }
    }
}
