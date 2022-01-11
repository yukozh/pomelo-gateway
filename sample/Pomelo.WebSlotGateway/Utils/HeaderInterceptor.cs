using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Http;

namespace Pomelo.WebSlotGateway.Utils
{
    public class HeaderInterceptor : IHttpInterceptor
    {
        private ConfigurationHelper config;

        public HeaderInterceptor(ConfigurationHelper config)
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

            var refererFrom = await config.GetOverrideRefererFromAsync(cancellationToken);
            var refererTo = await config.GetOverrideRefererToAsync(cancellationToken);
            if (!string.IsNullOrEmpty(refererFrom) && !string.IsNullOrEmpty(context.Request.Headers.Referer))
            {
                context.Request.Headers.AddOrUpdate("referer", context.Request.Headers.Referer.Replace(refererFrom, refererTo));
            }

            return false;
        }
    }
}
