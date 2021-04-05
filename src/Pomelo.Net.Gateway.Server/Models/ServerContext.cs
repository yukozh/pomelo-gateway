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

        public DbSet<PublicRule> PublicRules { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>(e =>
            {
                e.HasIndex(x => x.Role);
            });

            builder.Entity<PublicRule>(e =>
            {
                e.HasIndex(x => new { x.Protocol, x.ServerEndpoint });
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
