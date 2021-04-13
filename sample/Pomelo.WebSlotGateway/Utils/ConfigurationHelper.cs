using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pomelo.WebSlotGateway.Models;

namespace Pomelo.WebSlotGateway.Utils
{
    public class ConfigurationHelper
    {
        public const string KeyUsername = "USERNAME";
        public const string KeyPassword = "PASSWORD";
        public const string KeyARRAffinitySwitch = "ARPSWITCH";
        public const string KeyARRAffinityExpireMinutes = "ARREXPIREMINUTES";
        public const string KeyLocalEndpoint = "LOCALENDPOINT";

        private SlotContext db;

        public ConfigurationHelper(SlotContext db)
        {
            this.db = db;
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
    }
}
