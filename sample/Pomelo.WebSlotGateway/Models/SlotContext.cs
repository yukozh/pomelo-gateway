using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pomelo.WebSlotGateway.Utils;

namespace Pomelo.WebSlotGateway.Models
{
    public class SlotContext : DbContext
    {
        public SlotContext(DbContextOptions<SlotContext> opt)
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
                Description = "ARR affinity expire duration"
            });

            Configurations.Add(new Config
            {
                Key = ConfigurationHelper.KeyARRAffinitySwitch,
                Value = "true",
                Description = "ARR affinity switch"
            });

            await SaveChangesAsync(cancellationToken);
        }
    }
}
