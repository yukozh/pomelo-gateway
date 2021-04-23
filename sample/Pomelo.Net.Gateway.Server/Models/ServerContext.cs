using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Pomelo.Net.Gateway.Server.Models
{
    public class ServerContext : DbContext
    {
        public ServerContext(DbContextOptions<ServerContext> opt)
            : base(opt)
        { }

        public DbSet<User> Users { get; set; }

        public DbSet<Endpoint> Endpoints { get; set; }

        public DbSet<EndpointUser> EndpointUsers { get; set; }

        public DbSet<InitiativeRule> InitiativeRules { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>(e =>
            {
                e.HasIndex(x => x.Role);
                e.HasMany(x => x.AllowedEndpoints)
                    .WithOne(x => x.User)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Endpoint>(e => 
            {
                e.HasIndex(x => new { x.Protocol, x.Address, x.Port })
                    .IsUnique();
                e.HasMany(x => x.EndpointUsers)
                    .WithOne(x => x.Endpoint)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }


        public async ValueTask InitDatabaseAsync()
        {
            if (await Database.EnsureCreatedAsync())
            {
                Users.Add(new User 
                {
                    Username = "admin",
                    Password = "123456",
                    Role = UserRole.Admin
                });
                await SaveChangesAsync();
            }
        }
    }
}
