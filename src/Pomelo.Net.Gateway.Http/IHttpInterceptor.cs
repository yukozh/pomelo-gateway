using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public interface IHttpInterceptor
    {
        ValueTask<bool> ForwardRequestAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default);

        ValueTask<bool> BackwardResponseAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default);
    }
}
