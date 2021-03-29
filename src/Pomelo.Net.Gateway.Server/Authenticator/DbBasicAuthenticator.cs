using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Association.Authentication;
using Pomelo.Net.Gateway.Server.Models;

namespace Pomelo.Net.Gateway.Server.Authenticator
{
    public class DbBasicAuthenticator : BasicAuthenticator, IDisposable
    {
        private IServiceScope scope;
        private ServerContext db;

        public DbBasicAuthenticator(IServiceProvider services)
        {
            this.scope = services.CreateScope();
            this.db = scope.ServiceProvider.GetRequiredService<ServerContext>();
            this.db.InitDatabaseAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            scope?.Dispose();
        }

        public override async ValueTask<bool> ValidateUserNameAndPasswordAsync(
            string username, 
            string password, 
            CancellationToken cancellationToken = default)
        {
            return await db.Users.AnyAsync(x 
                => x.Username == username 
                    && x.Password == password, 
                cancellationToken);
        }
    }
}
