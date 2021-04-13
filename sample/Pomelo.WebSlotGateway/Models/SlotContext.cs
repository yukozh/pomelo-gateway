using Microsoft.EntityFrameworkCore;

namespace Pomelo.WebSlotGateway.Models
{
    public class SlotContext : DbContext
    {
        public SlotContext(DbContextOptions<SlotContext> opt)
            : base(opt)
        { }

        public DbSet<Slot> Slots { get; set; }

        public DbSet<Config> Configurations { get; set; }
    }
}
