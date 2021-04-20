using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnel : IStreamTunnel
    {
        private IServiceProvider services;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private const int BufferSize = 2048;
        private ConcurrentDictionary<Guid, HttpTunnelContext> contexts 
            = new ConcurrentDictionary<Guid, HttpTunnelContext>();
        public Guid Id => Guid.Parse("5237e1e5-5f1a-4df0-a716-2f3dbac2a3ff");
        public string Name => "HTTP Tunnel";

        public HttpTunnel(IServiceProvider services)
        {
            this.services = services;
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
        }

        public async ValueTask BackwardAsync(
            Stream rightToTunnelStream,
            Stream tunnelToLeftStream,
            StreamTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            if (!tunnelToLeftStream.CanWrite)
            {
                return;
            }

            while (true)
            {
                // 1. Prepare context
                var httpContext = FindOrCreateHttpTunnelContextCreated(context);
                httpContext.Response.SourceStream = rightToTunnelStream;
                httpContext.Response.DestinationStream = tunnelToLeftStream;
                httpContext.StreamTunnelContext = context;
                httpContext.Response.Body?.Dispose();

                try
                {
                    // 2. Get Headers
                    httpContext.Response.Headers = new HttpHeader();
                    await httpContext.Response.Headers.ParseHeaderAsync(
                        rightToTunnelStream,
                        HttpAction.Response);

                    // 3. Build Body Stream
                    if (!httpContext.Response.Headers.IsKeepAlive)
                    {
                        httpContext.Response.Body = new HttpBodyReadonlyStream(
                            httpContext.Response.SourceStream, HttpBodyType.NonKeepAlive);
                    }
                    else if (httpContext.Response.Headers.StatusCode == 101
                        && httpContext.Response.Headers.Upgrade?.ToLower() == "websocket")
                    {
                        httpContext.Response.Body = new HttpBodyReadonlyStream(
                            httpContext.Response.SourceStream, HttpBodyType.WebSocket);
                    }
                    else if (httpContext.Response.Headers.TransferEncoding != null
                        && httpContext.Response.Headers.TransferEncoding
                            .Any(x => x.ToLower() == "chunked"))
                    {
                        httpContext.Response.Body = new HttpBodyReadonlyStream(
                            httpContext.Response.SourceStream);
                    }
                    else if (httpContext.Response.Headers.ContentLength >= 0)
                    {
                        httpContext.Response.Body = new HttpBodyReadonlyStream(
                            httpContext.Response.SourceStream,
                            httpContext.Response.Headers.ContentLength);
                    }

                    // 4. Find Interceptor
                    var interceptors = services
                        .GetServices<IHttpInterceptor>()
                        .Reverse();

                    // 5. Backward Response
                    var handled = false;
                    foreach (var interceptor in interceptors)
                    {
                        if (await interceptor.BackwardResponseAsync(
                            httpContext,
                            cancellationToken))
                        {
                            handled = true;
                            break;
                        }
                    }
                    if (!handled)
                    {
                        throw new NotSupportedException("This stream is not supported");
                    }

                    // 6. Determine if disconnect is needed
                    if (httpContext.Response.Body.Type == HttpBodyType.NonKeepAlive)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    streamTunnelContextFactory.DestroyContext(httpContext?.ConnectionId ?? default);
                    throw;
                }
                finally
                {
                    httpContext?.Dispose();
                }
            }
        }

        public async ValueTask ForwardAsync(
            Stream leftToTunnelStream,
            Stream tunnelToRightStream,
            StreamTunnelContext context,
            CancellationToken cancellationToken = default)
        {
            if (!tunnelToRightStream.CanWrite)
            {
                return;
            }

            while (true)
            {
                // 1. Prepare context
                var httpContext = FindOrCreateHttpTunnelContextCreated(context);
                httpContext.ConnectionId = context.ConnectionId;
                httpContext.Request.SourceStream = leftToTunnelStream;
                httpContext.Request.DestinationStream = tunnelToRightStream;
                httpContext.StreamTunnelContext = context;
                httpContext.Request.Body?.Dispose();

                try
                {
                    // 2. Get Headers
                    httpContext.Request.Headers = new HttpHeader();
                    var result = await httpContext.Request.Headers.ParseHeaderAsync(leftToTunnelStream, HttpAction.Request);

                    // 3. Build body stream
                    if (!httpContext.Request.Headers.IsKeepAlive)
                    {
                        httpContext.Request.Body = new HttpBodyReadonlyStream(
                            httpContext.Request.SourceStream, HttpBodyType.NonKeepAlive);
                    }
                    else if (httpContext.Request.Headers.TransferEncoding != null
                        && httpContext.Request.Headers.TransferEncoding
                            .Any(x => x.ToLower() == "chunked"))
                    {
                        httpContext.Request.Body = new HttpBodyReadonlyStream(
                            httpContext.Request.SourceStream);
                    }
                    else if (httpContext.Request.Headers.ContentLength >= 0
                        || httpContext.Request.Headers.Method.ToLower() == "get")
                    {
                        httpContext.Request.Body = new HttpBodyReadonlyStream(
                            httpContext.Request.SourceStream,
                            httpContext.Request.Headers.ContentLength);
                    }

                    // 4. Find Interceptor
                    var interceptors = services
                        .GetServices<IHttpInterceptor>()
                        .Reverse();

                    // 5. Forward Request
                    var handled = false;
                    foreach (var interceptor in interceptors)
                    {
                        if (await interceptor.ForwardRequestAsync(
                            httpContext,
                            cancellationToken))
                        {
                            handled = true;
                            break;
                        }
                    }
                    if (!handled)
                    {
                        throw new NotSupportedException("This stream is not supported");
                    }

                    // 6. Determine if disconnect is needed
                    if (httpContext.Request.Body.Type == HttpBodyType.NonKeepAlive)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    streamTunnelContextFactory.DestroyContext(httpContext?.ConnectionId ?? default);
                    throw;
                }
                finally
                {
                    httpContext?.Dispose();
                }
            }
        }

        private HttpTunnelContext FindOrCreateHttpTunnelContextCreated(StreamTunnelContext context)
        {
            context.OnContextDisposed += OnTunnelDestroyed;
            return contexts.GetOrAdd(context.ConnectionId, (_) => new HttpTunnelContext());
        }

        private void OnTunnelDestroyed(Guid obj)
        {
            contexts.TryRemove(obj, out var _);
        }
    }
}
