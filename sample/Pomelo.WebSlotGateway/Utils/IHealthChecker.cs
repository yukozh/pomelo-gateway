using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.WebSlotGateway.Utils
{
    public interface IHealthChecker
    {
        ValueTask<bool> IsHealthAsync(IPEndPoint destination, CancellationToken cancellationToken = default);
    }
}
