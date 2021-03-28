using Microsoft.EntityFrameworkCore;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class RuleContext : DbContext
    {
        public DbSet<Endpoint> Endpoints { get; set; }

        public DbSet<EndpointUser> EndpointUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Endpoint>(e => 
            {
                e.HasIndex(x => new { x.Protocol, x.Address, x.Port }).IsUnique();
            });
        }
    }
}
