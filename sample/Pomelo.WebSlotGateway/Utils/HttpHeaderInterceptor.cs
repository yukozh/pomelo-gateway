using System.Threading;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Http;

namespace Pomelo.WebSlotGateway.Utils
{
    public class HttpHeaderInterceptor : IHttpInterceptor
    {
        private ConfigurationHelper config;

        public HttpHeaderInterceptor(ConfigurationHelper config)
        {
            this.config = config;
        }

        public async ValueTask<bool> BackwardResponseAsync(
            HttpTunnelContext context, 
            CancellationToken cancellationToken = default)
        {
            if (await config.GetAppendForwardHeaderAsync(cancellationToken))
            {
                context.Response.Headers.TryAdd("X-Forward-Server", "Pomelo Gateway");
            }
            return false;
        }

        public async ValueTask<bool> ForwardRequestAsync(
            HttpTunnelContext context, 
            CancellationToken cancellationToken = default)
        {
            if (await config.GetAppendForwardHeaderAsync(cancellationToken))
            {
                var from = context.ClientEndPoint;
                context.Request.Headers.TryAdd("X-Forwarded-For", from.ToString());
                context.Request.Headers.TryAdd("X-Forward-Server", "Pomelo Gateway");
                context.Request.Headers.TryAdd("X-Real-IP", from.Address.ToString());
                context.Request.Headers.TryAdd("RemoteAddress", from.Address.ToString());
            }
            context.Request.Headers.AddOrUpdate("Accept-Encoding", "identity");
            return false;
        }
    }
}
