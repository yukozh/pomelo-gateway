using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnel : IStreamTunnel
    {
        private IServiceProvider services;
        private const int BufferSize = 2048;
        private ConcurrentDictionary<Guid, HttpTunnelContext> contexts = new ConcurrentDictionary<Guid, HttpTunnelContext>();
        public Guid Id => Guid.Parse("5237e1e5-5f1a-4df0-a716-2f3dbac2a3ff");
        public string Name => "HTTP Tunnel";

        public HttpTunnel(IServiceProvider services)
        {
            this.services = services;
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

                // 2. Get Headers
                httpContext.Response.Headers = new HttpHeader();
                await httpContext.Response.Headers.ParseHeaderAsync(rightToTunnelStream, HttpAction.Response);

                // 3. Find Interceptor
                var interceptors = services.GetServices<IHttpInterceptor>();
                IHttpInterceptor interceptor = null;
                foreach (var _interceptor in interceptors)
                {
                    if (_interceptor.CanIntercept(httpContext.Response.Headers, HttpAction.Response))
                    {
                        interceptor = _interceptor;
                        break;
                    }
                }

                // 4. Backward Response
                if (interceptor == null)
                {
                    throw new NotSupportedException("This stream is not supported");
                }
                await interceptor.BackwardResponseAsync(
                    httpContext,
                    cancellationToken);

                // 5. Determine if disconnect is needed
                if (httpContext.Request.Headers.Protocol.ToLower() == "http/1.0"
                    || httpContext.Response.Headers.Protocol.ToLower() == "http/1.0")
                {
                    break;
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

                // 2. Get Headers
                httpContext.Request.Headers = new HttpHeader();
                var result = await httpContext.Request.Headers.ParseHeaderAsync(leftToTunnelStream, HttpAction.Request);

                // 3. Find Interceptor
                var interceptors = services.GetServices<IHttpInterceptor>();
                IHttpInterceptor interceptor = null;
                foreach (var _interceptor in interceptors)
                {
                    if (_interceptor.CanIntercept(httpContext.Request.Headers, HttpAction.Request))
                    {
                        interceptor = _interceptor;
                        break;
                    }
                }

                if (interceptor == null)
                {
                    throw new NotSupportedException("This stream is not supported");
                }

                // 4. Forward Request
                await interceptor.ForwardRequestAsync(
                    httpContext,
                    cancellationToken);

                // 5. Determine if disconnect is needed
                if (httpContext.Request.Headers.Protocol.ToLower() == "http/1.0"
                    || httpContext.Response.Headers.Protocol.ToLower() == "http/1.0")
                {
                    break;
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
