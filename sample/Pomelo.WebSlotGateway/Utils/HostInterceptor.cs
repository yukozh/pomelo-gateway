using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Http;

namespace Pomelo.WebSlotGateway.Utils
{
    public class HostInterceptor : IHttpInterceptor
    {
        private ConfigurationHelper config;

        public HostInterceptor(ConfigurationHelper config)
        {
            this.config = config;
        }

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
            var host = await config.GetOverrideHostAsync(cancellationToken);
            if (!string.IsNullOrEmpty(host))
            {
                context.Request.Headers.AddOrUpdate("host", host);
            }
            return false;
        }
    }
}
