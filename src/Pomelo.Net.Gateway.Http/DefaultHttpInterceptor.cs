using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public class DefaultHttpInterceptor : IHttpInterceptor
    {
        public const int BufferSize = 2048;
        public virtual bool CanIntercept(HttpHeader requestHeaders, HttpAction action) => true;

        protected virtual async ValueTask ForwardRequestHeaderAsync(
            HttpTunnelContext context, 
            CancellationToken cancellationToken = default)
        {
            await context.RequestHeaders.WriteToStreamAsync(
                context.RequestDestinationStream, 
                HttpAction.Request, 
                cancellationToken);
        }

        protected virtual async ValueTask ForwardRequestBodyAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(BufferSize))
            {
                while (true)
                {
                    var length = await context.RequestSourceStream.ReadAsync(buffer.Memory);
                    if (length == 0)
                    {
                        break;
                    }
                    await context.RequestDestinationStream.WriteAsync(buffer.Memory.Slice(0, length), cancellationToken);
                    if (context.RequestHeaders.Contains("content-length") && context.RequestHeaders.ContentLength >= length)
                    {
                        break;
                    }
                }
            }
        }

        protected virtual async ValueTask BackwardResponseHeaderAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            await context.ResponseHeaders.WriteToStreamAsync(
                context.ResponseDestinationStream,
                HttpAction.Response, 
                cancellationToken);
        }

        protected virtual async ValueTask BackwardResponseBodyAsync(
            HttpTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(BufferSize))
            {
                while (true)
                {
                    var length = await context.ResponseSourceStream.ReadAsync(buffer.Memory);
                    if (length == 0)
                    {
                        break;
                    }
                    await context.ResponseDestinationStream.WriteAsync(buffer.Memory.Slice(0, length), cancellationToken);
                    if (context.ResponseHeaders.Contains("content-length") && context.ResponseHeaders.ContentLength >= length)
                    {
                        break;
                    }
                }
            }
        }

        public virtual async ValueTask ForwardRequestAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            await ForwardRequestHeaderAsync(context, cancellationToken);
            await ForwardRequestBodyAsync(context, cancellationToken);
        }

        public virtual async ValueTask BackwardResponseAsync(HttpTunnelContext context, CancellationToken cancellationToken = default)
        {
            await BackwardResponseHeaderAsync(context, cancellationToken);
            await BackwardResponseBodyAsync(context, cancellationToken);
        }
    }
}
