using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway.Models
{
    public class GatewayContext : DbContext
    {
        public GatewayContext(DbContextOptions<GatewayContext> opt)
            : base(opt)
        {
            if (Database.EnsureCreated())
            {
                InitDatabaseAsync().GetAwaiter().GetResult();
            }
        }

        public DbSet<Slot> Slots { get; set; }

        public DbSet<Config> Configurations { get; set; }

        public async ValueTask InitDatabaseAsync(CancellationToken cancellationToken = default)
        {
            Configurations.Add(new Config 
            {
                Key = ConfigurationHelper.KeyUsername,
                Value = "admin",
                Description = "Username for log on this platform"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyPassword,
                Value = "123456",
                Description = "Password for log on this platform"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyLocalEndpoint,
                Value = "0.0.0.0:8000",
                Description = "The entry endpoint from this server"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyARRAffinityExpireMinutes,
                Value = "20",
                Description = "ARR affinity expire duration (Unit: min)"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyARRAffinitySwitch,
                Value = "true",
                Description = "ARR affinity switch. (true|false)"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyHealthCheckerIntervalSeconds,
                Value = "30",
                Description = "Health check interval (Unit: sec)"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyAppendForwardHeader,
                Value = "true",
                Description = "Append forward info into header. (true|false)"
            });

            await SaveChangesAsync(cancellationToken);
        }
    }
}
