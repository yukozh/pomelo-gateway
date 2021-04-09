using System.Net;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface IUdpEndpointListenerFinder
    {
        public PomeloUdpClient FindServerByEndpoint(IPEndPoint endpoint);
    }
}
