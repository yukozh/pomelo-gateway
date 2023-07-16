using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        public async ValueTask<string> RouteAsync(ArraySegment<byte> packet, IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            using (var scope = services.CreateScope())
            {
                var endPointProvider = scope.ServiceProvider.GetRequiredService<IEndPointProvider>();
                var _endPoint = await endPointProvider.GetActiveEndPointAsync(Protocol.UDP, endpoint, cancellationToken);

                if (_endPoint == null)
                {
                    return default;
                }

                return _endPoint.UserIds.FirstOrDefault();
            }
        }
    }
}
