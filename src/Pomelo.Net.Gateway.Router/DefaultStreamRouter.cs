using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async ValueTask<RouteResult> DetermineIdentifierAsync(
            Stream stream, 
            Memory<byte> buffer, 
            IPEndPoint endpoint, 
            CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var _endpoint = await scope.ServiceProvider.GetRequiredService<EndpointContext>()
                    .Endpoints
                    .Include(x => x.Users)
                    .SingleOrDefaultAsync(x =>
                        x.Address == endpoint.Address.ToString()
                        && x.Protocol == Protocol.TCP
                        && x.Port == (ushort)endpoint.Port);

                if (_endpoint == null)
                {
                    return default;
                }

                return new RouteResult
                {
                    IsSucceeded = true,
                    HeaderLength = 0,
                    Identifier = _endpoint.Users.FirstOrDefault()?.UserIdentifier
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
