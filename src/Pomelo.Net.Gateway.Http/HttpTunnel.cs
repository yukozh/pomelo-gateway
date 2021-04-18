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

            var httpContext = FindOrCreateHttpTunnelContextCreated(context);
            if (httpContext.ResponseSourceStream == null)
            {
                httpContext.ResponseSourceStream = rightToTunnelStream;
            }
            if (httpContext.ResponseDestinationStream == null)
            {
                httpContext.ResponseDestinationStream = tunnelToLeftStream;
            }

            // 1. Get Headers
            httpContext.ResponseHeaders = new HttpHeader();
            await httpContext.ResponseHeaders.ParseHeaderAsync(rightToTunnelStream, HttpAction.Response);

            // 2. Find Interceptor
            var interceptors = services.GetServices<IHttpInterceptor>();
            IHttpInterceptor interceptor = null;
            foreach (var _interceptor in interceptors)
            {
                if (_interceptor.CanIntercept(httpContext.ResponseHeaders, HttpAction.Response))
                {
                    interceptor = _interceptor;
                    break;
                }
            }

            // 3. Backward Response
            if (interceptor == null)
            {
                throw new NotSupportedException("This stream is not supported");
            }
            await interceptor.BackwardResponseAsync(
                httpContext, 
                cancellationToken);
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

            var httpContext = FindOrCreateHttpTunnelContextCreated(context);
            httpContext.ConnectionId = context.ConnectionId;
            if (httpContext.RequestSourceStream == null)
            {
                httpContext.RequestSourceStream = leftToTunnelStream;
            }
            if (httpContext.RequestDestinationStream == null)
            {
                httpContext.RequestDestinationStream = tunnelToRightStream;
            }

            // 1. Get Headers
            httpContext.RequestHeaders = new HttpHeader();
            var result = await httpContext.RequestHeaders.ParseHeaderAsync(leftToTunnelStream, HttpAction.Request);

            // 2. Find Interceptor
            var interceptors = services.GetServices<IHttpInterceptor>();
            IHttpInterceptor interceptor = null;
            foreach (var _interceptor in interceptors)
            {
                if (_interceptor.CanIntercept(httpContext.RequestHeaders, HttpAction.Request))
                {
                    interceptor = _interceptor;
                    break;
                }
            }

            if (interceptor == null)
            {
                throw new NotSupportedException("This stream is not supported");
            }

            // 3. Forward Request
            await interceptor.ForwardRequestAsync(
                httpContext, 
                cancellationToken);
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
