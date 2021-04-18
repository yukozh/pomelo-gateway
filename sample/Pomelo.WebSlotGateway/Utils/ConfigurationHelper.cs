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
    public class ConfigurationHelper
    {
        public const string KeyUsername = "USERNAME";
        public const string KeyPassword = "PASSWORD";
        public const string KeyARRAffinitySwitch = "ARRSWITCH";
        public const string KeyARRAffinityExpireMinutes = "ARREXPIREMINUTES";
        public const string KeyLocalEndpoint = "LOCALENDPOINT";
        public const string KeyHealthCheckerIntervalSeconds = "HEALTHCHECKERINTERVALSECONDS";
        public const string KeyAppendForwardHeader = "APPENDFORWARDHEADER";

        private IServiceProvider services;

        public ConfigurationHelper(IServiceProvider services)
        {
            this.services = services;
        }

        public async ValueTask<string> GetValueAsync(string key, CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GatewayContext>();
                return await db.Configurations
                    .AsNoTracking()
                    .Where(x => x.Key == key)
                    .Select(x => x.Value)
                    .SingleAsync(cancellationToken);
            }
        }

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
        public async ValueTask<bool> GetAppendForwardHeaderAsync(CancellationToken cancellationToken = default)
            => Convert.ToBoolean(await GetValueAsync(KeyAppendForwardHeader, cancellationToken));
    }
}
