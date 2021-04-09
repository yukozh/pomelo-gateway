using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.Router
{
    public class DefaultPacketRouter : IPacketRouter
    {
        private IServiceProvider services;

        public Guid Id => Guid.Parse("73e28de9-dfcd-49c3-a1eb-374528bb4f1e");
        public string Name => "Default Packet Router";


        public DefaultPacketRouter(IServiceProvider services)
        {
            this.services = services;
        }

        public async ValueTask<string> DetermineIdentifierAsync(ArraySegment<byte> packet, IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var _endpoint = await scope.ServiceProvider.GetRequiredService<EndpointContext>()
                    .Endpoints
                    .Include(x => x.Users)
                    .SingleOrDefaultAsync(x =>
                        x.Address == endpoint.Address.ToString()
                        && x.Protocol == Protocol.UDP
                        && x.Port == (ushort)endpoint.Port);

                if (_endpoint == null)
                {
                    return default;
                }

                return _endpoint.Users.FirstOrDefault()?.UserIdentifier;
            }
        }
    }
}
