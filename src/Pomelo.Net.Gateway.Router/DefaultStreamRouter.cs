using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Router
{
    public class DefaultStreamRouter : IStreamRouter
    {
        public Guid Id => Guid.Parse("6486995f-b40c-47db-96e5-4e50443a47a3");
        public string Name => "Default Stream Router";
        public int ExpectedBufferSize => 0;
        private IServiceProvider services;

        public DefaultStreamRouter(IServiceProvider services)
        {
            this.services = services;
        }

        public async ValueTask<RouteResult> RouteAsync(
            Stream stream, 
            Memory<byte> buffer,
            IPEndPoint listenerEndPoint,
            IPEndPoint clientEndPoint,
            CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var endPointProvider = scope.ServiceProvider.GetRequiredService<IEndPointProvider>();
                var _endpoint = await endPointProvider.GetActiveEndPointAsync(Protocol.TCP, listenerEndPoint, cancellationToken);

                if (_endpoint == null)
                {
                    return default;
                }

                return new RouteResult
                {
                    IsSucceeded = true,
                    HeaderLength = 0,
                    UserId = _endpoint.UserIds.FirstOrDefault(), // In default stream router, a listener should only be consumed by single user.
                };
            }
        }
    }

    public static class DefaultStreamRouterExtensions
    {
        public static IServiceCollection AddDefaultStreamRouter(this IServiceCollection services)
        {
            return services.AddSingleton<IStreamRouter, DefaultStreamRouter>();
        }
    }
}
