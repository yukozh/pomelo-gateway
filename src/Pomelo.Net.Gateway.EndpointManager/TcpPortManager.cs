using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.EndpointManager
{
    public class TcpPortManager
    {
        private RuleContext context;

        public TcpPortManager(RuleContext context)
        {
            this.context = context;
        }

        public async ValueTask RemoveAllRulesFromUserIdentifierAsync(
            string identifier, 
            CancellationToken cancellationToken = default)
        {
            context.EndpointUsers.RemoveRange(context.EndpointUsers.Where(x => x.UserIdentifier == identifier));
            await context.SaveChangesAsync(cancellationToken);
            var endpointsToRecycle = await context.Endpoints.Where(x => x.Users.Count == 0).ToListAsync(cancellationToken);
            if (endpointsToRecycle.Count > 0)
            {
                foreach (var endpoint in endpointsToRecycle)
                {
                    RecycleEndpoint(endpoint);
                }
                context.Endpoints.RemoveRange(endpointsToRecycle);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private void RecycleEndpoint(Endpoint endpoint)
        { 
            // TODO
        }
    }
}
