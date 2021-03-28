using System.Threading.Tasks;
using Pomelo.Net.Gateway.Association.Token;

namespace Pomelo.Net.Gateway.Association.Authentication
{
    public class DefaultTokenValidator : ITokenValidator
    {
        private AssociateServer server;

        public DefaultTokenValidator(AssociateServer server)
        {
            this.server = server;
        }

        public async ValueTask<bool> ValidateAsync(long token, string userIdentifier)
        {
            var context = server.GetAssociateContextByUserIdentifier(userIdentifier);
            if (context == null)
            {
                return false;
            }
            return context.Credential.Token == token;
        }
    }
}
