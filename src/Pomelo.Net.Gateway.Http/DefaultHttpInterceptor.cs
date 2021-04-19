using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public class DefaultHttpInterceptor : IHttpInterceptor
    {
        public const int BufferSize = 2048;

        protected virtual async ValueTask ForwardRequestHeaderAsync(
            HttpTunnelContext context, 
            CancellationToken cancellationToken = default)
        {
            await context.Request.Headers.WriteToStreamAsync(
                context.Request.DestinationStream, 
                HttpAction.Request, 
                cancellationToken);
        }

        protected virtual async ValueTask ForwardRequestBodyAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            await context.Request.Body.CopyToAsync(context.Request.DestinationStream);
        }

        protected virtual async ValueTask BackwardResponseHeaderAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            await context.Response.Headers.WriteToStreamAsync(
                context.Response.DestinationStream,
                HttpAction.Response, 
                cancellationToken);
        }

        protected virtual async ValueTask BackwardResponseBodyAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            await context.Response.Body.CopyToAsync(context.Response.DestinationStream);
        }

        public virtual async ValueTask<bool> ForwardRequestAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            await ForwardRequestHeaderAsync(context, cancellationToken);
            await ForwardRequestBodyAsync(context, cancellationToken);
            return true;
        }

        public virtual async ValueTask<bool> BackwardResponseAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            await BackwardResponseHeaderAsync(context, cancellationToken);
            await BackwardResponseBodyAsync(context, cancellationToken);
            return true;
        }
    }
}
