using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Http;

namespace Pomelo.Net.Gateway.Server
{
    public class HostInterceptor : IHttpInterceptor
    {
        public async ValueTask<bool> BackwardResponseAsync(
            HttpTunnelContext context, 
            CancellationToken cancellationToken = default)
        {
            return false;
        }

        public async ValueTask<bool> ForwardRequestAsync(
            HttpTunnelContext context, 
            CancellationToken cancellationToken = default)
        {
            context.Request.Headers.AddOrUpdate("host", "www.google.com");
            return false;
        }
    }
}
