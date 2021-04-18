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
        public virtual bool CanIntercept(HttpHeader requestHeaders, HttpAction action) => true;

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
            using (var buffer = MemoryPool<byte>.Shared.Rent(BufferSize))
            {
                if (context.Request.Headers.ContentLength >= 0 
                    || context.Request.Headers.Method.ToLower() == "get") // Request with Content-Length, GET request does not need that header
                {
                    var count = 0;
                    while (count < context.Request.Headers.ContentLength)
                    {
                        var read = await context.Request.SourceStream.ReadAsync(buffer.Memory, cancellationToken);
                        if (read == 0)
                        {
                            throw new IOException("Unexpected EOF of stream");
                        }
                        await context.Request.DestinationStream.WriteAsync(buffer.Memory.Slice(0, read), cancellationToken);
                        count += read;
                    }
                }
                else if (context.Request.Headers.ContentLength == -1
                    && context.Request.Headers.TransferEncoding != null
                    && context.Request.Headers.TransferEncoding.Any(x => x.ToLower() == "chunked"))
                {
                    using (var lengthBuffer = MemoryPool<byte>.Shared.Rent(4))
                    {
                        var _lengthBuffer = lengthBuffer.Memory.Slice(0, 4);
                        while (true)
                        {
                            // Chunk header
                            await context.Request.SourceStream.ReadExAsync(_lengthBuffer, cancellationToken);
                            await context.Request.DestinationStream.WriteAsync(_lengthBuffer, cancellationToken);

                            // Chunk body
                            var length = BitConverter.ToUInt16(_lengthBuffer.Slice(0, 2).Span);
                            var count = 0;
                            while (count < length + 2)
                            {
                                var read = await context.Request.SourceStream.ReadAsync(buffer.Memory, cancellationToken);
                                await context.Request.DestinationStream.WriteAsync(buffer.Memory.Slice(0, read), cancellationToken);
                                if (read == 0)
                                {
                                    throw new IOException("Unexpected EOF of stream");
                                }
                                count += read;
                            }
                            if (length == 0)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
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
            using (var buffer = MemoryPool<byte>.Shared.Rent(BufferSize))
            {
                if (context.Response.Headers.ContentLength >= 0) // Response with Content-Length
                {
                    var count = 0;
                    while (count < context.Response.Headers.ContentLength)
                    {
                        var read = await context.Response.SourceStream.ReadAsync(buffer.Memory, cancellationToken);
                        if (read == 0)
                        {
                            throw new IOException("Unexpected EOF of stream");
                        }
                        await context.Response.DestinationStream.WriteAsync(buffer.Memory.Slice(0, read), cancellationToken);
                        count += read;
                    }
                }
                else if (context.Response.Headers.ContentLength == -1
                    && context.Request.Headers.TransferEncoding != null
                    && context.Response.Headers.TransferEncoding.Any(x => x.ToLower() == "chunked"))
                {
                    using (var lengthBuffer = MemoryPool<byte>.Shared.Rent(4))
                    {
                        var _lengthBuffer = lengthBuffer.Memory.Slice(0, 4);
                        while (true)
                        {
                            // Chunk header
                            await context.Response.SourceStream.ReadExAsync(_lengthBuffer, cancellationToken);
                            await context.Response.DestinationStream.WriteAsync(_lengthBuffer, cancellationToken);

                            // Chunk body
                            var length = BitConverter.ToUInt16(_lengthBuffer.Slice(0, 2).Span);
                            var count = 0;
                            while (count < length + 2)
                            {
                                var read = await context.Response.SourceStream.ReadAsync(buffer.Memory, cancellationToken);
                                await context.Response.DestinationStream.WriteAsync(buffer.Memory.Slice(0, read), cancellationToken);
                                if (read == 0)
                                {
                                    throw new IOException("Unexpected EOF of stream");
                                }
                                count += read;
                            }
                            if (length == 0)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException();
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
