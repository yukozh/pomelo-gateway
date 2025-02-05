using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace Pomelo.Net.Gateway.Router
{
    public interface IPacketRouter
    {
        Guid Id { get; }
        string Name { get; }
        ValueTask<string> RouteAsync(ArraySegment<byte> packet, IPEndPoint endpoint, CancellationToken cancellationToken = default);
    }
}
