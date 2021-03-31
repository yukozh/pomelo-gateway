using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Pomelo.Net.Gateway.Server.Models
{
    public class ServerContext : DbContext
    {
        public ServerContext(DbContextOptions<ServerContext> opt) : base(opt)
        { }

        public DbSet<User> Users { get; set; }

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
