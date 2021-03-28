using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Pomelo.Net.Gateway.Association.Authentication
{
    public class DefaultBasicAuthenticator : BasicAuthenticator
    {
        private IConfiguration config;

        public DefaultBasicAuthenticator(IConfiguration config)
        {
            this.config = config;
        }

        public override ValueTask<bool> ValidateUserNameAndPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(username == config["Username"] && password == config["Password"]);
    }
}
