using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public interface IHttpInterceptor
    {
        bool CanIntercept(HttpHeader requestHeaders, HttpAction action);

        ValueTask ForwardRequestAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default);

        ValueTask BackwardResponseAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default);
    }
}
