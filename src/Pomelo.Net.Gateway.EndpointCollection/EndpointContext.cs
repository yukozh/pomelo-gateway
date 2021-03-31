using Microsoft.EntityFrameworkCore;

namespace Pomelo.Net.Gateway.EndpointCollection
{
    public class EndpointContext : DbContext
    {
        public EndpointContext(DbContextOptions<EndpointContext> opt) : base(opt)
        { }

        public DbSet<Endpoint> Endpoints { get; set; }

        public DbSet<EndpointUser> EndpointUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Endpoint>(e => 
            {
                e.HasIndex(x => new { x.Protocol, x.Address, x.Port }).IsUnique();
            });

            builder.Entity<EndpointUser>(e => 
            {
                e.HasKey(x => new { x.EndpointId, x.UserIdentifier });
            });
        }
    }
}
