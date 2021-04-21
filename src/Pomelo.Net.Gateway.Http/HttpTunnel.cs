using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnel : IStreamTunnel
    {
        private IServiceProvider services;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private ConcurrentDictionary<Guid, HttpTunnelContext> contexts 
            = new ConcurrentDictionary<Guid, HttpTunnelContext>();
        private ILogger<HttpTunnel> logger;


        public Guid Id => Guid.Parse("5237e1e5-5f1a-4df0-a716-2f3dbac2a3ff");
        public string Name => "HTTP Tunnel";

        public HttpTunnel(IServiceProvider services)
        {
            this.services = services;
            this.streamTunnelContextFactory = services.GetRequiredService<StreamTunnelContextFactory>();
            this.logger = services.GetRequiredService<ILogger<HttpTunnel>>();
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
                    if (!await httpContext.Response.Headers.ParseHeaderAsync(
                        rightToTunnelStream,
                        HttpAction.Response))
                    {
                        throw new InvalidDataException("Parse header failed");
                    }
                    logger.LogInformation($"Parsed response header for {httpContext.ConnectionId}");

                    // 3. Build Body Stream
                    if (!httpContext.Response.Headers.IsKeepAlive)
                    {
                        httpContext.Response.Body = new HttpBodyStream(
                            httpContext.Response, 
                            httpContext.Response.SourceStream,
                            httpContext.Response.DestinationStream,
                            HttpBodyType.NonKeepAlive);
                    }
                    else if (httpContext.Response.Headers.StatusCode == 101
                        && httpContext.Response.Headers.Upgrade?.ToLower() == "websocket")
                    {
                        httpContext.Response.Body = new HttpBodyStream(
                            httpContext.Response,
                            httpContext.Response.SourceStream,
                            httpContext.Response.DestinationStream,
                            HttpBodyType.WebSocket);
                    }
                    else if (httpContext.Response.Headers.TransferEncoding != null
                        && httpContext.Response.Headers.TransferEncoding
                            .Any(x => x.ToLower() == "chunked"))
                    {
                        httpContext.Response.Body = new HttpBodyStream(
                            httpContext.Response,
                            httpContext.Response.SourceStream,
                            httpContext.Response.DestinationStream);
                    }
                    else if (httpContext.Response.Headers.ContentLength >= 0)
                    {
                        httpContext.Response.Body = new HttpBodyStream(
                            httpContext.Response,
                            httpContext.Response.SourceStream,
                            httpContext.Response.DestinationStream,
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
                    logger.LogInformation($"Backwarded response {httpContext.ConnectionId}");

                    // 6. Determine if disconnect is needed
                    if (httpContext.Response.Body.Type == HttpBodyType.NonKeepAlive)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"HTTP Tunnel {httpContext?.ConnectionId} destroyed due to error");
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
                    var headers = new HttpHeader();
                    if (!await headers.ParseHeaderAsync(leftToTunnelStream, HttpAction.Request))
                    {
                        break;
                    }
                    logger.LogInformation($"Parsed request header for {httpContext.ConnectionId}: {headers.Method} {headers.Url}");
                    httpContext.Request.Headers = headers;

                    // 3. Build body stream
                    if (!httpContext.Request.Headers.IsKeepAlive)
                    {
                        httpContext.Request.Body = new HttpBodyStream(
                            httpContext.Request,
                            httpContext.Request.SourceStream,
                            httpContext.Request.DestinationStream,
                            HttpBodyType.NonKeepAlive);
                    }
                    else if (httpContext.Request.Headers.TransferEncoding != null
                        && httpContext.Request.Headers.TransferEncoding
                            .Any(x => x.ToLower() == "chunked"))
                    {
                        httpContext.Request.Body = new HttpBodyStream(
                            httpContext.Request,
                            httpContext.Request.SourceStream,
                            httpContext.Request.DestinationStream);
                    }
                    else if (httpContext.Request.Headers.ContentLength >= 0
                        || httpContext.Request.Headers.Method.ToLower() == "get")
                    {
                        httpContext.Request.Body = new HttpBodyStream(
                            httpContext.Request,
                            httpContext.Request.SourceStream,
                            httpContext.Request.DestinationStream,
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
                    logger.LogInformation($"Forwarded request {httpContext.ConnectionId}");

                    // 6. Determine if disconnect is needed
                    if (httpContext.Request.Body.Type == HttpBodyType.NonKeepAlive)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"HTTP Tunnel {httpContext?.ConnectionId} destroyed due to error");
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
            return contexts.GetOrAdd(context.ConnectionId, id => new HttpTunnelContext(id));
        }

        private void OnTunnelDestroyed(Guid obj)
        {
            contexts.TryRemove(obj, out var _);
        }
    }
}
